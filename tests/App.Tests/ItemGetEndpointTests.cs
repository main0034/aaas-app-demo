using System.Net;
using System.Net.Http.Json;
using App.Data;
using App.Tests.Postgres;

namespace App.Tests;

public sealed class ItemGetEndpointTests(TemplateDatabase template) : EndpointTest(template)
{
    [Fact]
    public async Task Returns_the_item_with_that_id_only()
    {
        // Arrange
        await SeedAsync(new Item { Title = "first" }, new Item { Title = "second", Priority = 4 });

        // Act
        var item = await Client.GetFromJsonAsync<Item>("/items/2", Ct);

        // Assert
        Assert.Equal(2, item!.Id);
        Assert.Equal("second", item.Title);
        Assert.Equal(4, item.Priority);
    }

    [Fact]
    public async Task An_unknown_id_is_404()
    {
        // Arrange
        await SeedAsync(new Item { Title = "first" });

        // Act
        var response = await Client.GetAsync("/items/2", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
