using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace App.Tests;

public sealed class ItemCalendarValidationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory
        .WithWebHostBuilder(b => b.UseSetting("PGHOST", ""))
        .CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task From_without_to_is_400()
    {
        // Act
        var response = await _client.GetAsync("/items/calendar?from=2026-10-01", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task To_without_from_is_400()
    {
        // Act
        var response = await _client.GetAsync("/items/calendar?to=2026-10-01", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_from_format_is_400()
    {
        // Act
        var response = await _client.GetAsync("/items/calendar?from=not-a-date&to=2026-10-01", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_to_format_is_400()
    {
        // Act
        var response = await _client.GetAsync("/items/calendar?from=2026-10-01&to=not-a-date", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task From_after_to_is_400()
    {
        // Act
        var response = await _client.GetAsync("/items/calendar?from=2026-10-15&to=2026-10-01", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Span_of_32_days_is_400()
    {
        // Act
        var response = await _client.GetAsync("/items/calendar?from=2026-10-01&to=2026-11-01", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
