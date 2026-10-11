using System.Net;
using System.Text.Json;

namespace Acceptance.Change20261011ItemEditB;

public sealed class ItemEditAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private async Task<JsonElement> EditItemAsync(
        int id,
        object body,
        HttpStatusCode expect = HttpStatusCode.OK
    ) => await SendAsync(HttpMethod.Put, $"/items/{id}", body, expect);

    [Fact]
    public async Task Updating_title_note_and_priority_returns_200_with_updated_fields()
    {
        // Arrange
        var created = await PostAsync(
            "/items",
            new
            {
                title = "Buy piant",
                note = "any colour",
                priority = 3,
            }
        );
        var id = created.GetProperty("id").GetInt32();

        // Act
        var result = await EditItemAsync(
            id,
            new
            {
                title = "Buy paint",
                note = "Gloss only",
                priority = 2,
            }
        );

        // Assert
        Assert.Equal("Buy paint", result.GetProperty("title").GetString());
        Assert.Equal("Gloss only", result.GetProperty("note").GetString());
        Assert.Equal(2, result.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task Sending_null_priority_clears_it()
    {
        // Arrange
        var id = await CreateItemAsync("Call dentist", 1);

        // Act
        var result = await EditItemAsync(id, new { title = "Call dentist", priority = (int?)null });

        // Assert
        Assert.Equal(JsonValueKind.Null, result.GetProperty("priority").ValueKind);
    }

    [Fact]
    public async Task Omitting_priority_clears_it()
    {
        // Arrange
        var id = await CreateItemAsync("Call dentist", 1);

        // Act
        var result = await EditItemAsync(id, new { title = "Call dentist" });

        // Assert
        Assert.Equal(JsonValueKind.Null, result.GetProperty("priority").ValueKind);
    }

    [Fact]
    public async Task Sending_null_note_clears_it()
    {
        // Arrange
        var created = await PostAsync("/items", new { title = "Call dentist", note = "urgent" });
        var id = created.GetProperty("id").GetInt32();

        // Act
        var result = await EditItemAsync(id, new { title = "Call dentist", note = (string?)null });

        // Assert
        Assert.Equal(JsonValueKind.Null, result.GetProperty("note").ValueKind);
    }

    [Fact]
    public async Task Omitting_note_clears_it()
    {
        // Arrange
        var created = await PostAsync("/items", new { title = "Call dentist", note = "urgent" });
        var id = created.GetProperty("id").GetInt32();

        // Act
        var result = await EditItemAsync(id, new { title = "Call dentist" });

        // Assert
        Assert.Equal(JsonValueKind.Null, result.GetProperty("note").ValueKind);
    }

    [Fact]
    public async Task Done_status_and_due_date_are_not_affected_by_edit()
    {
        // Arrange
        var created = await PostAsync(
            "/items",
            new { title = "Task", dueDate = new DateOnly(2026, 12, 31) }
        );
        var id = created.GetProperty("id").GetInt32();
        await MarkDoneAsync(id);

        // Act
        var result = await EditItemAsync(id, new { title = "Renamed task" });

        // Assert
        Assert.True(result.GetProperty("isDone").GetBoolean());
        Assert.Equal("2026-12-31", result.GetProperty("dueDate").GetString());
    }

    [Fact]
    public async Task Blank_title_returns_400()
    {
        // Arrange
        var id = await CreateItemAsync("Original");

        // Act & Assert
        await EditItemAsync(id, new { title = "" }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Null_title_returns_400()
    {
        // Arrange
        var id = await CreateItemAsync("Original");

        // Act & Assert
        await EditItemAsync(id, new { title = (string?)null }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Spaces_only_title_returns_400()
    {
        // Arrange
        var id = await CreateItemAsync("Original");

        // Act & Assert
        await EditItemAsync(id, new { title = "   " }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Title_of_1_character_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Original");

        // Act
        var result = await EditItemAsync(id, new { title = "X" });

        // Assert
        Assert.Equal("X", result.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Title_of_200_characters_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Original");
        var title = new string('x', 200);

        // Act
        var result = await EditItemAsync(id, new { title });

        // Assert
        Assert.Equal(title, result.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Title_of_201_characters_is_refused()
    {
        // Arrange
        var id = await CreateItemAsync("Original");

        // Act & Assert
        await EditItemAsync(id, new { title = new string('x', 201) }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Title_with_spaces_at_200_characters_after_trimming_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Original");
        var title = " " + new string('x', 200) + " ";

        // Act & Assert
        await EditItemAsync(id, new { title });
    }

    [Fact]
    public async Task Note_of_2000_characters_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Task");
        var note = new string('n', 2000);

        // Act
        var result = await EditItemAsync(id, new { title = "Task", note });

        // Assert
        Assert.Equal(note, result.GetProperty("note").GetString());
    }

    [Fact]
    public async Task Note_of_2001_characters_is_refused()
    {
        // Arrange
        var id = await CreateItemAsync("Task");

        // Act & Assert
        await EditItemAsync(
            id,
            new { title = "Task", note = new string('n', 2001) },
            HttpStatusCode.BadRequest
        );
    }

    [Fact]
    public async Task Priority_1_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Task");

        // Act
        var result = await EditItemAsync(id, new { title = "Task", priority = 1 });

        // Assert
        Assert.Equal(1, result.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task Priority_5_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Task");

        // Act
        var result = await EditItemAsync(id, new { title = "Task", priority = 5 });

        // Assert
        Assert.Equal(5, result.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task Priority_0_is_refused()
    {
        // Arrange
        var id = await CreateItemAsync("Task");

        // Act & Assert
        await EditItemAsync(id, new { title = "Task", priority = 0 }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Priority_6_is_refused()
    {
        // Arrange
        var id = await CreateItemAsync("Task");

        // Act & Assert
        await EditItemAsync(id, new { title = "Task", priority = 6 }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_item_returns_404()
    {
        // Act & Assert
        await EditItemAsync(99999, new { title = "X" }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task After_blank_title_item_is_unchanged()
    {
        // Arrange
        var created = await PostAsync(
            "/items",
            new
            {
                title = "Original",
                note = "keep",
                priority = 3,
            }
        );
        var id = created.GetProperty("id").GetInt32();
        await EditItemAsync(id, new { title = "" }, HttpStatusCode.BadRequest);

        // Act
        var list = await GetAsync("/items");

        // Assert
        var item = list.EnumerateArray().First(e => e.GetProperty("id").GetInt32() == id);
        Assert.Equal("Original", item.GetProperty("title").GetString());
        Assert.Equal("keep", item.GetProperty("note").GetString());
        Assert.Equal(3, item.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task After_invalid_priority_item_is_unchanged()
    {
        // Arrange
        var created = await PostAsync("/items", new { title = "Original", priority = 2 });
        var id = created.GetProperty("id").GetInt32();
        await EditItemAsync(
            id,
            new { title = "New title", priority = 6 },
            HttpStatusCode.BadRequest
        );

        // Act
        var list = await GetAsync("/items");

        // Assert
        var item = list.EnumerateArray().First(e => e.GetProperty("id").GetInt32() == id);
        Assert.Equal("Original", item.GetProperty("title").GetString());
        Assert.Equal(2, item.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task Response_has_id_matching_the_edited_item()
    {
        // Arrange
        var id = await CreateItemAsync("Task");

        // Act
        var result = await EditItemAsync(id, new { title = "Updated" });

        // Assert
        Assert.Equal(id, result.GetProperty("id").GetInt32());
    }
}
