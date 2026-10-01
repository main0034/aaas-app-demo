using System.Net;
using System.Net.Http.Json;
using App.Data;
using App.Tests.Postgres;

namespace App.Tests;

public sealed class ItemEndpointTests(TemplateDatabase template) : EndpointTest(template)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<string[]> TitlesAsync(string url) =>
        (await Client.GetFromJsonAsync<List<Item>>(url, Ct))!.Select(i => i.Title).ToArray();

    [Fact]
    public async Task List_returns_newest_first()
    {
        await SeedAsync(new Item { Title = "first" }, new Item { Title = "second" }, new Item { Title = "third" });

        Assert.Equal(["third", "second", "first"], await TitlesAsync("/items"));
    }

    // The filter must run before Take: with the newest 100 items all done, the
    // open items are all older than the first page.
    [Fact]
    public async Task List_open_filters_before_taking_the_first_page()
    {
        await SeedAsync([.. Enumerable.Range(1, 150).Select(i => new Item { Title = $"item-{i:000}", IsDone = i > 50 })]);

        var titles = await TitlesAsync("/items?open=true");

        Assert.Equal(Enumerable.Range(1, 50).Reverse().Select(i => $"item-{i:000}"), titles);
    }

    [Fact]
    public async Task Search_matches_title_or_note_case_insensitively()
    {
        await SeedAsync(
            new Item { Title = "Fix the Login Bug" },
            new Item { Title = "Deploy to prod", Note = "needs hotFIX sign-off" },
            new Item { Title = "Write docs", Note = "no match here" });

        Assert.Equal(["Deploy to prod", "Fix the Login Bug"], await TitlesAsync("/items?q=fix"));
    }

    [Fact]
    public async Task Search_combined_with_open_excludes_done_matches()
    {
        await SeedAsync(
            new Item { Title = "Fix login" },
            new Item { Title = "Fix logout", IsDone = true },
            new Item { Title = "Deploy app" });

        Assert.Equal(["Fix login"], await TitlesAsync("/items?open=true&q=fix"));
    }

    [Fact]
    public async Task Overdue_returns_open_items_due_before_today_most_overdue_first()
    {
        await SeedAsync(
            new Item { Title = "due-yesterday", DueDate = Today.AddDays(-1) },
            new Item { Title = "very-overdue", DueDate = Today.AddDays(-30) },
            new Item { Title = "due-today", DueDate = Today },
            new Item { Title = "due-tomorrow", DueDate = Today.AddDays(1) },
            new Item { Title = "done-overdue", DueDate = Today.AddDays(-30), IsDone = true },
            new Item { Title = "no-due-date" });

        Assert.Equal(["very-overdue", "due-yesterday"], await TitlesAsync("/items/overdue"));
    }

    [Fact]
    public async Task Set_due_date_makes_an_item_overdue()
    {
        await SeedAsync(new Item { Title = "a" }, new Item { Title = "b" });
        var all = (await Client.GetFromJsonAsync<List<Item>>("/items", Ct))!;
        var b = all.Single(i => i.Title == "b");

        var response = await Client.PutAsJsonAsync($"/items/{b.Id}/due-date", new { date = Today.AddDays(-2) }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["b"], await TitlesAsync("/items/overdue"));
    }

    [Fact]
    public async Task Set_due_date_on_a_missing_item_is_404()
    {
        var response = await Client.PutAsJsonAsync("/items/999/due-date", new { date = Today }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Mark_done_removes_the_item_from_the_open_list()
    {
        await SeedAsync(new Item { Title = "keep" }, new Item { Title = "finish" });
        var finish = (await Client.GetFromJsonAsync<List<Item>>("/items", Ct))!.Single(i => i.Title == "finish");

        var response = await Client.PatchAsJsonAsync($"/items/{finish.Id}", new { done = true }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["keep"], await TitlesAsync("/items?open=true"));
    }
}
