using System.Text.Json;

namespace Acceptance.Change20261011ItemsListTableB;

public sealed class ItemsTableAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    [Fact]
    public async Task Get_returns_200()
    {
        // Act & Assert
        await GetAsync("/items");
    }

    [Fact]
    public async Task Response_includes_note_priority_due_date_and_is_done_fields()
    {
        // Arrange
        await PostAsync(
            "/items",
            new
            {
                title = "Full item",
                priority = 2,
                note = "A note",
                dueDate = "2026-10-15",
            }
        );

        // Act
        var list = await GetAsync("/items");

        // Assert
        var item = list.EnumerateArray().First();
        Assert.Equal("A note", item.GetProperty("note").GetString());
        Assert.Equal(2, item.GetProperty("priority").GetInt32());
        Assert.Equal("2026-10-15", item.GetProperty("dueDate").GetString());
        Assert.False(item.GetProperty("isDone").GetBoolean());
    }

    [Fact]
    public async Task Note_is_null_when_not_set()
    {
        // Arrange
        await PostAsync("/items", new { title = "No note item" });

        // Act
        var list = await GetAsync("/items");

        // Assert
        var item = list.EnumerateArray().First();
        Assert.Equal(JsonValueKind.Null, item.GetProperty("note").ValueKind);
    }

    [Fact]
    public async Task Note_is_returned_in_full_not_truncated_at_display_limit()
    {
        // Arrange
        var longNote = new string('a', 100);
        await PostAsync("/items", new { title = "Long note item", note = longNote });

        // Act
        var list = await GetAsync("/items");

        // Assert
        var item = list.EnumerateArray().First();
        Assert.Equal(100, item.GetProperty("note").GetString()!.Length);
    }

    [Fact]
    public async Task Priority_is_null_when_not_set()
    {
        // Arrange
        await PostAsync("/items", new { title = "No priority item" });

        // Act
        var list = await GetAsync("/items");

        // Assert
        var item = list.EnumerateArray().First();
        Assert.Equal(JsonValueKind.Null, item.GetProperty("priority").ValueKind);
    }

    [Fact]
    public async Task Priority_is_returned_as_a_number()
    {
        // Arrange
        await PostAsync("/items", new { title = "Priority item", priority = 3 });

        // Act
        var list = await GetAsync("/items");

        // Assert
        var item = list.EnumerateArray().First();
        Assert.Equal(3, item.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task Due_date_is_null_when_not_set()
    {
        // Arrange
        await PostAsync("/items", new { title = "No due date item" });

        // Act
        var list = await GetAsync("/items");

        // Assert
        var item = list.EnumerateArray().First();
        Assert.Equal(JsonValueKind.Null, item.GetProperty("dueDate").ValueKind);
    }

    [Fact]
    public async Task Due_date_is_returned_in_yyyy_mm_dd_format()
    {
        // Arrange
        await PostAsync("/items", new { title = "Has due date", dueDate = "2026-10-15" });

        // Act
        var list = await GetAsync("/items");

        // Assert
        var item = list.EnumerateArray().First();
        Assert.Equal("2026-10-15", item.GetProperty("dueDate").GetString());
    }

    [Fact]
    public async Task Is_done_is_false_for_open_items()
    {
        // Arrange
        await PostAsync("/items", new { title = "Open item" });

        // Act
        var list = await GetAsync("/items");

        // Assert
        var item = list.EnumerateArray().First();
        Assert.False(item.GetProperty("isDone").GetBoolean());
    }

    [Fact]
    public async Task Is_done_is_true_for_done_items()
    {
        // Arrange
        var response = await PostAsync("/items", new { title = "Done item" });
        var id = response.GetProperty("id").GetInt32();
        await MarkDoneAsync(id);

        // Act
        var list = await GetAsync("/items");

        // Assert
        var item = list.EnumerateArray().First();
        Assert.True(item.GetProperty("isDone").GetBoolean());
    }
}
