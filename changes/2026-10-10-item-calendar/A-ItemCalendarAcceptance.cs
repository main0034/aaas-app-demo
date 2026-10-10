using System.Net;
using System.Text.Json;

namespace Acceptance.Change20261010ItemCalendarA;

public sealed class ItemCalendarAcceptance(App.Tests.Postgres.TemplateDatabase t)
    : AcceptanceBase(t)
{
    private static string DateStr(JsonElement e) => e.GetProperty("date").GetString()!;

    private static int DueCount(JsonElement e) => e.GetProperty("due").GetInt32();

    [Fact]
    public async Task No_parameters_returns_seven_days()
    {
        // Act
        var result = await GetAsync("/items/calendar");

        // Assert
        Assert.Equal(7, result.GetArrayLength());
    }

    [Fact]
    public async Task No_parameters_first_day_is_today_in_utc()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var result = await GetAsync("/items/calendar");

        // Assert
        Assert.Equal(today.ToString("yyyy-MM-dd"), DateStr(result.EnumerateArray().First()));
    }

    [Fact]
    public async Task No_parameters_last_day_is_six_days_after_today()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var result = await GetAsync("/items/calendar");

        // Assert
        Assert.Equal(
            today.AddDays(6).ToString("yyyy-MM-dd"),
            DateStr(result.EnumerateArray().Last())
        );
    }

    [Fact]
    public async Task Days_are_returned_in_date_order()
    {
        // Act
        var result = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-14");

        // Assert
        Assert.Equal(
            ["2026-10-12", "2026-10-13", "2026-10-14"],
            result.EnumerateArray().Select(DateStr).ToArray()
        );
    }

    [Fact]
    public async Task Day_with_no_due_items_is_still_listed_with_zero()
    {
        // Arrange
        await PostAsync(
            "/items",
            new { title = "only-on-12th", dueDate = new DateOnly(2026, 10, 12) }
        );

        // Act
        var result = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-14");

        // Assert
        var days = result.EnumerateArray().ToList();
        Assert.Equal(1, DueCount(days[0])); // 2026-10-12 has an item
        Assert.Equal(0, DueCount(days[1])); // 2026-10-13 has nothing
        Assert.Equal(0, DueCount(days[2])); // 2026-10-14 has nothing
    }

    [Fact]
    public async Task Both_start_and_end_dates_are_included()
    {
        // Arrange
        await PostAsync("/items", new { title = "on-start", dueDate = new DateOnly(2026, 10, 12) });
        await PostAsync("/items", new { title = "on-end", dueDate = new DateOnly(2026, 10, 14) });

        // Act
        var result = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-14");

        // Assert
        var days = result.EnumerateArray().ToList();
        Assert.Equal(1, DueCount(days[0])); // start date included
        Assert.Equal(1, DueCount(days[2])); // end date included
    }

    [Fact]
    public async Task Items_outside_range_are_not_counted()
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
        await PostAsync(
            "/items",
            new { title = "inside-range", dueDate = new DateOnly(2026, 10, 13) }
        );

        // Act
        var result = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-14");

        // Assert
        var days = result.EnumerateArray().ToList();
        Assert.Equal(0, DueCount(days[0])); // before-range falls on 10-11, outside
        Assert.Equal(1, DueCount(days[1])); // only inside-range counts
        Assert.Equal(0, DueCount(days[2])); // after-range falls on 10-15, outside
    }

    [Fact]
    public async Task Done_items_are_not_counted()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var id = (await PostAsync("/items", new { title = "done-item", dueDate = today }))
            .GetProperty("id")
            .GetInt32();
        await MarkDoneAsync(id);

        // Act
        var result = await GetAsync("/items/calendar");

        // Assert
        Assert.All(result.EnumerateArray(), day => Assert.Equal(0, DueCount(day)));
    }

    [Fact]
    public async Task Items_without_due_date_are_not_counted()
    {
        // Arrange
        await CreateItemAsync("no-due-date");

        // Act
        var result = await GetAsync("/items/calendar");

        // Assert
        Assert.All(result.EnumerateArray(), day => Assert.Equal(0, DueCount(day)));
    }

    [Fact]
    public async Task Multiple_open_items_due_same_day_are_all_counted()
    {
        // Arrange
        await PostAsync("/items", new { title = "item-a", dueDate = new DateOnly(2026, 10, 12) });
        await PostAsync("/items", new { title = "item-b", dueDate = new DateOnly(2026, 10, 12) });

        // Act
        var result = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-12");

        // Assert
        Assert.Equal(2, DueCount(result.EnumerateArray().First()));
    }

    [Fact]
    public async Task Open_today_done_today_open_tomorrow_gives_today_1_tomorrow_1_rest_0()
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
        var result = await GetAsync("/items/calendar");

        // Assert
        var days = result.EnumerateArray().ToList();
        Assert.Equal(7, days.Count);
        Assert.Equal(1, DueCount(days[0])); // today: 1 open (done item excluded)
        Assert.Equal(1, DueCount(days[1])); // tomorrow: 1
        Assert.All(days.Skip(2), day => Assert.Equal(0, DueCount(day)));
    }

    [Fact]
    public async Task Span_of_exactly_31_days_is_accepted()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=2026-10-01&to=2026-10-31");
    }

    [Fact]
    public async Task Span_of_32_days_is_refused()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=2026-10-01&to=2026-11-01", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task From_after_to_is_refused()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=2026-10-14&to=2026-10-12", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task From_equal_to_returns_a_single_day()
    {
        // Act
        var result = await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-12");

        // Assert
        Assert.Equal(1, result.GetArrayLength());
        Assert.Equal("2026-10-12", DateStr(result.EnumerateArray().First()));
    }

    [Fact]
    public async Task Only_from_without_to_is_refused()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=2026-10-12", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_to_without_from_is_refused()
    {
        // Act & Assert
        await GetAsync("/items/calendar?to=2026-10-14", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Invalid_from_date_format_is_refused()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=not-a-date&to=2026-10-14", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Invalid_to_date_format_is_refused()
    {
        // Act & Assert
        await GetAsync("/items/calendar?from=2026-10-12&to=not-a-date", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Calendar_does_not_modify_any_item()
    {
        // Arrange
        await PostAsync("/items", new { title = "watch-me", dueDate = new DateOnly(2026, 10, 12) });

        // Act
        await GetAsync("/items/calendar?from=2026-10-12&to=2026-10-12");

        // Assert
        var items = await GetAsync("/items");
        Assert.Equal(1, items.GetArrayLength());
    }
}
