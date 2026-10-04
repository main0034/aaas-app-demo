using System.Net.Http.Json;
using App.Data;
using App.Tests.Postgres;

namespace App.Tests;

public sealed class ItemUpcomingEndpointTests(TemplateDatabase template) : EndpointTest(template)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<string[]> TitlesAsync(string url) =>
        (await Client.GetFromJsonAsync<List<Item>>(url, Ct))!.Select(i => i.Title).ToArray();

    [Fact]
    public async Task Upcoming_returns_open_items_due_today_through_window()
    {
        await SeedAsync(
            new Item { Title = "yesterday", DueDate = Today.AddDays(-1) },
            new Item { Title = "today", DueDate = Today },
            new Item { Title = "in-3-days", DueDate = Today.AddDays(3) },
            new Item { Title = "in-7-days", DueDate = Today.AddDays(7) },
            new Item { Title = "in-8-days", DueDate = Today.AddDays(8) },
            new Item { Title = "no-due-date" },
            new Item { Title = "done-today", DueDate = Today, IsDone = true });

        Assert.Equal(
            ["today", "in-3-days", "in-7-days"],
            await TitlesAsync("/items/upcoming?days=7"));
    }

    [Fact]
    public async Task Upcoming_defaults_to_7_days()
    {
        await SeedAsync(
            new Item { Title = "in-7-days", DueDate = Today.AddDays(7) },
            new Item { Title = "in-8-days", DueDate = Today.AddDays(8) });

        Assert.Equal(["in-7-days"], await TitlesAsync("/items/upcoming"));
    }

    [Fact]
    public async Task Upcoming_days_0_returns_only_today()
    {
        await SeedAsync(
            new Item { Title = "today", DueDate = Today },
            new Item { Title = "tomorrow", DueDate = Today.AddDays(1) });

        Assert.Equal(["today"], await TitlesAsync("/items/upcoming?days=0"));
    }

    [Fact]
    public async Task Upcoming_orders_soonest_first_then_by_id()
    {
        // Seed in reverse order so IDs are higher for earlier-added items by design.
        // Two items on same day: the one inserted first (lower Id) comes first.
        await SeedAsync(
            new Item { Title = "same-day-first", DueDate = Today.AddDays(2) },
            new Item { Title = "same-day-second", DueDate = Today.AddDays(2) },
            new Item { Title = "later", DueDate = Today.AddDays(4) },
            new Item { Title = "sooner", DueDate = Today });

        Assert.Equal(
            ["sooner", "same-day-first", "same-day-second", "later"],
            await TitlesAsync("/items/upcoming?days=7"));
    }

    [Fact]
    public async Task Upcoming_excludes_overdue_items()
    {
        await SeedAsync(
            new Item { Title = "overdue", DueDate = Today.AddDays(-1) },
            new Item { Title = "today", DueDate = Today });

        Assert.Equal(["today"], await TitlesAsync("/items/upcoming?days=7"));
    }
}
