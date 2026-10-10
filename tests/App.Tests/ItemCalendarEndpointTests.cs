using System.Net;
using System.Net.Http.Json;
using App.Data;
using App.Tests.Postgres;

namespace App.Tests;

public sealed class ItemCalendarEndpointTests(TemplateDatabase template) : EndpointTest(template)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record CalendarDay(string Date, int Due);

    private async Task<List<CalendarDay>> CalendarAsync(string url) =>
        (await Client.GetFromJsonAsync<List<CalendarDay>>(url, Ct))!;

    [Fact]
    public async Task Default_range_returns_7_days_starting_today()
    {
        // Act
        var days = await CalendarAsync("/items/calendar");

        // Assert
        Assert.Equal(7, days.Count);
        Assert.Equal(Today.ToString("yyyy-MM-dd"), days[0].Date);
        Assert.Equal(Today.AddDays(6).ToString("yyyy-MM-dd"), days[6].Date);
    }

    [Fact]
    public async Task Span_of_31_days_is_accepted()
    {
        // Arrange
        var start = new DateOnly(2026, 10, 1);
        var end = new DateOnly(2026, 10, 31);

        // Act
        var response = await Client.GetAsync(
            $"/items/calendar?from={start:yyyy-MM-dd}&to={end:yyyy-MM-dd}",
            Ct
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var days = await response.Content.ReadFromJsonAsync<List<CalendarDay>>(Ct);
        Assert.Equal(31, days!.Count);
    }

    [Fact]
    public async Task Open_item_due_in_range_is_counted()
    {
        // Arrange
        var from = Today.ToString("yyyy-MM-dd");
        var to = Today.AddDays(2).ToString("yyyy-MM-dd");
        await SeedAsync(
            new Item { Title = "in-range", DueDate = Today.AddDays(1) },
            new Item { Title = "outside-range", DueDate = Today.AddDays(5) }
        );

        // Act
        var days = await CalendarAsync($"/items/calendar?from={from}&to={to}");

        // Assert
        Assert.Equal(3, days.Count);
        Assert.Equal(0, days[0].Due);
        Assert.Equal(1, days[1].Due);
        Assert.Equal(0, days[2].Due);
    }

    [Fact]
    public async Task Done_items_are_not_counted()
    {
        // Arrange
        var date = Today.ToString("yyyy-MM-dd");
        await SeedAsync(
            new Item { Title = "open", DueDate = Today },
            new Item
            {
                Title = "done",
                DueDate = Today,
                IsDone = true,
            }
        );

        // Act
        var days = await CalendarAsync($"/items/calendar?from={date}&to={date}");

        // Assert
        Assert.Equal(1, days.Single().Due);
    }

    [Fact]
    public async Task Items_without_due_date_are_not_counted()
    {
        // Arrange
        var date = Today.ToString("yyyy-MM-dd");
        await SeedAsync(
            new Item { Title = "with-date", DueDate = Today },
            new Item { Title = "no-date" }
        );

        // Act
        var days = await CalendarAsync($"/items/calendar?from={date}&to={date}");

        // Assert
        Assert.Equal(1, days.Single().Due);
    }

    [Fact]
    public async Task All_days_in_range_are_listed_including_days_with_zero()
    {
        // Arrange
        var from = Today.ToString("yyyy-MM-dd");
        var to = Today.AddDays(2).ToString("yyyy-MM-dd");
        await SeedAsync(new Item { Title = "a", DueDate = Today });

        // Act
        var days = await CalendarAsync($"/items/calendar?from={from}&to={to}");

        // Assert
        Assert.Equal(3, days.Count);
        Assert.Equal(Today.ToString("yyyy-MM-dd"), days[0].Date);
        Assert.Equal(1, days[0].Due);
        Assert.Equal(Today.AddDays(1).ToString("yyyy-MM-dd"), days[1].Date);
        Assert.Equal(0, days[1].Due);
        Assert.Equal(Today.AddDays(2).ToString("yyyy-MM-dd"), days[2].Date);
        Assert.Equal(0, days[2].Due);
    }

    [Fact]
    public async Task Multiple_items_on_same_day_are_summed()
    {
        // Arrange
        var from = Today.ToString("yyyy-MM-dd");
        var to = Today.AddDays(1).ToString("yyyy-MM-dd");
        await SeedAsync(
            new Item { Title = "a", DueDate = Today },
            new Item { Title = "b", DueDate = Today },
            new Item { Title = "c", DueDate = Today.AddDays(1) }
        );

        // Act
        var days = await CalendarAsync($"/items/calendar?from={from}&to={to}");

        // Assert
        Assert.Equal(2, days[0].Due);
        Assert.Equal(1, days[1].Due);
    }

    [Fact]
    public async Task Items_outside_the_range_are_not_counted()
    {
        // Arrange
        var from = Today.AddDays(1).ToString("yyyy-MM-dd");
        var to = Today.AddDays(3).ToString("yyyy-MM-dd");
        await SeedAsync(
            new Item { Title = "before", DueDate = Today },
            new Item { Title = "in-range", DueDate = Today.AddDays(2) },
            new Item { Title = "after", DueDate = Today.AddDays(4) }
        );

        // Act
        var days = await CalendarAsync($"/items/calendar?from={from}&to={to}");

        // Assert
        Assert.Equal(3, days.Count);
        Assert.Equal(0, days[0].Due);
        Assert.Equal(1, days[1].Due);
        Assert.Equal(0, days[2].Due);
    }
}
