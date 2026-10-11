using System.Net.Http.Json;
using App.Components.Pages;
using App.Data;
using App.Tests.Postgres;

namespace App.Tests.Pages;

public sealed class ItemDetailPageTests(TemplateDatabase template) : PageTest(template)
{
    [Fact]
    public async Task Shows_the_item_in_its_form()
    {
        // Arrange
        await SeedAsync(
            new Item
            {
                Title = "Paint fence",
                Note = "white",
                Priority = 2,
            }
        );

        // Act
        var html = await GetPageAsync<ItemDetail>("/app/items/1");

        // Assert
        Assert.Contains("<h1>Paint fence</h1>", html);
        Assert.Contains("white</textarea>", html);
        Assert.Contains("value=\"2\" selected", html);
    }

    [Fact]
    public async Task An_item_that_does_not_exist_says_so()
    {
        // Act
        var html = await GetPageAsync<ItemDetail>("/app/items/99");

        // Assert
        Assert.Contains("This item no longer exists.", html);
    }

    [Fact]
    public async Task Saving_an_edit_goes_through_the_route_and_back_to_the_list()
    {
        // Arrange
        await SeedAsync(new Item { Title = "Paint fence", Note = "white" });

        // Act: the route trims the title and clears the note
        var html = await SubmitFormAsync<ItemDetail>(
            "/app/items/1",
            "edit-item",
            ("Edit.Title", "  Paint gate  "),
            ("Edit.Note", "")
        );
        var item = await Client.GetFromJsonAsync<Item>("/items/1", Ct);

        // Assert
        Assert.Contains("<h1>Items</h1>", html);
        Assert.Equal("Paint gate", item!.Title);
        Assert.Null(item.Note);
    }

    [Fact]
    public async Task A_refused_edit_shows_the_reason_and_changes_nothing()
    {
        // Arrange
        await SeedAsync(new Item { Title = "Paint fence" });

        // Act: a title that is blank after trimming
        var html = await SubmitFormAsync<ItemDetail>(
            "/app/items/1",
            "edit-item",
            ("Edit.Title", "   ")
        );
        var item = await Client.GetFromJsonAsync<Item>("/items/1", Ct);

        // Assert
        Assert.Contains("class=\"errors\"", html);
        Assert.Equal("Paint fence", item!.Title);
    }

    [Fact]
    public async Task Setting_a_due_date_and_marking_done_show_on_the_page()
    {
        // Arrange
        await SeedAsync(new Item { Title = "Paint fence" });

        // Act
        await SubmitFormAsync<ItemDetail>("/app/items/1", "due-date", ("Due.Date", "2030-05-06"));
        var html = await SubmitFormAsync<ItemDetail>(
            "/app/items/1",
            "done",
            ("Done.Value", "true")
        );

        // Assert
        Assert.Contains("value=\"2030-05-06\"", html);
        Assert.Contains("This item is done.", html);
        Assert.Contains("Reopen", html);
    }
}
