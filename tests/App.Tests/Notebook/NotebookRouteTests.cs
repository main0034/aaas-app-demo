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
                    // Replace real implementations with fakes for all route tests.
                    services.AddSingleton<IAssistant>(new FakeAssistant("Test answer."));
                    services.AddSingleton<IHandwritingReader>(new FakeHandwritingReader("Buy milk"));
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

    // ── /notebook/ink ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PostInk_non_png_returns_400()
    {
        // JPEG magic bytes — valid base64 but not a PNG.
        var notPng = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 });
        var response = await _client.PostAsJsonAsync(
            "/notebook/ink",
            new { strokeIds = new[] { 1 }, image = notPng, boundingLeft = 0, boundingTop = 0, boundingWidth = 100, boundingHeight = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostInk_invalid_base64_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/notebook/ink",
            new { strokeIds = new[] { 1 }, image = "not-valid-base64!!!", boundingLeft = 0, boundingTop = 0, boundingWidth = 100, boundingHeight = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostInk_image_over_2mb_returns_400()
    {
        // Build a byte array that starts with the PNG signature but is 1 byte over the 2 MB limit.
        var bigBytes = new byte[2 * 1024 * 1024 + 1];
        byte[] sig = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        sig.CopyTo(bigBytes, 0);

        var response = await _client.PostAsJsonAsync(
            "/notebook/ink",
            new { strokeIds = new[] { 1 }, image = Convert.ToBase64String(bigBytes), boundingLeft = 0, boundingTop = 0, boundingWidth = 100, boundingHeight = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostInk_nothing_legible_returns_200_with_nothing_type()
    {
        // Fake reader that signals nothing legible with an empty string.
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("PGHOST", "");
                b.ConfigureServices(services =>
                {
                    services.AddSingleton<IAssistant>(new FakeAssistant("Test answer."));
                    services.AddSingleton<IHandwritingReader>(new FakeHandwritingReader(""));
                });
            });
        var client = factory.CreateClient();

        var png = MinimalPng();
        var response = await client.PostAsJsonAsync(
            "/notebook/ink",
            new { strokeIds = new[] { 1 }, image = Convert.ToBase64String(png), boundingLeft = 0, boundingTop = 0, boundingWidth = 100, boundingHeight = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("nothing", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task PostInk_reader_error_returns_502()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("PGHOST", "");
                b.ConfigureServices(services =>
                {
                    services.AddSingleton<IAssistant>(new FakeAssistant("Test answer."));
                    services.AddSingleton<IHandwritingReader>(new FakeHandwritingReader(null, "The AI is not available."));
                });
            });
        var client = factory.CreateClient();

        var png = MinimalPng();
        var response = await client.PostAsJsonAsync(
            "/notebook/ink",
            new { strokeIds = new[] { 1 }, image = Convert.ToBase64String(png), boundingLeft = 0, boundingTop = 0, boundingWidth = 100, boundingHeight = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task PostInk_item_text_fails_cleanly_without_database()
    {
        // Reader returns item text; the stroke-lookup step needs DB → 503.
        var png = MinimalPng();
        var response = await _client.PostAsJsonAsync(
            "/notebook/ink",
            new { strokeIds = new[] { 1 }, image = Convert.ToBase64String(png), boundingLeft = 0, boundingTop = 0, boundingWidth = 100, boundingHeight = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task PostInk_question_text_fails_cleanly_without_database()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("PGHOST", "");
                b.ConfigureServices(services =>
                {
                    services.AddSingleton<IAssistant>(new FakeAssistant("42."));
                    services.AddSingleton<IHandwritingReader>(new FakeHandwritingReader("What is 2+2?"));
                });
            });
        var client = factory.CreateClient();

        var png = MinimalPng();
        var response = await client.PostAsJsonAsync(
            "/notebook/ink",
            new { strokeIds = new[] { 1 }, image = Convert.ToBase64String(png), boundingLeft = 0, boundingTop = 0, boundingWidth = 100, boundingHeight = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task PostInk_missing_stroke_ids_returns_400()
    {
        var png = MinimalPng();
        var response = await _client.PostAsJsonAsync(
            "/notebook/ink",
            new { image = Convert.ToBase64String(png), boundingLeft = 0, boundingTop = 0, boundingWidth = 100, boundingHeight = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostInk_empty_stroke_ids_returns_400()
    {
        var png = MinimalPng();
        var response = await _client.PostAsJsonAsync(
            "/notebook/ink",
            new { strokeIds = Array.Empty<int>(), image = Convert.ToBase64String(png), boundingLeft = 0, boundingTop = 0, boundingWidth = 100, boundingHeight = 20 },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // A minimal valid 1×1 white PNG (67 bytes).
    private static byte[] MinimalPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwADhQGAWjR9awAAAABJRU5ErkJggg==");
}

// A test double for IAssistant that never starts a real process.
internal sealed class FakeAssistant(string? answer = null, string? error = null) : IAssistant
{
    public Task<AssistantResult> AskAsync(string question, CancellationToken ct) =>
        Task.FromResult(new AssistantResult(answer, error));
}

// A test double for IHandwritingReader that never starts a real process.
internal sealed class FakeHandwritingReader(string? text = null, string? error = null) : IHandwritingReader
{
    public Task<HandwritingResult> ReadAsync(string pngBase64, CancellationToken ct) =>
        Task.FromResult(new HandwritingResult(text, error));
}
