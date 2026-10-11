using App.Components.Pages;
using App.Data;
using App.Tests.Postgres;

namespace App.Tests.Pages;

public sealed class HomePageTests(TemplateDatabase template) : PageTest(template)
{
    [Fact]
    public async Task Lists_items_newest_first_with_links_to_each()
    {
        // Arrange
        await SeedAsync(
            new Item { Title = "Older item", Priority = 2 },
            new Item { Title = "Newer item", IsDone = true }
        );

        // Act
        var html = await GetPageAsync<Home>("/");

        // Assert
        Assert.True(
            html.IndexOf("Newer item", StringComparison.Ordinal)
                < html.IndexOf("Older item", StringComparison.Ordinal)
        );
        Assert.Contains("href=\"/app/items/1\"", html);
        Assert.Contains("Priority 2", html);
        Assert.Contains(" - done", html);
    }

    [Fact]
    public async Task Adding_an_item_shows_it_in_the_list()
    {
        // Act
        var html = await SubmitFormAsync<Home>(
            "/",
            "new-item",
            ("Input.Title", "Buy paint"),
            ("Input.Priority", "3"),
            ("Input.DueDate", "2030-01-02")
        );

        // Assert
        Assert.Contains("Buy paint", html);
        Assert.Contains("Priority 3", html);
        Assert.Contains("Due 2030-01-02", html);
    }

    [Fact]
    public async Task A_refused_item_shows_the_reason_and_is_not_added()
    {
        // Act: an empty title, which the route refuses
        var html = await SubmitFormAsync<Home>("/", "new-item", ("Input.Title", ""));

        // Assert
        Assert.Contains("class=\"errors\"", html);
        Assert.Contains("Nothing here yet", html);
    }
}
