using System.Net;
using System.Text.Json;

namespace Acceptance.Change20261010ItemCalendarB;

public sealed class ItemCalendarAcceptance(App.Tests.Postgres.TemplateDatabase t)
    : AcceptanceBase(t)
{
    private static int DueOnDate(JsonElement calendar, string date) =>
        calendar
            .EnumerateArray()
            .First(e => e.GetProperty("date").GetString() == date)
            .GetProperty("due")
            .GetInt32();

    [Fact]
    public async Task Calendar_lists_every_day_in_range_in_order()
    {
        // Act
        var cal = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-14");

        // Assert
        var dates = cal.EnumerateArray().Select(e => e.GetProperty("date").GetString()).ToArray();
        Assert.Equal(["2026-10-12", "2026-10-13", "2026-10-14"], dates);
    }

    [Fact]
    public async Task Day_with_no_due_items_is_listed_with_zero()
    {
        // Act
        var cal = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-12");

        // Assert
        Assert.Equal(0, DueOnDate(cal, "2026-10-12"));
    }

    [Fact]
    public async Task Open_items_due_on_a_date_are_counted()
    {
        // Arrange
        await PostAsync("/items", new { title = "a", dueDate = new DateOnly(2026, 10, 12) });
        await PostAsync("/items", new { title = "b", dueDate = new DateOnly(2026, 10, 12) });
        await PostAsync("/items", new { title = "c", dueDate = new DateOnly(2026, 10, 14) });

        // Act
        var cal = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-14");

        // Assert
        Assert.Equal(2, DueOnDate(cal, "2026-10-12"));
        Assert.Equal(0, DueOnDate(cal, "2026-10-13"));
        Assert.Equal(1, DueOnDate(cal, "2026-10-14"));
    }

    [Fact]
    public async Task Done_item_is_not_counted()
    {
        // Arrange
        var id = (
            await PostAsync(
                "/items",
                new { title = "done-item", dueDate = new DateOnly(2026, 10, 12) }
            )
        )
            .GetProperty("id")
            .GetInt32();
        await MarkDoneAsync(id);
        await PostAsync(
            "/items",
            new { title = "open-item", dueDate = new DateOnly(2026, 10, 12) }
        );

        // Act
        var cal = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-12");

        // Assert
        Assert.Equal(1, DueOnDate(cal, "2026-10-12"));
    }

    [Fact]
    public async Task Item_without_due_date_is_not_counted()
    {
        // Arrange
        await CreateItemAsync("no-due-date");
        await PostAsync(
            "/items",
            new { title = "has-due-date", dueDate = new DateOnly(2026, 10, 12) }
        );

        // Act
        var cal = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-12");

        // Assert
        Assert.Equal(1, DueOnDate(cal, "2026-10-12"));
    }

    [Fact]
    public async Task Item_due_outside_range_is_not_counted()
    {
        // Arrange
        await PostAsync(
            "/items",
            new { title = "before-range", dueDate = new DateOnly(2026, 10, 11) }
        );
        await PostAsync(
            "/items",
            new { title = "after-range", dueDate = new DateOnly(2026, 10, 15) }
        );
        await PostAsync("/items", new { title = "in-range", dueDate = new DateOnly(2026, 10, 13) });

        // Act
        var cal = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-14");

        // Assert
        Assert.Equal(0, DueOnDate(cal, "2026-10-12"));
        Assert.Equal(1, DueOnDate(cal, "2026-10-13"));
        Assert.Equal(0, DueOnDate(cal, "2026-10-14"));
    }

    [Fact]
    public async Task Start_and_end_dates_are_both_included_in_range()
    {
        // Arrange
        await PostAsync("/items", new { title = "on-start", dueDate = new DateOnly(2026, 10, 12) });
        await PostAsync("/items", new { title = "on-end", dueDate = new DateOnly(2026, 10, 14) });

        // Act
        var cal = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-14");

        // Assert
        Assert.Equal(1, DueOnDate(cal, "2026-10-12"));
        Assert.Equal(1, DueOnDate(cal, "2026-10-14"));
    }

    [Fact]
    public async Task No_dates_returns_seven_days_starting_today()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var cal = await GetAsync("/items/calendar");

        // Assert
        Assert.Equal(7, cal.GetArrayLength());
        Assert.Equal(today.ToString("yyyy-MM-dd"), cal[0].GetProperty("date").GetString());
        Assert.Equal(
            today.AddDays(6).ToString("yyyy-MM-dd"),
            cal[6].GetProperty("date").GetString()
        );
    }

    [Fact]
    public async Task Default_calendar_counts_open_items_due_today_and_tomorrow_correctly()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "open-today", dueDate = today });
        var doneId = (await PostAsync("/items", new { title = "done-today", dueDate = today }))
            .GetProperty("id")
            .GetInt32();
        await MarkDoneAsync(doneId);
        await PostAsync("/items", new { title = "open-tomorrow", dueDate = today.AddDays(1) });

        // Act
        var cal = await GetAsync("/items/calendar");

        // Assert
        Assert.Equal(1, DueOnDate(cal, today.ToString("yyyy-MM-dd")));
        Assert.Equal(1, DueOnDate(cal, today.AddDays(1).ToString("yyyy-MM-dd")));
        Assert.Equal(0, DueOnDate(cal, today.AddDays(2).ToString("yyyy-MM-dd")));
        Assert.Equal(0, DueOnDate(cal, today.AddDays(5).ToString("yyyy-MM-dd")));
    }

    [Fact]
    public async Task From_after_to_returns_400()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=2026-10-14&to=2026-10-12", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Span_of_exactly_31_days_is_accepted()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=2026-10-01&to=2026-10-31");
    }

    [Fact]
    public async Task Span_of_32_days_returns_400()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=2026-10-01&to=2026-11-01", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Single_day_span_is_accepted()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-12");
    }

    [Fact]
    public async Task Only_from_provided_returns_400()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=2026-10-12", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_to_provided_returns_400()
    {
        // Act & Assert
        await GetAsync("/items/calendar?to=2026-10-12", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Invalid_from_date_format_returns_400()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=not-a-date&to=2026-10-14", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Invalid_to_date_format_returns_400()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=2026-10-12&to=not-a-date", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Getting_calendar_does_not_change_items()
    {
        // Arrange
        await PostAsync("/items", new { title = "a", dueDate = new DateOnly(2026, 10, 12) });
        await PostAsync("/items", new { title = "b", dueDate = new DateOnly(2026, 10, 13) });

        // Act
        await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-14");

        // Assert
        var items = await GetAsync("/items");
        Assert.Equal(2, items.GetArrayLength());
    }
}
