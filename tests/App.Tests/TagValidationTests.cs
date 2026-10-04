// Validation runs before the database is touched, so these need none.
// Route behaviour against Postgres is in TagEndpointTests.

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace App.Tests;

public sealed class TagValidationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory
        .WithWebHostBuilder(b => b.UseSetting("PGHOST", ""))
        .CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PutTags_rejects_missing_tags_field()
    {
        var response = await _client.PutAsJsonAsync("/items/1/tags", new { }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutTags_rejects_null_tags_field()
    {
        var response = await _client.PutAsJsonAsync("/items/1/tags", new { tags = (string[]?)null }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutTags_rejects_tag_that_is_empty_after_trim()
    {
        var response = await _client.PutAsJsonAsync("/items/1/tags", new { tags = new[] { "   " } }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutTags_rejects_tag_longer_than_30_characters()
    {
        var response = await _client.PutAsJsonAsync("/items/1/tags", new { tags = new[] { new string('a', 31) } }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutTags_accepts_tag_of_exactly_30_characters_after_trim()
    {
        // Tag length 30 is valid. With no DB the item won't be found (503), but
        // the 400 path never fires — that is the assertion.
        var response = await _client.PutAsJsonAsync("/items/1/tags", new { tags = new[] { new string('a', 30) } }, Ct);

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutTags_rejects_more_than_10_tags_after_dedup()
    {
        var tags = Enumerable.Range(1, 11).Select(i => $"tag{i}").ToArray();
        var response = await _client.PutAsJsonAsync("/items/1/tags", new { tags }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutTags_accepts_exactly_10_tags()
    {
        // 10 tags is valid. Without DB this reaches FindAsync and returns 503, not 400.
        var tags = Enumerable.Range(1, 10).Select(i => $"tag{i}").ToArray();
        var response = await _client.PutAsJsonAsync("/items/1/tags", new { tags }, Ct);

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
