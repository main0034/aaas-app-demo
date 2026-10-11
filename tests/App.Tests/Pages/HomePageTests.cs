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
        Assert.Contains("<td>2</td>", html);
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
        Assert.Contains("<td>3</td>", html);
        Assert.Contains("2030-01-02", html);
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

    [Fact]
    public async Task Table_shows_column_headers()
    {
        // Arrange
        await SeedAsync(new Item { Title = "Paint fence" });

        // Act
        var html = await GetPageAsync<Home>("/");

        // Assert
        Assert.Contains("<th>Title</th>", html);
        Assert.Contains("<th>Priority</th>", html);
        Assert.Contains("<th>Due date</th>", html);
        Assert.Contains("<th>Note</th>", html);
    }

    [Fact]
    public async Task Missing_priority_due_date_and_note_show_em_dash()
    {
        // Arrange
        await SeedAsync(new Item { Title = "Call dentist" });

        // Act
        var html = await GetPageAsync<Home>("/");

        // Assert: three em-dashes for missing priority, due date, and note
        Assert.Equal(3, html.Split('—').Length - 1);
    }

    [Fact]
    public async Task Note_longer_than_60_characters_is_truncated_with_ellipsis()
    {
        // Arrange
        await SeedAsync(
            new Item
            {
                Title = "Long note item",
                Note = "This note is definitely longer than sixty characters in total",
            }
        );

        // Act
        var html = await GetPageAsync<Home>("/");

        // Assert
        Assert.Contains("…", html);
        Assert.DoesNotContain("in total", html);
    }
}
