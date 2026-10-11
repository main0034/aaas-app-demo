using System.Text.Json;

namespace Acceptance.Change20261011ItemsListTableA;

public sealed class ItemsListTableAcceptance(App.Tests.Postgres.TemplateDatabase t)
    : AcceptanceBase(t)
{
    [Fact]
    public async Task Get_items_returns_200_with_all_required_fields()
    {
        // Arrange
        await CreateItemAsync("Sample item", 2);

        // Act
        var list = await GetAsync("/items");

        // Assert
        var item = list.EnumerateArray().First();
        Assert.True(item.TryGetProperty("id", out _));
        Assert.True(item.TryGetProperty("title", out _));
        Assert.True(item.TryGetProperty("note", out _));
        Assert.True(item.TryGetProperty("priority", out _));
        Assert.True(item.TryGetProperty("dueDate", out _));
        Assert.True(item.TryGetProperty("isDone", out _));
    }

    [Fact]
    public async Task Item_with_all_optional_fields_set_returns_their_values()
    {
        // Arrange
        var created = await PostAsync(
            "/items",
            new
            {
                title = "Full item",
                note = "A note here",
                priority = 3,
                dueDate = "2026-10-20",
            }
        );
        var id = created.GetProperty("id").GetInt32();

        // Act
        var list = await GetAsync("/items");
        var item = list.EnumerateArray().First(e => e.GetProperty("id").GetInt32() == id);

        // Assert
        Assert.Equal("Full item", item.GetProperty("title").GetString());
        Assert.Equal("A note here", item.GetProperty("note").GetString());
        Assert.Equal(3, item.GetProperty("priority").GetInt32());
        Assert.Equal("2026-10-20", item.GetProperty("dueDate").GetString());
    }

    [Fact]
    public async Task Item_with_no_optional_fields_returns_null_for_note_priority_and_dueDate()
    {
        // Arrange
        var id = await CreateItemAsync("Minimal item");

        // Act
        var list = await GetAsync("/items");
        var item = list.EnumerateArray().First(e => e.GetProperty("id").GetInt32() == id);

        // Assert
        Assert.Equal(JsonValueKind.Null, item.GetProperty("note").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("priority").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("dueDate").ValueKind);
    }

    [Fact]
    public async Task Done_item_has_isDone_true_and_open_item_has_isDone_false()
    {
        // Arrange
        var openId = await CreateItemAsync("Open item");
        var doneId = await CreateItemAsync("Done item");
        await MarkDoneAsync(doneId);

        // Act
        var list = await GetAsync("/items");

        // Assert
        var openItem = list.EnumerateArray().First(e => e.GetProperty("id").GetInt32() == openId);
        var doneItem = list.EnumerateArray().First(e => e.GetProperty("id").GetInt32() == doneId);
        Assert.False(openItem.GetProperty("isDone").GetBoolean());
        Assert.True(doneItem.GetProperty("isDone").GetBoolean());
    }

    [Fact]
    public async Task Viewing_the_list_does_not_change_any_item()
    {
        // Arrange
        var id = (
            await PostAsync(
                "/items",
                new
                {
                    title = "Stable item",
                    note = "Keep note",
                    priority = 2,
                    dueDate = "2026-11-01",
                }
            )
        ).GetProperty("id").GetInt32();

        // Act
        await GetAsync("/items");
        var item = await GetAsync($"/items/{id}");

        // Assert
        Assert.Equal("Stable item", item.GetProperty("title").GetString());
        Assert.Equal("Keep note", item.GetProperty("note").GetString());
        Assert.Equal(2, item.GetProperty("priority").GetInt32());
        Assert.False(item.GetProperty("isDone").GetBoolean());
    }

    [Fact]
    public async Task Full_note_text_is_returned_untruncated()
    {
        // Arrange
        var longNote = new string('x', 100);
        var id = (await PostAsync("/items", new { title = "Long note item", note = longNote }))
            .GetProperty("id")
            .GetInt32();

        // Act
        var list = await GetAsync("/items");
        var item = list.EnumerateArray().First(e => e.GetProperty("id").GetInt32() == id);

        // Assert
        Assert.Equal(100, item.GetProperty("note").GetString()!.Length);
    }

    [Fact]
    public async Task Items_are_returned_newest_first()
    {
        // Arrange
        await CreateItemAsync("First created");
        await CreateItemAsync("Second created");
        await CreateItemAsync("Third created");

        // Act
        var list = await GetAsync("/items");

        // Assert
        Assert.Equal(["Third created", "Second created", "First created"], Titles(list));
    }
}
