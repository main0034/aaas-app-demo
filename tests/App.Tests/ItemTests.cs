using System.Net;
using System.Net.Http.Json;
using App.Data;
using Microsoft.AspNetCore.Mvc.Testing;

namespace App.Tests;

public sealed class ItemTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory
        .WithWebHostBuilder(b => b.UseSetting("PGHOST", ""))
        .CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListItems_open_filter_fails_cleanly_without_database()
    {
        var response = await _client.GetAsync("/items?open=true", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task MarkDone_fails_cleanly_without_database()
    {
        var response = await _client.PatchAsJsonAsync("/items/1", new { done = true }, Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task MarkDone_rejects_missing_done_field()
    {
        // Validation runs before the database is touched.
        var response = await _client.PatchAsJsonAsync("/items/1", new { }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // The filter must run before Take, not after. With 200 items where the most
    // recent 100 are all done, applying Take first and filtering second returns
    // zero open items. The correct order returns all 100 open items.
    [Fact]
    public void WhereOpen_applied_before_Take_returns_open_items_outside_top_100()
    {
        // Items 1-100: open. Items 101-200: done.
        // Sorted descending by id, the top 100 are all done (ids 200→101).
        var items = Enumerable.Range(1, 200)
            .Select(i => new Item { Id = i, Title = $"item-{i}", IsDone = i > 100 })
            .AsQueryable();

        var result = items
            .WhereOpen()
            .OrderByDescending(i => i.Id)
            .Take(100)
            .ToList();

        Assert.Equal(100, result.Count);
        Assert.All(result, i => Assert.False(i.IsDone));
    }

    [Fact]
    public void WhereOpen_excludes_done_items()
    {
        var items = new[]
        {
            new Item { Id = 1, Title = "open", IsDone = false },
            new Item { Id = 2, Title = "done", IsDone = true },
        }.AsQueryable();

        var result = items.WhereOpen().ToList();

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    // Search tests must show which specific items are returned — not just that the
    // route exists — to avoid the class of bug where a filter is wired incorrectly
    // and all items come back (or none).

    [Fact]
    public void WhereSearch_returns_title_and_note_matches_case_insensitively()
    {
        var items = new[]
        {
            new Item { Id = 1, Title = "Fix the Login Bug", Note = null },
            new Item { Id = 2, Title = "Deploy to prod", Note = "needs hotfix sign-off" },
            new Item { Id = 3, Title = "Write docs", Note = null },
        }.AsQueryable();

        var result = items.WhereSearch("fix").OrderBy(i => i.Id).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Id); // "Fix" appears in title
        Assert.Equal(2, result[1].Id); // "fix" appears in note
        // Id 3 is absent: neither title nor note contains "fix"
    }

    [Fact]
    public void WhereSearch_combined_with_WhereOpen_excludes_done_matches()
    {
        var items = new[]
        {
            new Item { Id = 1, Title = "Fix login", IsDone = false },
            new Item { Id = 2, Title = "Fix logout", IsDone = true },
            new Item { Id = 3, Title = "Deploy app", IsDone = false },
        }.AsQueryable();

        var result = items.WhereSearch("fix").WhereOpen().ToList();

        Assert.Single(result);
        Assert.Equal(1, result[0].Id); // open + matches; Id 2 is done, Id 3 does not match
    }

    [Fact]
    public async Task ListItems_search_fails_cleanly_without_database()
    {
        var response = await _client.GetAsync("/items?q=fix", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
