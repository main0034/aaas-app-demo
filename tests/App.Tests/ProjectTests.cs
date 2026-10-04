// Validation tests that run with no database configured.
// Route behaviour is tested against Postgres in ProjectEndpointTests.

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace App.Tests;

public sealed class ProjectTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory
        .WithWebHostBuilder(b => b.UseSetting("PGHOST", ""))
        .CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_project_with_null_name_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/projects", new { name = (string?)null }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_project_with_empty_name_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/projects", new { name = "" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_project_with_whitespace_only_name_returns_400()
    {
        // Empty after trimming, so invalid even though raw length > 0.
        var response = await _client.PostAsJsonAsync("/projects", new { name = "   " }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_project_with_name_over_100_chars_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/projects", new { name = new string('x', 101) }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
