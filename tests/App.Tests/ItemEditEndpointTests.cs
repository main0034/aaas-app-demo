using System.Net;
using System.Net.Http.Json;
using App.Data;
using App.Tests.Postgres;

namespace App.Tests;

public sealed class ItemEditEndpointTests(TemplateDatabase template) : EndpointTest(template)
{
    [Fact]
    public async Task Edit_updates_title_note_and_priority()
    {
        // Arrange
        await SeedAsync(
            new Item
            {
                Title = "Buy piant",
                Note = "any colour",
                Priority = 3,
            },
            new Item { Title = "other" }
        );
        var all = (await Client.GetFromJsonAsync<List<Item>>("/items", Ct))!;
        var target = all.Single(i => i.Title == "Buy piant");

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/items/{target.Id}",
            new
            {
                title = "Buy paint",
                note = "Gloss only",
                priority = 2,
            },
            Ct
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<Item>(Ct))!;
        Assert.Equal("Buy paint", item.Title);
        Assert.Equal("Gloss only", item.Note);
        Assert.Equal(2, item.Priority);
        Assert.Equal(target.Id, item.Id);
    }

    [Fact]
    public async Task Edit_clears_priority_when_omitted()
    {
        // Arrange
        await SeedAsync(
            new Item { Title = "Call dentist", Priority = 1 },
            new Item { Title = "other" }
        );
        var all = (await Client.GetFromJsonAsync<List<Item>>("/items", Ct))!;
        var target = all.Single(i => i.Title == "Call dentist");

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/items/{target.Id}",
            new { title = "Call dentist" },
            Ct
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<Item>(Ct))!;
        Assert.Equal("Call dentist", item.Title);
        Assert.Null(item.Priority);
    }

    [Fact]
    public async Task Edit_clears_note_when_omitted()
    {
        // Arrange
        await SeedAsync(
            new Item { Title = "Call dentist", Note = "old note" },
            new Item { Title = "other" }
        );
        var all = (await Client.GetFromJsonAsync<List<Item>>("/items", Ct))!;
        var target = all.Single(i => i.Title == "Call dentist");

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/items/{target.Id}",
            new { title = "Call dentist" },
            Ct
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<Item>(Ct))!;
        Assert.Equal("Call dentist", item.Title);
        Assert.Null(item.Note);
    }

    [Fact]
    public async Task Edit_does_not_affect_done_status_or_due_date()
    {
        // Arrange
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
        await SeedAsync(
            new Item
            {
                Title = "Old title",
                IsDone = true,
                DoneAt = DateTimeOffset.UtcNow,
                DueDate = dueDate,
            },
            new Item { Title = "other" }
        );
        var all = (await Client.GetFromJsonAsync<List<Item>>("/items", Ct))!;
        var target = all.Single(i => i.Title == "Old title");

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/items/{target.Id}",
            new { title = "New title" },
            Ct
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<Item>(Ct))!;
        Assert.Equal("New title", item.Title);
        Assert.True(item.IsDone);
        Assert.Equal(dueDate, item.DueDate);
    }

    [Fact]
    public async Task Edit_on_missing_item_is_404()
    {
        // Act
        var response = await Client.PutAsJsonAsync("/items/999", new { title = "anything" }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
