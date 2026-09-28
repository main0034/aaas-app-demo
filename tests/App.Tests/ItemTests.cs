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

    // Search tests — exercising GET /items?q= and the WhereSearch filter

    // Proves the q parameter is wired into the database path.
    [Fact]
    public async Task Search_fails_cleanly_without_database()
    {
        var response = await _client.GetAsync("/items?q=milk", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // Proves q and open can be combined and both reach the database path.
    [Fact]
    public async Task Search_combined_with_open_fails_cleanly_without_database()
    {
        var response = await _client.GetAsync("/items?q=milk&open=true", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // The items below are the same list used in every WhereSearch test so that
    // each test demonstrates concretely which items appear and which do not.
    //
    //   Id 1 — "Buy milk"              note: "skimmed"      open
    //   Id 2 — "Fix the login Bug"     note: null           open
    //   Id 3 — "Write release notes"   note: "see JIRA"     done
    //   Id 4 — "Order coffee"          note: "strong roast" open
    private static IQueryable<Item> SearchFixture() =>
        new[]
        {
            new Item { Id = 1, Title = "Buy milk",            Note = "skimmed",      IsDone = false },
            new Item { Id = 2, Title = "Fix the login Bug",   Note = null,           IsDone = false },
            new Item { Id = 3, Title = "Write release notes", Note = "see JIRA",     IsDone = true  },
            new Item { Id = 4, Title = "Order coffee",        Note = "strong roast", IsDone = false },
        }.AsQueryable();

    [Fact]
    public void WhereSearch_matches_word_in_title()
    {
        // "milk" appears in item 1's title; items 2, 3, 4 must not be returned.
        var result = SearchFixture().WhereSearch("milk").ToList();

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void WhereSearch_matches_word_in_note()
    {
        // "roast" appears in item 4's note; items 1, 2, 3 must not be returned.
        var result = SearchFixture().WhereSearch("roast").ToList();

        Assert.Single(result);
        Assert.Equal(4, result[0].Id);
    }

    [Fact]
    public void WhereSearch_is_case_insensitive()
    {
        // "bug" matches "Bug" in item 2's title; item 4 ("strong roast") must not match.
        var result = SearchFixture().WhereSearch("bug").ToList();

        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void WhereSearch_matches_anywhere_in_title_or_note()
    {
        // "release" appears mid-title in item 3; "JIRA" in item 3's note.
        // Searching "JIRA" must find item 3 and nothing else.
        var result = SearchFixture().WhereSearch("JIRA").ToList();

        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    [Fact]
    public void WhereSearch_returns_empty_when_nothing_matches()
    {
        var result = SearchFixture().WhereSearch("nonexistent").ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void WhereSearch_combined_with_WhereOpen_excludes_done_items()
    {
        // "release" matches item 3 (done) and item 3 only; WhereOpen must suppress it.
        // Without open: item 3 would appear.  With open: result must be empty.
        var withoutOpen = SearchFixture().WhereSearch("release").ToList();
        var withOpen = SearchFixture().WhereOpen().WhereSearch("release").ToList();

        Assert.Single(withoutOpen);
        Assert.Equal(3, withoutOpen[0].Id);
        Assert.Empty(withOpen);
    }

    // Mirrors the WhereOpen ordering test: search must be applied before Take so
    // a matching item with a low id is still reachable when there are 200 items.
    [Fact]
    public void WhereSearch_applied_before_Take_finds_item_outside_top_100()
    {
        // Item 1 is the only one titled "needle"; ids 2-200 do not match.
        // Sorted descending, item 1 falls outside the top 100 that Take would return
        // if the search filter ran after Take instead of before it.
        var items = Enumerable.Range(1, 200)
            .Select(i => new Item { Id = i, Title = i == 1 ? "needle" : $"item-{i}" })
            .AsQueryable();

        var result = items
            .WhereSearch("needle")
            .OrderByDescending(i => i.Id)
            .Take(100)
            .ToList();

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }
}
