using System.Diagnostics;
using System.Text.Json;

namespace App.Assistant;

// Sends a question to the local `claude` CLI and returns its answer.
// The question is written to stdin so that a question beginning with "--"
// cannot be interpreted as a flag.
public sealed class ClaudeAssistant(IConfiguration config, ILogger<ClaudeAssistant> logger)
    : IAssistant
{
    private readonly string _claudePath = config["Assistant:ClaudePath"] ?? "claude";
    private readonly int _timeoutSeconds = int.TryParse(config["Assistant:TimeoutSeconds"], out var t) ? t : 120;

    public async Task<AssistantResult> AskAsync(string question, CancellationToken ct)
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);
        try
        {
            return await RunAsync(question, tmpDir, ct);
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private async Task<AssistantResult> RunAsync(string question, string workingDir, CancellationToken ct)
    {
        var psi = BuildProcessStartInfo(_claudePath, workingDir);

        using var process = new Process { StartInfo = psi };
        // Read stdout and stderr concurrently; an unread stderr pipe stalls the child.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            process.Start();

            await process.StandardInput.WriteAsync(question);
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

            await process.WaitForExitAsync(linkedCts.Token);
            var stdout = await stdoutTask;
            await stderrTask;
            return ParseResponse(stdout);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            logger.LogWarning("claude timed out after {Seconds} s", _timeoutSeconds);
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return new AssistantResult(null, "The AI did not respond in time.");
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start '{ClaudePath}'", _claudePath);
            return new AssistantResult(null, $"The AI is not available: '{_claudePath}' could not be started.");
        }
    }

    // Exposed for unit testing without starting a real process.
    public static ProcessStartInfo BuildProcessStartInfo(string claudePath, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = claudePath,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("json");
        psi.ArgumentList.Add("--tools");
        psi.ArgumentList.Add("");
        psi.ArgumentList.Add("--strict-mcp-config");
        psi.ArgumentList.Add("--no-session-persistence");
        return psi;
    }

    // Exposed for unit testing.
    public static AssistantResult ParseResponse(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("is_error", out var isError) && isError.GetBoolean())
            {
                var msg = root.TryGetProperty("result", out var r) ? r.GetString() : null;
                return new AssistantResult(null, msg ?? "The AI reported an error.");
            }

            if (root.TryGetProperty("result", out var result))
            {
                return new AssistantResult(result.GetString(), null);
            }

            return new AssistantResult(null, "Unexpected response from AI.");
        }
        catch (JsonException)
        {
            return new AssistantResult(null, "Could not read the AI response.");
        }
    }
}
