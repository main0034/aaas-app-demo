// Validation runs before the database is touched, so these need none.
// Route behaviour is tested against Postgres in ItemEndpointTests.

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace App.Tests;

public sealed class ItemTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory
        .WithWebHostBuilder(b => b.UseSetting("PGHOST", ""))
        .CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MarkDone_rejects_missing_done_field()
    {
        // Act
        var response = await _client.PatchAsJsonAsync("/items/1", new { }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Edit_rejects_blank_title()
    {
        // Act
        var response = await _client.PutAsJsonAsync("/items/1", new { title = "   " }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Edit_rejects_missing_title()
    {
        // Act
        var response = await _client.PutAsJsonAsync("/items/1", new { }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Edit_rejects_priority_out_of_range()
    {
        // Act
        var response = await _client.PutAsJsonAsync(
            "/items/1",
            new { title = "valid", priority = 6 },
            Ct
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Edit_rejects_note_over_2000_characters()
    {
        // Act
        var response = await _client.PutAsJsonAsync(
            "/items/1",
            new { title = "valid", note = new string('x', 2001) },
            Ct
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
