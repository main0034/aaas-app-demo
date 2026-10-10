using System.Net;
using System.Net.Http.Json;
using App.Data;
using App.Tests.Postgres;

namespace App.Tests;

public sealed class ItemSummaryEndpointTests(TemplateDatabase template) : EndpointTest(template)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Summary(int Open, int Overdue, int DueSoon, int Done, Dictionary<string, int> ByPriority);

    private async Task<Summary> SummaryAsync(string url = "/items/summary") =>
        (await Client.GetFromJsonAsync<Summary>(url, Ct))!;

    [Fact]
    public async Task Empty_database_returns_all_zeros()
    {
        var s = await SummaryAsync();

        Assert.Equal(0, s.Open);
        Assert.Equal(0, s.Overdue);
        Assert.Equal(0, s.DueSoon);
        Assert.Equal(0, s.Done);
        Assert.Equal(new Dictionary<string, int> { ["1"] = 0, ["2"] = 0, ["3"] = 0, ["4"] = 0, ["5"] = 0, ["none"] = 0 }, s.ByPriority);
    }

    [Fact]
    public async Task Counts_open_and_done()
    {
        await SeedAsync(
            new Item { Title = "open-1" },
            new Item { Title = "open-2" },
            new Item { Title = "done-1", IsDone = true },
            new Item { Title = "done-2", IsDone = true });

        var s = await SummaryAsync();

        Assert.Equal(2, s.Open);
        Assert.Equal(2, s.Done);
    }

    [Fact]
    public async Task Overdue_counts_open_items_with_due_date_before_today()
    {
        await SeedAsync(
            new Item { Title = "overdue", DueDate = Today.AddDays(-1) },
            new Item { Title = "due-today", DueDate = Today },
            new Item { Title = "no-date" },
            new Item { Title = "done-overdue", DueDate = Today.AddDays(-1), IsDone = true });

        var s = await SummaryAsync();

        Assert.Equal(1, s.Overdue);
    }

    [Fact]
    public async Task DueSoon_includes_today_through_today_plus_7()
    {
        await SeedAsync(
            new Item { Title = "due-today", DueDate = Today },
            new Item { Title = "due-in-7", DueDate = Today.AddDays(7) },
            new Item { Title = "due-in-8", DueDate = Today.AddDays(8) },
            new Item { Title = "overdue", DueDate = Today.AddDays(-1) },
            new Item { Title = "no-date" });

        var s = await SummaryAsync();

        Assert.Equal(2, s.DueSoon);
        Assert.Equal(1, s.Overdue);
    }

    [Fact]
    public async Task ByPriority_all_keys_present_with_zeros_for_missing_priorities()
    {
        await SeedAsync(
            new Item { Title = "p1", Priority = 1 },
            new Item { Title = "p2-a", Priority = 2 },
            new Item { Title = "p2-b", Priority = 2 },
            new Item { Title = "none-a" },
            new Item { Title = "none-b" });

        var s = await SummaryAsync();

        Assert.Equal(1, s.ByPriority["1"]);
        Assert.Equal(2, s.ByPriority["2"]);
        Assert.Equal(0, s.ByPriority["3"]);
        Assert.Equal(0, s.ByPriority["4"]);
        Assert.Equal(0, s.ByPriority["5"]);
        Assert.Equal(2, s.ByPriority["none"]);
    }

    [Fact]
    public async Task ByPriority_excludes_done_items()
    {
        await SeedAsync(
            new Item { Title = "open-p1", Priority = 1 },
            new Item { Title = "done-p1", Priority = 1, IsDone = true });

        var s = await SummaryAsync();

        Assert.Equal(1, s.ByPriority["1"]);
    }

    [Fact]
    public async Task Q_narrows_all_counts_case_insensitively()
    {
        await SeedAsync(
            new Item { Title = "Fix Login", Priority = 1 },
            new Item { Title = "Fix Logout", IsDone = true },
            new Item { Title = "Write Docs", Priority = 2 });

        var s = await SummaryAsync("/items/summary?q=fix");

        Assert.Equal(1, s.Open);
        Assert.Equal(1, s.Done);
        Assert.Equal(1, s.ByPriority["1"]);
        Assert.Equal(0, s.ByPriority["2"]);
    }

    [Fact]
    public async Task Q_whitespace_only_means_no_narrowing()
    {
        await SeedAsync(
            new Item { Title = "a" },
            new Item { Title = "b" },
            new Item { Title = "c", IsDone = true });

        var s = await SummaryAsync("/items/summary?q=   ");

        Assert.Equal(2, s.Open);
        Assert.Equal(1, s.Done);
    }

    [Fact]
    public async Task Q_exactly_100_chars_is_allowed()
    {
        var q = new string('x', 100);
        var response = await Client.GetAsync($"/items/summary?q={q}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
