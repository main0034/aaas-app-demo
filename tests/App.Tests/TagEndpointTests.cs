using System.Net;
using System.Net.Http.Json;
using App.Data;
using App.Endpoints;
using App.Tests.Postgres;

namespace App.Tests;

public sealed class TagEndpointTests(TemplateDatabase template) : EndpointTest(template)
{
    private async Task<int> ItemIdAsync(string title)
    {
        var items = await Client.GetFromJsonAsync<List<Item>>("/items", Ct);
        return items!.Single(i => i.Title == title).Id;
    }

    [Fact]
    public async Task PutTags_sets_tags_and_returns_sorted_array()
    {
        await SeedAsync(new Item { Title = "a" }, new Item { Title = "b" });
        var id = await ItemIdAsync("a");

        var response = await Client.PutAsJsonAsync($"/items/{id}/tags", new { tags = new[] { "urgent", "home" } }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tags = await response.Content.ReadFromJsonAsync<string[]>(Ct);
        Assert.Equal(["home", "urgent"], tags!);
    }

    [Fact]
    public async Task PutTags_replaces_existing_tags()
    {
        await SeedAsync(new Item { Title = "a" });
        var id = await ItemIdAsync("a");
        await Client.PutAsJsonAsync($"/items/{id}/tags", new { tags = new[] { "old" } }, Ct);

        var response = await Client.PutAsJsonAsync($"/items/{id}/tags", new { tags = new[] { "new" } }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tags = await response.Content.ReadFromJsonAsync<string[]>(Ct);
        Assert.Equal(["new"], tags!);
    }

    [Fact]
    public async Task PutTags_empty_array_removes_all_tags()
    {
        await SeedAsync(new Item { Title = "a" });
        var id = await ItemIdAsync("a");
        await Client.PutAsJsonAsync($"/items/{id}/tags", new { tags = new[] { "home" } }, Ct);

        var response = await Client.PutAsJsonAsync($"/items/{id}/tags", new { tags = Array.Empty<string>() }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tags = await response.Content.ReadFromJsonAsync<string[]>(Ct);
        Assert.Equal([], tags!);
    }

    [Fact]
    public async Task PutTags_normalizes_case_and_surrounding_spaces()
    {
        await SeedAsync(new Item { Title = "a" });
        var id = await ItemIdAsync("a");

        var response = await Client.PutAsJsonAsync($"/items/{id}/tags", new { tags = new[] { " Home ", "URGENT" } }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tags = await response.Content.ReadFromJsonAsync<string[]>(Ct);
        Assert.Equal(["home", "urgent"], tags!);
    }

    [Fact]
    public async Task PutTags_deduplicates_after_normalization()
    {
        await SeedAsync(new Item { Title = "a" });
        var id = await ItemIdAsync("a");

        var response = await Client.PutAsJsonAsync($"/items/{id}/tags", new { tags = new[] { "home", "HOME", " home " } }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tags = await response.Content.ReadFromJsonAsync<string[]>(Ct);
        Assert.Equal(["home"], tags!);
    }

    [Fact]
    public async Task PutTags_unknown_item_returns_404()
    {
        var response = await Client.PutAsJsonAsync("/items/999/tags", new { tags = new[] { "home" } }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTags_returns_sorted_array()
    {
        await SeedAsync(new Item { Title = "a" });
        var id = await ItemIdAsync("a");
        await Client.PutAsJsonAsync($"/items/{id}/tags", new { tags = new[] { "work", "home" } }, Ct);

        var tags = await Client.GetFromJsonAsync<string[]>($"/items/{id}/tags", Ct);

        Assert.Equal(["home", "work"], tags!);
    }

    [Fact]
    public async Task GetTags_unknown_item_returns_404()
    {
        var response = await Client.GetAsync("/items/999/tags", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTags_empty_for_item_with_no_tags()
    {
        await SeedAsync(new Item { Title = "a" });
        var id = await ItemIdAsync("a");

        var tags = await Client.GetFromJsonAsync<string[]>($"/items/{id}/tags", Ct);

        Assert.Equal([], tags!);
    }

    [Fact]
    public async Task GetAllTags_returns_tags_with_item_counts_sorted_by_name()
    {
        await SeedAsync(
            new Item { Title = "item1" },
            new Item { Title = "item2" },
            new Item { Title = "item3" });
        var id1 = await ItemIdAsync("item1");
        var id2 = await ItemIdAsync("item2");
        var id3 = await ItemIdAsync("item3");

        await Client.PutAsJsonAsync($"/items/{id1}/tags", new { tags = new[] { "home", "work" } }, Ct);
        await Client.PutAsJsonAsync($"/items/{id2}/tags", new { tags = new[] { "home" } }, Ct);
        // item3 has no tags — must not appear in the result

        var result = await Client.GetFromJsonAsync<List<TagSummary>>("/tags", Ct);

        Assert.Equal(
            [new TagSummary("home", 2), new TagSummary("work", 1)],
            result);
    }

    [Fact]
    public async Task GetAllTags_counts_done_items_too()
    {
        await SeedAsync(
            new Item { Title = "open" },
            new Item { Title = "done", IsDone = true });
        var openId = await ItemIdAsync("open");
        var doneId = await ItemIdAsync("done");

        await Client.PutAsJsonAsync($"/items/{openId}/tags", new { tags = new[] { "home" } }, Ct);
        await Client.PutAsJsonAsync($"/items/{doneId}/tags", new { tags = new[] { "home" } }, Ct);

        var result = await Client.GetFromJsonAsync<List<TagSummary>>("/tags", Ct);

        Assert.Equal([new TagSummary("home", 2)], result);
    }

    [Fact]
    public async Task GetAllTags_empty_when_no_tags_exist()
    {
        await SeedAsync(new Item { Title = "a" });

        var result = await Client.GetFromJsonAsync<List<TagSummary>>("/tags", Ct);

        Assert.Equal([], result);
    }

    [Fact]
    public async Task ListItems_filters_by_tag_excludes_untagged()
    {
        await SeedAsync(
            new Item { Title = "tagged" },
            new Item { Title = "other-tag" },
            new Item { Title = "no-tag" });
        var taggedId = await ItemIdAsync("tagged");
        var otherId = await ItemIdAsync("other-tag");

        await Client.PutAsJsonAsync($"/items/{taggedId}/tags", new { tags = new[] { "home" } }, Ct);
        await Client.PutAsJsonAsync($"/items/{otherId}/tags", new { tags = new[] { "work" } }, Ct);

        var items = await Client.GetFromJsonAsync<List<Item>>("/items?tag=home", Ct);

        Assert.Equal(["tagged"], items!.Select(i => i.Title));
    }

    [Fact]
    public async Task ListItems_tag_filter_is_case_insensitive()
    {
        await SeedAsync(new Item { Title = "a" }, new Item { Title = "b" });
        var id = await ItemIdAsync("a");
        await Client.PutAsJsonAsync($"/items/{id}/tags", new { tags = new[] { "home" } }, Ct);

        var items = await Client.GetFromJsonAsync<List<Item>>("/items?tag=HOME", Ct);

        Assert.Equal(["a"], items!.Select(i => i.Title));
    }

    [Fact]
    public async Task ListItems_tag_filter_combines_with_open()
    {
        await SeedAsync(
            new Item { Title = "open-tagged" },
            new Item { Title = "done-tagged", IsDone = true },
            new Item { Title = "open-untagged" });
        var openId = await ItemIdAsync("open-tagged");
        var doneId = await ItemIdAsync("done-tagged");

        await Client.PutAsJsonAsync($"/items/{openId}/tags", new { tags = new[] { "home" } }, Ct);
        await Client.PutAsJsonAsync($"/items/{doneId}/tags", new { tags = new[] { "home" } }, Ct);

        var items = await Client.GetFromJsonAsync<List<Item>>("/items?tag=home&open=true", Ct);

        Assert.Equal(["open-tagged"], items!.Select(i => i.Title));
    }

    [Fact]
    public async Task ListItems_tag_filter_combines_with_search()
    {
        await SeedAsync(
            new Item { Title = "fix auth" },
            new Item { Title = "fix deploy" },
            new Item { Title = "add feature" });
        var fixAuthId = await ItemIdAsync("fix auth");
        var fixDeployId = await ItemIdAsync("fix deploy");

        await Client.PutAsJsonAsync($"/items/{fixAuthId}/tags", new { tags = new[] { "urgent" } }, Ct);
        await Client.PutAsJsonAsync($"/items/{fixDeployId}/tags", new { tags = new[] { "work" } }, Ct);

        var items = await Client.GetFromJsonAsync<List<Item>>("/items?q=fix&tag=urgent", Ct);

        Assert.Equal(["fix auth"], items!.Select(i => i.Title));
    }
}
