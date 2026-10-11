using System.Net;
using System.Text.Json;

namespace Acceptance.Change20261011ItemEditA;

public sealed class ItemEditAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private async Task<JsonElement> PutItemAsync(
        int id,
        object body,
        HttpStatusCode expect = HttpStatusCode.OK
    ) => await SendAsync(HttpMethod.Put, $"/items/{id}", body, expect);

    [Fact]
    public async Task Corrected_title_note_and_priority_are_returned()
    {
        // Arrange
        var id = (
            await PostAsync(
                "/items",
                new
                {
                    title = "Buy piant",
                    note = "any colour",
                    priority = 3,
                }
            )
        ).GetProperty("id").GetInt32();

        // Act
        var result = await PutItemAsync(
            id,
            new
            {
                title = "Buy paint",
                note = "Gloss only",
                priority = 2,
            }
        );

        // Assert
        Assert.Equal(id, result.GetProperty("id").GetInt32());
        Assert.Equal("Buy paint", result.GetProperty("title").GetString());
        Assert.Equal("Gloss only", result.GetProperty("note").GetString());
        Assert.Equal(2, result.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task Unknown_item_returns_404()
    {
        // Act & Assert
        await PutItemAsync(99999, new { title = "anything" }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Blank_title_returns_400()
    {
        // Arrange
        var id = await CreateItemAsync("Original");

        // Act & Assert
        await PutItemAsync(id, new { title = "   " }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Title_over_200_chars_returns_400()
    {
        // Arrange
        var id = await CreateItemAsync("Original");

        // Act & Assert
        await PutItemAsync(id, new { title = new string('x', 201) }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Title_of_1_char_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Original");

        // Act
        var result = await PutItemAsync(id, new { title = "A" });

        // Assert
        Assert.Equal("A", result.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Title_of_200_chars_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Original");
        var title = new string('x', 200);

        // Act
        var result = await PutItemAsync(id, new { title });

        // Assert
        Assert.Equal(title, result.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Title_valid_only_after_trimming_is_accepted()
    {
        // 201 chars raw (over the 200 limit), but 200 after trimming the leading space.
        // Arrange
        var id = await CreateItemAsync("Original");

        // Act & Assert
        await PutItemAsync(id, new { title = " " + new string('x', 200) });
    }

    [Fact]
    public async Task Note_over_2000_chars_returns_400()
    {
        // Arrange
        var id = await CreateItemAsync("Task");

        // Act & Assert
        await PutItemAsync(
            id,
            new { title = "Task", note = new string('x', 2001) },
            HttpStatusCode.BadRequest
        );
    }

    [Fact]
    public async Task Note_of_2000_chars_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Task");
        var note = new string('x', 2000);

        // Act
        var result = await PutItemAsync(id, new { title = "Task", note });

        // Assert
        Assert.Equal(note, result.GetProperty("note").GetString());
    }

    [Fact]
    public async Task Null_note_clears_it()
    {
        // Arrange
        var id = (await PostAsync("/items", new { title = "Task", note = "Some note" }))
            .GetProperty("id")
            .GetInt32();

        // Act
        var result = await PutItemAsync(id, new { title = "Call dentist", note = (string?)null });

        // Assert
        Assert.Equal(JsonValueKind.Null, result.GetProperty("note").ValueKind);
    }

    [Fact]
    public async Task Omitting_note_clears_it()
    {
        // Arrange
        var id = (await PostAsync("/items", new { title = "Task", note = "Some note" }))
            .GetProperty("id")
            .GetInt32();

        // Act
        var result = await PutItemAsync(id, new { title = "Call dentist" });

        // Assert
        Assert.Equal(JsonValueKind.Null, result.GetProperty("note").ValueKind);
    }

    [Fact]
    public async Task Priority_0_returns_400()
    {
        // Arrange
        var id = await CreateItemAsync("Task");

        // Act & Assert
        await PutItemAsync(id, new { title = "Task", priority = 0 }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Priority_6_returns_400()
    {
        // Arrange
        var id = await CreateItemAsync("Task");

        // Act & Assert
        await PutItemAsync(id, new { title = "Task", priority = 6 }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Priority_1_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Task");

        // Act
        var result = await PutItemAsync(id, new { title = "Task", priority = 1 });

        // Assert
        Assert.Equal(1, result.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task Priority_5_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Task");

        // Act
        var result = await PutItemAsync(id, new { title = "Task", priority = 5 });

        // Assert
        Assert.Equal(5, result.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task Null_priority_clears_it()
    {
        // Arrange
        var id = await CreateItemAsync("Task", 1);

        // Act
        var result = await PutItemAsync(id, new { title = "Call dentist", priority = (int?)null });

        // Assert
        Assert.Equal(JsonValueKind.Null, result.GetProperty("priority").ValueKind);
    }

    [Fact]
    public async Task Omitting_priority_clears_it()
    {
        // Arrange
        var id = await CreateItemAsync("Task", 1);

        // Act
        var result = await PutItemAsync(id, new { title = "Call dentist" });

        // Assert
        Assert.Equal(JsonValueKind.Null, result.GetProperty("priority").ValueKind);
    }

    [Fact]
    public async Task Done_status_not_affected_by_edit()
    {
        // Arrange
        var id = await CreateItemAsync("Task");
        await MarkDoneAsync(id);

        // Act
        var result = await PutItemAsync(id, new { title = "Task updated" });

        // Assert
        Assert.True(result.GetProperty("isDone").GetBoolean());
    }

    [Fact]
    public async Task Due_date_not_affected_by_edit()
    {
        // Arrange
        var id = await CreateItemAsync("Task");
        await SendAsync(
            HttpMethod.Put,
            $"/items/{id}/due-date",
            new { date = "2026-12-01" },
            HttpStatusCode.OK
        );

        // Act
        var result = await PutItemAsync(id, new { title = "Task updated" });

        // Assert
        Assert.Equal("2026-12-01", result.GetProperty("dueDate").GetString());
    }

    [Fact]
    public async Task After_blank_title_item_is_unchanged()
    {
        // Arrange
        var id = await CreateItemAsync("Original title");
        await PutItemAsync(id, new { title = "   " }, HttpStatusCode.BadRequest);

        // Act
        var list = await GetAsync("/items");
        var item = list.EnumerateArray().First(e => e.GetProperty("id").GetInt32() == id);

        // Assert
        Assert.Equal("Original title", item.GetProperty("title").GetString());
    }
}
