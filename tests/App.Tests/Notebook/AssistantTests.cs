using App.Assistant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace App.Tests.Notebook;

public sealed class AssistantTests
{
    // ── BuildProcessStartInfo ────────────────────────────────────────────────

    [Fact]
    public void BuildProcessStartInfo_uses_configured_path()
    {
        var psi = ClaudeAssistant.BuildProcessStartInfo("my-claude", "/tmp/work");

        Assert.Equal("my-claude", psi.FileName);
        Assert.Equal("/tmp/work", psi.WorkingDirectory);
    }

    [Fact]
    public void BuildProcessStartInfo_redirects_all_streams()
    {
        var psi = ClaudeAssistant.BuildProcessStartInfo("claude", "/tmp");

        Assert.True(psi.RedirectStandardInput);
        Assert.True(psi.RedirectStandardOutput);
        Assert.True(psi.RedirectStandardError);
        Assert.False(psi.UseShellExecute);
    }

    [Fact]
    public void BuildProcessStartInfo_argument_list_is_correct()
    {
        var psi = ClaudeAssistant.BuildProcessStartInfo("claude", "/tmp");

        // Must use ArgumentList (not Arguments string) so "-p" cannot be
        // misinterpreted and a question starting with "--" cannot become a flag.
        var args = psi.ArgumentList;
        Assert.Contains("-p", args);
        Assert.Contains("--output-format", args);
        Assert.Contains("json", args);
        Assert.Contains("--tools", args);
        Assert.Contains("", args);            // empty tools list
        Assert.Contains("--strict-mcp-config", args);
        Assert.Contains("--no-session-persistence", args);
    }

    [Fact]
    public void BuildProcessStartInfo_no_Arguments_string()
    {
        var psi = ClaudeAssistant.BuildProcessStartInfo("claude", "/tmp");

        // If Arguments is set, the ArgumentList entries may be ignored.
        Assert.True(string.IsNullOrEmpty(psi.Arguments));
    }

    // ── ParseResponse ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseResponse_success_returns_answer()
    {
        var json = """{"result":"The sky is blue.","is_error":false}""";

        var result = ClaudeAssistant.ParseResponse(json);

        Assert.Equal("The sky is blue.", result.Answer);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ParseResponse_is_error_true_returns_error()
    {
        var json = """{"result":"Something went wrong.","is_error":true}""";

        var result = ClaudeAssistant.ParseResponse(json);

        Assert.Null(result.Answer);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ParseResponse_malformed_json_returns_error()
    {
        var result = ClaudeAssistant.ParseResponse("not json at all");

        Assert.Null(result.Answer);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ParseResponse_missing_result_field_returns_error()
    {
        var json = """{"is_error":false}""";

        var result = ClaudeAssistant.ParseResponse(json);

        Assert.Null(result.Answer);
        Assert.NotNull(result.Error);
    }

    // ── AskAsync failure paths ────────────────────────────────────────────────

    [Fact]
    public async Task AskAsync_nonexistent_path_returns_error_rather_than_throwing()
    {
        var nonexistentPath = "/nonexistent/claude-does-not-exist";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Assistant:ClaudePath"] = nonexistentPath })
            .Build();
        var assistant = new ClaudeAssistant(config, NullLogger<ClaudeAssistant>.Instance);

        var result = await assistant.AskAsync("Is the CLI missing?", CancellationToken.None);

        Assert.Null(result.Answer);
        Assert.NotNull(result.Error);
        Assert.Contains(nonexistentPath, result.Error);
    }
}
