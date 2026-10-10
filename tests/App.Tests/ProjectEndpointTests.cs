using System.Net;
using System.Net.Http.Json;
using App.Data;
using App.Endpoints;
using App.Tests.Postgres;

namespace App.Tests;

public sealed class ProjectEndpointTests(TemplateDatabase template) : EndpointTest(template)
{
    private async Task<ProjectCreated> CreateProjectAsync(string name)
    {
        var response = await Client.PostAsJsonAsync("/projects", new { name }, Ct);
        return (await response.Content.ReadFromJsonAsync<ProjectCreated>(Ct))!;
    }

    private async Task AssignItemAsync(int itemId, int? projectId) =>
        await Client.PutAsJsonAsync($"/items/{itemId}/project", new { projectId }, Ct);

    // ── POST /projects ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_project_returns_201_with_id_and_name()
    {
        // Act
        var response = await Client.PostAsJsonAsync("/projects", new { name = "Garden" }, Ct);
        var body = await response.Content.ReadFromJsonAsync<ProjectCreated>(Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body.Id > 0);
        Assert.Equal("Garden", body.Name);
    }

    [Fact]
    public async Task Create_project_trims_name_and_stores_trimmed()
    {
        // Act
        var response = await Client.PostAsJsonAsync("/projects", new { name = "  Work  " }, Ct);
        var body = await response.Content.ReadFromJsonAsync<ProjectCreated>(Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Work", body!.Name);
    }

    [Fact]
    public async Task Create_project_empty_name_returns_400()
    {
        // Act
        var response = await Client.PostAsJsonAsync("/projects", new { name = "" }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_project_whitespace_only_name_returns_400()
    {
        // Act
        var response = await Client.PostAsJsonAsync("/projects", new { name = "   " }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_project_name_over_100_chars_returns_400()
    {
        // Act
        var response = await Client.PostAsJsonAsync(
            "/projects",
            new { name = new string('x', 101) },
            Ct
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_project_name_exactly_100_chars_returns_201()
    {
        // Act
        var response = await Client.PostAsJsonAsync(
            "/projects",
            new { name = new string('x', 100) },
            Ct
        );

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_project_duplicate_name_returns_409()
    {
        // Arrange
        await CreateProjectAsync("Garden");

        // Act
        var response = await Client.PostAsJsonAsync("/projects", new { name = "Garden" }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_project_duplicate_name_case_insensitive_returns_409()
    {
        // Arrange
        await CreateProjectAsync("Garden");

        // Act
        var response = await Client.PostAsJsonAsync("/projects", new { name = "GARDEN" }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── PUT /items/{id}/project ─────────────────────────────────────────────────

    [Fact]
    public async Task Assign_item_to_project_returns_200_with_item_and_project_id()
    {
        // Arrange
        await SeedAsync(new Item { Title = "Buy seeds" });
        var project = await CreateProjectAsync("Garden");
        var item = (await Client.GetFromJsonAsync<List<Item>>("/items", Ct))!.Single();

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/items/{item.Id}/project",
            new { projectId = project.Id },
            Ct
        );
        var body = await response.Content.ReadFromJsonAsync<Item>(Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(project.Id, body!.ProjectId);
        Assert.Equal("Buy seeds", body.Title);
    }

    [Fact]
    public async Task Assign_item_to_nonexistent_project_returns_400()
    {
        // Arrange
        await SeedAsync(new Item { Title = "Buy seeds" });
        var item = (await Client.GetFromJsonAsync<List<Item>>("/items", Ct))!.Single();

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/items/{item.Id}/project",
            new { projectId = 9999 },
            Ct
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Assign_nonexistent_item_to_project_returns_404()
    {
        // Act
        var response = await Client.PutAsJsonAsync(
            "/items/9999/project",
            new { projectId = (int?)null },
            Ct
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Remove_item_from_project_clears_project_id()
    {
        // Arrange
        await SeedAsync(new Item { Title = "Buy seeds" });
        var project = await CreateProjectAsync("Garden");
        var item = (await Client.GetFromJsonAsync<List<Item>>("/items", Ct))!.Single();
        await AssignItemAsync(item.Id, project.Id);

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/items/{item.Id}/project",
            new { projectId = (int?)null },
            Ct
        );
        var body = await response.Content.ReadFromJsonAsync<Item>(Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(body!.ProjectId);
    }

    // ── GET /projects ───────────────────────────────────────────────────────────

    [Fact]
    public async Task List_projects_returns_stats_and_sorts_by_name_case_insensitively()
    {
        // Arrange
        await SeedAsync(
            new Item { Title = "z-done", IsDone = true },
            new Item { Title = "z-open" },
            new Item { Title = "a-open" }
        );
        var garden = await CreateProjectAsync("Garden");
        var alpha = await CreateProjectAsync("alpha");
        var items = (await Client.GetFromJsonAsync<List<Item>>("/items", Ct))!;
        var zDone = items.Single(i => i.Title == "z-done");
        var zOpen = items.Single(i => i.Title == "z-open");
        var aOpen = items.Single(i => i.Title == "a-open");
        await AssignItemAsync(zDone.Id, garden.Id);
        await AssignItemAsync(zOpen.Id, garden.Id);
        await AssignItemAsync(aOpen.Id, alpha.Id);

        // Act
        var list = await Client.GetFromJsonAsync<List<ProjectSummary>>("/projects", Ct);

        // Assert
        Assert.Equal(2, list!.Count);
        // "alpha" < "Garden" case-insensitively
        Assert.Equal("alpha", list[0].Name);
        Assert.Equal(1, list[0].Total);
        Assert.Equal(0, list[0].Done);
        Assert.Equal(0, list[0].PercentDone);
        Assert.Equal("Garden", list[1].Name);
        Assert.Equal(2, list[1].Total);
        Assert.Equal(1, list[1].Done);
        Assert.Equal(50, list[1].PercentDone);
    }

    [Fact]
    public async Task List_projects_empty_project_shows_zero_percent()
    {
        // Arrange
        await CreateProjectAsync("Empty");

        // Act
        var list = (await Client.GetFromJsonAsync<List<ProjectSummary>>("/projects", Ct))!;

        // Assert
        Assert.Single(list);
        Assert.Equal(0, list[0].Total);
        Assert.Equal(0, list[0].Done);
        Assert.Equal(0, list[0].PercentDone);
    }

    [Fact]
    public async Task List_projects_percent_done_truncates_not_rounds()
    {
        // Arrange
        // 1 done out of 3 = 33.3% → 33 (floor), not 34
        await SeedAsync(
            new Item { Title = "a", IsDone = true },
            new Item { Title = "b" },
            new Item { Title = "c" }
        );
        var project = await CreateProjectAsync("Sprint");
        var items = await Client.GetFromJsonAsync<List<Item>>("/items", Ct);
        foreach (var item in items!)
        {
            await AssignItemAsync(item.Id, project.Id);
        }

        // Act
        var list = await Client.GetFromJsonAsync<List<ProjectSummary>>("/projects", Ct);

        // Assert
        Assert.Equal(33, list!.Single().PercentDone);
    }

    // ── GET /projects/{id} ──────────────────────────────────────────────────────

    [Fact]
    public async Task Get_project_lists_open_items_before_done_newest_first_within_each_group()
    {
        // Arrange
        await SeedAsync(
            new Item { Title = "open-old" },
            new Item { Title = "done-old", IsDone = true },
            new Item { Title = "open-new" },
            new Item { Title = "done-new", IsDone = true },
            new Item { Title = "not-in-project" }
        );
        var project = await CreateProjectAsync("Work");
        var all = await Client.GetFromJsonAsync<List<Item>>("/items", Ct);
        foreach (var item in all!.Where(i => i.Title != "not-in-project"))
        {
            await AssignItemAsync(item.Id, project.Id);
        }

        // Act
        var detail = await Client.GetFromJsonAsync<ProjectDetail>($"/projects/{project.Id}", Ct);

        // Assert
        Assert.NotNull(detail);
        Assert.Equal(4, detail.Total);
        Assert.Equal(2, detail.Done);
        Assert.Equal(50, detail.PercentDone);
        Assert.Equal(
            ["open-new", "open-old", "done-new", "done-old"],
            detail.Items.Select(i => i.Title).ToArray()
        );
    }

    [Fact]
    public async Task Get_project_not_found_returns_404()
    {
        // Act
        var response = await Client.GetAsync("/projects/9999", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── DELETE /projects/{id} ───────────────────────────────────────────────────

    [Fact]
    public async Task Delete_empty_project_returns_204()
    {
        // Arrange
        var project = await CreateProjectAsync("Temp");

        // Act
        var response = await Client.DeleteAsync($"/projects/{project.Id}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var list = await Client.GetFromJsonAsync<List<ProjectSummary>>("/projects", Ct);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Delete_project_with_items_returns_409()
    {
        // Arrange
        await SeedAsync(new Item { Title = "Buy seeds" });
        var project = await CreateProjectAsync("Garden");
        var item = (await Client.GetFromJsonAsync<List<Item>>("/items", Ct))!.Single();
        await AssignItemAsync(item.Id, project.Id);

        // Act
        var response = await Client.DeleteAsync($"/projects/{project.Id}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_nonexistent_project_returns_404()
    {
        // Act
        var response = await Client.DeleteAsync("/projects/9999", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
