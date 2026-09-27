using System.Diagnostics;
using System.Text.Json;

namespace App.Assistant;

// Sends a question to the local `claude` CLI and returns its answer.
// The question is written to stdin so that a question beginning with "--"
// cannot be interpreted as a flag.
public sealed class ClaudeAssistant : IAssistant
{
    private readonly ClaudeRunner _runner;

    public ClaudeAssistant(IConfiguration config, ILogger<ClaudeAssistant> logger)
    {
        _runner = new ClaudeRunner(config, logger);
    }

    public async Task<AssistantResult> AskAsync(string question, CancellationToken ct)
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);
        try
        {
            var psi = BuildProcessStartInfo(_runner.ClaudePath, tmpDir);
            var (stdout, error) = await _runner.RunAsync(psi, question, ct);
            if (error != null)
            {
                return new AssistantResult(null, error);
            }

            return ParseResponse(stdout!);
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { /* best effort */ }
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

        // An npm or Homebrew install of `claude` is a Node script (#!/usr/bin/env node)
        // with `node` beside it. Started from an IDE with a minimal PATH, the script
        // is found (see ClaudeLocator) but node is not. Put the CLI's own directory
        // first on the child's PATH so its interpreter resolves too.
        ClaudeRunner.PrependClaudeDir(psi, claudePath);

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
