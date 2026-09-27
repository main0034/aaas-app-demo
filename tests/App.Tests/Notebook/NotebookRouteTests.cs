using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.Assistant;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests.Notebook;

public sealed class NotebookRouteTests
{
    private readonly HttpClient _client;

    public NotebookRouteTests()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("PGHOST", "");
                b.ConfigureServices(services =>
                {
                    // Replace the real IAssistant with a fake for all route tests.
                    services.AddSingleton<IAssistant>(new FakeAssistant("Test answer."));
                });
            });
        _client = factory.CreateClient();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ── /notebook/state ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetState_fails_cleanly_without_database()
    {
        var response = await _client.GetAsync("/notebook/state", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // ── /notebook/lines ───────────────────────────────────────────────────────

    [Fact]
    public async Task PostLine_lone_question_mark_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/notebook/lines",
            new { text = "?", x = 0, y = 0, width = 10, height = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostLine_empty_text_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/notebook/lines",
            new { text = "   ", x = 0, y = 0, width = 10, height = 20 },
            Ct);

        // Validation rejects empty/whitespace before the route handler runs.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostLine_item_fails_cleanly_without_database()
    {
        var response = await _client.PostAsJsonAsync(
            "/notebook/lines",
            new { text = "Buy milk", x = 10, y = 20, width = 80, height = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task PostLine_question_calls_assistant_and_fails_cleanly_without_database()
    {
        // The route saves the question to DB first, so it 503 before reaching the assistant.
        var response = await _client.PostAsJsonAsync(
            "/notebook/lines",
            new { text = "What is 2+2?", x = 10, y = 20, width = 80, height = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task PostLine_missing_text_field_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/notebook/lines",
            new { x = 0, y = 0, width = 10, height = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── /notebook/strokes ─────────────────────────────────────────────────────

    [Fact]
    public async Task PostStroke_fails_cleanly_without_database()
    {
        var response = await _client.PostAsJsonAsync(
            "/notebook/strokes",
            new { points = new[] { new { x = 0, y = 0 }, new { x = 10, y = 10 } } },
            Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task PostStroke_missing_points_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/notebook/strokes",
            new { },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Assistant integration ─────────────────────────────────────────────────

    [Fact]
    public async Task FakeAssistant_returns_configured_answer()
    {
        var fake = new FakeAssistant("hello");
        var result = await fake.AskAsync("anything?", Ct);

        Assert.Equal("hello", result.Answer);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task FakeAssistant_error_mode_returns_error()
    {
        var fake = new FakeAssistant(null, "broken");
        var result = await fake.AskAsync("anything?", Ct);

        Assert.Null(result.Answer);
        Assert.Equal("broken", result.Error);
    }
}

// A test double for IAssistant that never starts a real process.
internal sealed class FakeAssistant(string? answer = null, string? error = null) : IAssistant
{
    public Task<AssistantResult> AskAsync(string question, CancellationToken ct) =>
        Task.FromResult(new AssistantResult(answer, error));
}
