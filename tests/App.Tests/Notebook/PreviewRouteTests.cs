using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.Assistant;
using App.Data;
using App.Notebook;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.Tests.Notebook;

public sealed class PreviewRouteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // A minimal valid 1×1 white PNG (67 bytes).
    private static byte[] MinimalPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwADhQGAWjR9awAAAABJRU5ErkJggg==");

    private static HttpClient BuildClient(IHandwritingReader reader, IAssistant? assistant = null)
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("PGHOST", "");
                b.ConfigureServices(services =>
                {
                    services.AddSingleton(reader);
                    services.AddSingleton<IAssistant>(assistant ?? new FakeAssistant("answer"));
                });
            });
        return factory.CreateClient();
    }

    // ── /notebook/preview — PNG validation (mirrors /notebook/ink) ────────────

    [Fact]
    public async Task PostPreview_non_png_returns_400()
    {
        var client = BuildClient(new FakeHandwritingReader("Buy milk"));
        var notPng = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

        var response = await client.PostAsJsonAsync(
            "/notebook/preview",
            new { image = notPng },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPreview_invalid_base64_returns_400()
    {
        var client = BuildClient(new FakeHandwritingReader("Buy milk"));

        var response = await client.PostAsJsonAsync(
            "/notebook/preview",
            new { image = "not-valid-base64!!!" },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPreview_image_over_2mb_returns_400()
    {
        var client = BuildClient(new FakeHandwritingReader("Buy milk"));
        var big = new byte[2 * 1024 * 1024 + 1];
        byte[] sig = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        sig.CopyTo(big, 0);

        var response = await client.PostAsJsonAsync(
            "/notebook/preview",
            new { image = Convert.ToBase64String(big) },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── /notebook/preview — behaviour ─────────────────────────────────────────

    [Fact]
    public async Task PostPreview_returns_recognised_text()
    {
        var client = BuildClient(new FakeHandwritingReader("Buy milk"));
        var png = MinimalPng();

        var response = await client.PostAsJsonAsync(
            "/notebook/preview",
            new { image = Convert.ToBase64String(png) },
            Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Buy milk", body.GetProperty("text").GetString());
        Assert.True(body.GetProperty("error").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task PostPreview_nothing_legible_returns_null_text_no_error()
    {
        var client = BuildClient(new FakeHandwritingReader(""));
        var png = MinimalPng();

        var response = await client.PostAsJsonAsync(
            "/notebook/preview",
            new { image = Convert.ToBase64String(png) },
            Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(body.GetProperty("text").ValueKind == JsonValueKind.Null);
        Assert.True(body.GetProperty("error").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task PostPreview_reader_failure_returns_error_field_not_502()
    {
        var client = BuildClient(new FakeHandwritingReader(null, "reader broke"));
        var png = MinimalPng();

        var response = await client.PostAsJsonAsync(
            "/notebook/preview",
            new { image = Convert.ToBase64String(png) },
            Ct);

        // Preview errors are surfaced in-line (200 with error field), not as HTTP errors.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.NotNull(body.GetProperty("error").GetString());
        Assert.True(body.GetProperty("text").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task PostPreview_creates_no_item_question_and_does_not_call_assistant()
    {
        var trackingAssistant = new TrackingAssistant();
        var client = BuildClient(new FakeHandwritingReader("Buy milk"), trackingAssistant);
        var png = MinimalPng();

        var response = await client.PostAsJsonAsync(
            "/notebook/preview",
            new { image = Convert.ToBase64String(png) },
            Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, trackingAssistant.CallCount);
    }

    // ── Cache deduplication ───────────────────────────────────────────────────

    [Fact]
    public async Task Preview_then_ink_same_png_calls_reader_once()
    {
        // Both routes share the PreviewCache; same PNG bytes → single reader call.
        var counting = new CountingHandwritingReader("Buy milk");
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("PGHOST", "");
                b.ConfigureServices(services =>
                {
                    services.AddSingleton<IHandwritingReader>(counting);
                    services.AddSingleton<IAssistant>(new FakeAssistant("answer"));
                });
            });
        var client = factory.CreateClient();
        var b64 = Convert.ToBase64String(MinimalPng());

        // First call: preview route populates the cache.
        await client.PostAsJsonAsync("/notebook/preview", new { image = b64 }, Ct);

        // Second call: ink route hits the cache (DB absent → 503, but reader NOT called again).
        await client.PostAsJsonAsync(
            "/notebook/ink",
            new { strokeIds = new[] { 1 }, image = b64, boundingLeft = 0, boundingTop = 0, boundingWidth = 100, boundingHeight = 20 },
            Ct);

        Assert.Equal(1, counting.CallCount);
    }

    [Fact]
    public async Task Preview_then_ink_different_png_calls_reader_twice()
    {
        var counting = new CountingHandwritingReader("Buy milk");
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("PGHOST", "");
                b.ConfigureServices(services =>
                {
                    services.AddSingleton<IHandwritingReader>(counting);
                    services.AddSingleton<IAssistant>(new FakeAssistant("answer"));
                });
            });
        var client = factory.CreateClient();

        // PNG 1: fill signature + extra byte 0x00.
        var png1 = new byte[9];
        byte[] sig = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        sig.CopyTo(png1, 0);
        png1[8] = 0x00;

        // PNG 2: signature + extra byte 0xFF (different content).
        var png2 = new byte[9];
        sig.CopyTo(png2, 0);
        png2[8] = 0xFF;

        await client.PostAsJsonAsync("/notebook/preview", new { image = Convert.ToBase64String(png1) }, Ct);
        await client.PostAsJsonAsync(
            "/notebook/ink",
            new { strokeIds = new[] { 1 }, image = Convert.ToBase64String(png2), boundingLeft = 0, boundingTop = 0, boundingWidth = 100, boundingHeight = 20 },
            Ct);

        Assert.Equal(2, counting.CallCount);
    }

    // ── PreviewCache unit tests ────────────────────────────────────────────────

    [Fact]
    public void PreviewCache_hit_within_ttl()
    {
        var clock = new TestTimeProvider(DateTimeOffset.UtcNow);
        var cache = new PreviewCache(clock);
        var result = new HandwritingResult("Buy milk", null);

        cache.Set("key1", result);
        clock.Advance(PreviewCache.Ttl - TimeSpan.FromSeconds(1));

        var hit = cache.TryGet("key1");
        Assert.NotNull(hit);
        Assert.Equal("Buy milk", hit.Text);
    }

    [Fact]
    public void PreviewCache_miss_after_expiry()
    {
        var clock = new TestTimeProvider(DateTimeOffset.UtcNow);
        var cache = new PreviewCache(clock);
        cache.Set("key1", new HandwritingResult("Buy milk", null));

        clock.Advance(PreviewCache.Ttl + TimeSpan.FromSeconds(1));

        Assert.Null(cache.TryGet("key1"));
    }

    [Fact]
    public void PreviewCache_nothing_legible_result_is_cached_and_returned()
    {
        var clock = new TestTimeProvider(DateTimeOffset.UtcNow);
        var cache = new PreviewCache(clock);
        var nothing = new HandwritingResult("", null);

        cache.Set("k", nothing);
        var hit = cache.TryGet("k");

        Assert.NotNull(hit);
        Assert.Equal("", hit.Text);
    }

    [Fact]
    public void PreviewCache_evicts_oldest_when_full()
    {
        var clock = new TestTimeProvider(DateTimeOffset.UtcNow);
        var cache = new PreviewCache(clock);
        var result = new HandwritingResult("x", null);

        // Fill to capacity.
        for (var i = 0; i < PreviewCache.Capacity; i++)
        {
            cache.Set($"key{i}", result);
        }
        Assert.Equal(PreviewCache.Capacity, cache.Count);

        // One more evicts the first entry.
        cache.Set("key_new", result);
        Assert.Equal(PreviewCache.Capacity, cache.Count);
        Assert.Null(cache.TryGet("key0"));
        Assert.NotNull(cache.TryGet("key_new"));
    }

    [Fact]
    public void PreviewCache_update_existing_key_does_not_grow_count()
    {
        var clock = new TestTimeProvider(DateTimeOffset.UtcNow);
        var cache = new PreviewCache(clock);

        cache.Set("k", new HandwritingResult("first", null));
        cache.Set("k", new HandwritingResult("second", null));

        Assert.Equal(1, cache.Count);
        Assert.Equal("second", cache.TryGet("k")!.Text);
    }

    // ── Stroke rejection by the read route ────────────────────────────────────
    // Tests the server half of the bug fix: a stroke that ticked an item
    // (ReadAt set) is rejected by /notebook/ink with 400.

    [Fact]
    public async Task PostInk_rejects_stroke_that_ticked_an_item()
    {
        var dbName = Guid.NewGuid().ToString();
        var inMemoryOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // Seed the in-memory store: a stroke with ReadAt set (ticked an item).
        int strokeId;
        await using (var seedCtx = new AppDbContext(inMemoryOptions))
        {
            var stroke = new Stroke
            {
                Points = [new StrokePoint(0, 0), new StrokePoint(10, 10)],
                ReadAt = DateTimeOffset.UtcNow,
            };
            seedCtx.Strokes.Add(stroke);
            await seedCtx.SaveChangesAsync(Ct);
            strokeId = stroke.Id;
        }

        // Replace AppDbContext in DI so the route uses the same in-memory store.
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("PGHOST", "");
                b.ConfigureServices(services =>
                {
                    // Replace the scoped AppDbContext with one backed by InMemory.
                    // Using Replace avoids the "two providers registered" EF Core error.
                    services.Replace(new ServiceDescriptor(
                        typeof(AppDbContext),
                        _ => new AppDbContext(inMemoryOptions),
                        ServiceLifetime.Scoped));

                    services.AddSingleton<IHandwritingReader>(new FakeHandwritingReader("Buy milk"));
                    services.AddSingleton<IAssistant>(new FakeAssistant("answer"));
                });
            });

        var client = factory.CreateClient();
        var png = MinimalPng();

        var response = await client.PostAsJsonAsync(
            "/notebook/ink",
            new
            {
                strokeIds = new[] { strokeId },
                image = Convert.ToBase64String(png),
                boundingLeft = 0,
                boundingTop = 0,
                boundingWidth = 100,
                boundingHeight = 20,
            },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Contains("already been read", body.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }
}

// Counts how many times ReadAsync is called.
internal sealed class CountingHandwritingReader(string? text = null, string? error = null) : IHandwritingReader
{
    public int CallCount { get; private set; }

    public Task<HandwritingResult> ReadAsync(string pngBase64, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(new HandwritingResult(text, error));
    }
}

// Counts how many times AskAsync is called.
internal sealed class TrackingAssistant : IAssistant
{
    public int CallCount { get; private set; }

    public Task<AssistantResult> AskAsync(string question, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(new AssistantResult("answer", null));
    }
}

// Controllable time source for cache expiry tests.
internal sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan offset) => _now += offset;
}
