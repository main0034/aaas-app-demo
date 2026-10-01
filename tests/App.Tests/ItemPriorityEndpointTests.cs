using System.Net.Http.Json;
using App.Data;
using App.Tests.Postgres;

namespace App.Tests;

public sealed class ItemPriorityEndpointTests(TemplateDatabase template) : EndpointTest(template)
{
    private async Task<string[]> TitlesAsync(string url) =>
        (await Client.GetFromJsonAsync<List<Item>>(url, Ct))!.Select(i => i.Title).ToArray();

    [Fact]
    public async Task Priority_list_orders_by_priority_then_newest_first()
    {
        await SeedAsync(
            new Item { Title = "p2-old", Priority = 2 },
            new Item { Title = "p1-first", Priority = 1 },
            new Item { Title = "none-old" },
            new Item { Title = "p1-second", Priority = 1 },
            new Item { Title = "none-new" });

        Assert.Equal(
            ["p1-second", "p1-first", "p2-old", "none-new", "none-old"],
            await TitlesAsync("/items/priority"));
    }

    [Fact]
    public async Task Priority_list_excludes_done_items()
    {
        await SeedAsync(
            new Item { Title = "open", Priority = 1 },
            new Item { Title = "done", Priority = 1, IsDone = true });

        Assert.Equal(["open"], await TitlesAsync("/items/priority"));
    }

    [Fact]
    public async Task Priority_list_with_maxPriority_excludes_lower_and_unprioritised()
    {
        await SeedAsync(
            new Item { Title = "p1", Priority = 1 },
            new Item { Title = "p2", Priority = 2 },
            new Item { Title = "p3", Priority = 3 },
            new Item { Title = "none" });

        Assert.Equal(["p1", "p2"], await TitlesAsync("/items/priority?maxPriority=2"));
    }

    [Fact]
    public async Task Priority_list_with_maxPriority_1_returns_only_priority_1()
    {
        await SeedAsync(
            new Item { Title = "p1", Priority = 1 },
            new Item { Title = "p2", Priority = 2 },
            new Item { Title = "none" });

        Assert.Equal(["p1"], await TitlesAsync("/items/priority?maxPriority=1"));
    }
}
