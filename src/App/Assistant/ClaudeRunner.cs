using System.Diagnostics;

namespace App.Assistant;

// Shared process runner used by ClaudeAssistant and ClaudeHandwritingReader.
// Handles: ClaudeLocator resolution, PATH setup so Node.js is found, stdin
// write, concurrent stdout/stderr reads, timeout with process-tree kill, and
// converting a failed process start into a readable error.
internal sealed class ClaudeRunner
{
    internal string ClaudePath { get; }
    private readonly int _timeoutSeconds;
    private readonly ILogger _logger;

    internal ClaudeRunner(IConfiguration config, ILogger logger)
    {
        ClaudePath = ClaudeLocator.Resolve(
            config["Assistant:ClaudePath"] ?? "claude",
            Environment.GetEnvironmentVariable("PATH"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            File.Exists);
        _timeoutSeconds = int.TryParse(config["Assistant:TimeoutSeconds"], out var t) ? t : 120;
        _logger = logger;
    }

    // Puts the claude CLI's own directory first on the child's PATH so its
    // Node.js interpreter resolves when the app is started from an IDE or
    // Dock with a minimal PATH.
    internal static void PrependClaudeDir(ProcessStartInfo psi, string claudePath)
    {
        var dir = Path.GetDirectoryName(claudePath);
        if (!string.IsNullOrEmpty(dir))
        {
            var inherited = psi.Environment.TryGetValue("PATH", out var p) ? p : null;
            psi.Environment["PATH"] = string.IsNullOrEmpty(inherited)
                ? dir
                : dir + Path.PathSeparator + inherited;
        }
    }

    // Starts psi, writes stdin, drains stderr concurrently, and waits for
    // exit. Returns (stdout, null) on success, or (null, errorMessage) on
    // timeout or process-start failure. Cancellation is re-thrown.
    internal async Task<(string? Stdout, string? Error)> RunAsync(
        ProcessStartInfo psi, string stdin, CancellationToken ct)
    {
        using var process = new Process { StartInfo = psi };
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        try
        {
            process.Start();

            await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

            await process.WaitForExitAsync(linkedCts.Token);
            var stdout = await stdoutTask;
            await stderrTask;
            return (stdout, null);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning("claude timed out after {Seconds} s", _timeoutSeconds);
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return (null, "The AI did not respond in time.");
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start '{ClaudePath}'", ClaudePath);
            return (null, $"The AI is not available: '{ClaudePath}' could not be started.");
        }
    }
}
