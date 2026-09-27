using System.Text.Json;
using App.Assistant;

namespace App.Tests.Notebook;

public sealed class HandwritingReaderTests
{
    // ── BuildProcessStartInfo ────────────────────────────────────────────────

    [Fact]
    public void BuildProcessStartInfo_uses_configured_path()
    {
        var psi = ClaudeHandwritingReader.BuildProcessStartInfo("my-claude", "/tmp/work");

        Assert.Equal("my-claude", psi.FileName);
        Assert.Equal("/tmp/work", psi.WorkingDirectory);
    }

    [Fact]
    public void BuildProcessStartInfo_redirects_all_streams()
    {
        var psi = ClaudeHandwritingReader.BuildProcessStartInfo("claude", "/tmp");

        Assert.True(psi.RedirectStandardInput);
        Assert.True(psi.RedirectStandardOutput);
        Assert.True(psi.RedirectStandardError);
        Assert.False(psi.UseShellExecute);
    }

    [Fact]
    public void BuildProcessStartInfo_argument_list_has_stream_json_formats_and_verbose()
    {
        var psi = ClaudeHandwritingReader.BuildProcessStartInfo("claude", "/tmp");
        var args = psi.ArgumentList;

        Assert.Contains("-p", args);
        Assert.Contains("--input-format", args);
        Assert.Contains("stream-json", args);
        Assert.Contains("--output-format", args);
        Assert.Contains("--verbose", args);
        Assert.Contains("--tools", args);
        Assert.Contains("", args);              // empty tools list
        Assert.Contains("--strict-mcp-config", args);
        Assert.Contains("--no-session-persistence", args);
    }

    [Fact]
    public void BuildProcessStartInfo_output_format_follows_stream_json()
    {
        var psi = ClaudeHandwritingReader.BuildProcessStartInfo("claude", "/tmp");
        var args = psi.ArgumentList.ToList();

        // "--output-format" must be followed by "stream-json" (not "json").
        var idx = args.IndexOf("--output-format");
        Assert.True(idx >= 0 && idx + 1 < args.Count);
        Assert.Equal("stream-json", args[idx + 1]);
    }

    [Fact]
    public void BuildProcessStartInfo_no_Arguments_string()
    {
        var psi = ClaudeHandwritingReader.BuildProcessStartInfo("claude", "/tmp");

        Assert.True(string.IsNullOrEmpty(psi.Arguments));
    }

    // ── BuildStdin ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildStdin_is_single_line()
    {
        var stdin = ClaudeHandwritingReader.BuildStdin("abc123");

        Assert.DoesNotContain('\n', stdin);
    }

    [Fact]
    public void BuildStdin_has_user_type_and_role()
    {
        var stdin = ClaudeHandwritingReader.BuildStdin("abc123");
        using var doc = JsonDocument.Parse(stdin);
        var root = doc.RootElement;

        Assert.Equal("user", root.GetProperty("type").GetString());
        Assert.Equal("user", root.GetProperty("message").GetProperty("role").GetString());
    }

    [Fact]
    public void BuildStdin_image_block_has_correct_structure()
    {
        const string b64 = "iVBORw0KGgoAAAANSUhEUg==";
        var stdin = ClaudeHandwritingReader.BuildStdin(b64);
        using var doc = JsonDocument.Parse(stdin);
        var content = doc.RootElement
            .GetProperty("message")
            .GetProperty("content");

        var imageBlock = content[0];
        Assert.Equal("image", imageBlock.GetProperty("type").GetString());

        var source = imageBlock.GetProperty("source");
        Assert.Equal("base64", source.GetProperty("type").GetString());
        Assert.Equal("image/png", source.GetProperty("media_type").GetString());
        Assert.Equal(b64, source.GetProperty("data").GetString());
    }

    [Fact]
    public void BuildStdin_text_block_asks_to_transcribe()
    {
        var stdin = ClaudeHandwritingReader.BuildStdin("abc");
        using var doc = JsonDocument.Parse(stdin);
        var content = doc.RootElement
            .GetProperty("message")
            .GetProperty("content");

        var textBlock = content[1];
        Assert.Equal("text", textBlock.GetProperty("type").GetString());
        Assert.Equal("Transcribe the handwriting in this image.", textBlock.GetProperty("text").GetString());
    }

    [Fact]
    public void BuildStdin_base64_is_preserved_verbatim()
    {
        const string b64 = "ABC+/123==";
        var stdin = ClaudeHandwritingReader.BuildStdin(b64);
        using var doc = JsonDocument.Parse(stdin);
        var data = doc.RootElement
            .GetProperty("message")
            .GetProperty("content")[0]
            .GetProperty("source")
            .GetProperty("data")
            .GetString();

        Assert.Equal(b64, data);
    }

    // ── ParseResponse ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseResponse_finds_result_among_other_lines()
    {
        var lines = """
            {"type":"system","subtype":"init","session_id":"s1"}
            {"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"Buy milk"}]}}
            {"type":"result","subtype":"success","is_error":false,"result":"Buy milk"}
            """;

        var result = ClaudeHandwritingReader.ParseResponse(lines);

        Assert.Equal("Buy milk", result.Text);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ParseResponse_is_error_true_returns_error()
    {
        var lines = """
            {"type":"result","subtype":"error","is_error":true,"result":"Something went wrong."}
            """;

        var result = ClaudeHandwritingReader.ParseResponse(lines);

        Assert.Null(result.Text);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ParseResponse_no_result_line_returns_error()
    {
        var lines = """
            {"type":"system","subtype":"init"}
            {"type":"assistant","message":{}}
            """;

        var result = ClaudeHandwritingReader.ParseResponse(lines);

        Assert.Null(result.Text);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ParseResponse_non_json_lines_are_skipped()
    {
        var lines = "not json at all\n{\"type\":\"result\",\"is_error\":false,\"result\":\"Hello\"}";

        var result = ClaudeHandwritingReader.ParseResponse(lines);

        Assert.Equal("Hello", result.Text);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ParseResponse_empty_result_string_means_nothing_legible()
    {
        var lines = """{"type":"result","is_error":false,"result":""}""";

        var result = ClaudeHandwritingReader.ParseResponse(lines);

        // Empty string is returned as Text="" so callers can distinguish
        // "nothing legible" from an error.
        Assert.Equal("", result.Text);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ParseResponse_null_result_value_treated_as_nothing_legible()
    {
        var lines = """{"type":"result","is_error":false,"result":null}""";

        var result = ClaudeHandwritingReader.ParseResponse(lines);

        Assert.Equal("", result.Text);
        Assert.Null(result.Error);
    }
}
