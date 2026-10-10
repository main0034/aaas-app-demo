using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace App.Tests;

public sealed class ItemSummaryValidationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory
        .WithWebHostBuilder(b => b.UseSetting("PGHOST", ""))
        .CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Q_over_100_chars_is_400()
    {
        // Arrange
        var q = new string('x', 101);

        // Act
        var response = await _client.GetAsync($"/items/summary?q={q}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
