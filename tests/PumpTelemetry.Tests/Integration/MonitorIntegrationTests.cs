using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace PumpTelemetry.Tests;

[Trait("Category", "Integration")]
public sealed class MonitorIntegrationTests : IDisposable
{
    // Each test gets its own working directory so it cannot change the user's history.
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private string HistoryPath => Path.Combine(_directory, "alert-history.json");

    public MonitorIntegrationTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task HistoryOnlyWithNoFileDisplaysEmptyHistoryAndExits()
    {
        var result = await RunAsync("--history");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No saved alerts.", result.Output);
        Assert.DoesNotContain("monitor running", result.Output);
        Assert.False(File.Exists(HistoryPath));
    }

    [Fact]
    public async Task HistoryOnlyLoadsFiltersAndOrdersSavedJsonWithoutChangingIt()
    {
        // Handwritten fixture tests the external file contract without using the store to seed it.
        const string json = """
        [
          {"Timestamp":"2026-01-01T00:01:00Z","Type":"HighPressure","DeviceId":"old-pump","Value":125,"Threshold":120,"Unit":"pressure","Message":"Old alert"},
          {"Timestamp":"2026-01-01T00:03:00Z","Type":"DeviceSilent","DeviceId":"newest-pump","Value":null,"Threshold":5,"Unit":"seconds silent","Message":"Silent alert"},
          {"Timestamp":"2026-01-01T00:02:00Z","Type":"HighPressure","DeviceId":"middle-pump","Value":140,"Threshold":120,"Unit":"pressure","Message":"Middle alert"}
        ]
        """;
        await File.WriteAllTextAsync(HistoryPath, json);
        var result = await RunAsync("--history", "--last=2");
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("old-pump", result.Output);
        Assert.Contains("DeviceSilent newest-pump", result.Output);
        Assert.Contains("HighPressure middle-pump", result.Output);
        Assert.True(result.Output.IndexOf("newest-pump", StringComparison.Ordinal) <
                    result.Output.IndexOf("middle-pump", StringComparison.Ordinal));
        Assert.DoesNotContain("monitor running", result.Output);
        Assert.Equal(json, await File.ReadAllTextAsync(HistoryPath));
    }

    [Theory]
    [InlineData("--last=0")]
    [InlineData("--last=abc")]
    [InlineData("--unknown")]
    public async Task InvalidCommandReturnsErrorWithoutStartingMonitor(string argument)
    {
        var result = await RunAsync(argument);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Usage:", result.Error);
        Assert.DoesNotContain("monitor running", result.Output);
        Assert.False(File.Exists(HistoryPath));
    }

    [Fact]
    public async Task CorruptHistoryReturnsErrorAndPreservesFile()
    {
        await File.WriteAllTextAsync(HistoryPath, "broken JSON");
        var result = await RunAsync("--history");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Cannot load alert history", result.Error);
        Assert.Equal("broken JSON", await File.ReadAllTextAsync(HistoryPath));
    }

    [Fact]
    public async Task LiveMonitorPersistsAlertsThatANewProcessCanDisplay()
    {
        using (var process = Start())
        {
            // Drain pipes while monitoring, so a full pipe cannot block persistence.
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            try
            {
                // Wait for an actual saved alert rather than assuming a fixed startup time.
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (true)
                {
                    timeout.Token.ThrowIfCancellationRequested();
                    Assert.False(process.HasExited, "Monitor exited before saving an alert.");
                    if (File.Exists(HistoryPath))
                    {
                        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(HistoryPath));
                        if (json.RootElement.GetArrayLength() > 0) break;
                    }
                    await Task.Delay(50, timeout.Token);
                }
            }
            finally
            {
                // Test persistence after termination; graceful Ctrl+C shutdown is a separate concern.
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                await Task.WhenAll(output, error);
            }
        }
        var result = await RunAsync("--history", "--last=1");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("pump-", result.Output);
        Assert.Single(result.Output.Split('\n').Where(line => line.Contains("value=")));
    }

    private Process Start(params string[] arguments)
    {
        // Use the test runner's .NET host, with a fallback for SDKs outside PATH.
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ??
            Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..",
                OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        var start = new ProcessStartInfo(host)
        {
            WorkingDirectory = _directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Monitor", "PumpTelemetry.Monitor.dll"));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start)!;
    }

    private async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] arguments)
    {
        using var process = Start(arguments);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            // A broken exit path should fail the test instead of hanging the test run.
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            return (process.ExitCode, await output, await error);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
