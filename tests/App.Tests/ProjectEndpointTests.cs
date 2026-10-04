using System.Net;
using System.Net.Http.Json;
using App.Data;
using App.Tests.Postgres;

namespace App.Tests;

public sealed class ProjectEndpointTests(TemplateDatabase template) : EndpointTest(template)
{
    // ── POST /projects ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_project_returns_201_with_id_and_name()
    {
        var response = await Client.PostAsJsonAsync("/projects", new { name = "Garden" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectResponse>(Ct);
        Assert.NotNull(body);
        Assert.True(body.Id > 0);
        Assert.Equal("Garden", body.Name);
    }

    [Fact]
    public async Task Create_project_trims_name()
    {
        var response = await Client.PostAsJsonAsync("/projects", new { name = "  Trimmed  " }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectResponse>(Ct);
        Assert.Equal("Trimmed", body!.Name);
    }

    [Fact]
    public async Task Create_duplicate_name_case_insensitive_returns_409()
    {
        await Client.PostAsJsonAsync("/projects", new { name = "Garden" }, Ct);

        var response = await Client.PostAsJsonAsync("/projects", new { name = "garden" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── GET /projects ───────────────────────────────────────────────────────

    [Fact]
    public async Task List_projects_sorted_by_name_case_insensitively()
    {
        await SeedAsync(
            new Project { Name = "Zebra" },
            new Project { Name = "apple" },
            new Project { Name = "Banana" });

        var projects = await Client.GetFromJsonAsync<List<ProjectSummaryResponse>>("/projects", Ct);

        Assert.Equal(["apple", "Banana", "Zebra"], projects!.Select(p => p.Name));
    }

    [Fact]
    public async Task List_projects_computes_total_done_and_percent()
    {
        await SeedAsync(new Project { Name = "Work" });
        var proj = (await Client.GetFromJsonAsync<List<ProjectSummaryResponse>>("/projects", Ct))!.Single();

        await SeedAsync(
            new Item { Title = "task-a", ProjectId = proj.Id },
            new Item { Title = "task-b", ProjectId = proj.Id, IsDone = true },
            new Item { Title = "task-c", ProjectId = proj.Id, IsDone = true },
            new Item { Title = "task-d", ProjectId = proj.Id, IsDone = true },
            new Item { Title = "unrelated" });

        var updated = (await Client.GetFromJsonAsync<List<ProjectSummaryResponse>>("/projects", Ct))!.Single();

        Assert.Equal(4, updated.Total);
        Assert.Equal(3, updated.Done);
        Assert.Equal(75, updated.PercentDone);
    }

    [Fact]
    public async Task List_projects_percent_floors_not_rounds()
    {
        await SeedAsync(new Project { Name = "X" });
        var proj = (await Client.GetFromJsonAsync<List<ProjectSummaryResponse>>("/projects", Ct))!.Single();

        await SeedAsync(
            new Item { Title = "a", ProjectId = proj.Id, IsDone = true },
            new Item { Title = "b", ProjectId = proj.Id },
            new Item { Title = "c", ProjectId = proj.Id },
            new Item { Title = "d", ProjectId = proj.Id });

        var updated = (await Client.GetFromJsonAsync<List<ProjectSummaryResponse>>("/projects", Ct))!.Single();

        Assert.Equal(4, updated.Total);
        Assert.Equal(1, updated.Done);
        Assert.Equal(25, updated.PercentDone);
    }

    [Fact]
    public async Task List_projects_empty_project_is_zero_percent()
    {
        await SeedAsync(new Project { Name = "Empty" });

        var projects = await Client.GetFromJsonAsync<List<ProjectSummaryResponse>>("/projects", Ct);
        var p = projects!.Single();

        Assert.Equal(0, p.Total);
        Assert.Equal(0, p.Done);
        Assert.Equal(0, p.PercentDone);
    }

    // ── GET /projects/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_project_returns_items_open_first_then_done_newest_first_within_group()
    {
        await SeedAsync(new Project { Name = "Sprint" });
        var proj = (await Client.GetFromJsonAsync<List<ProjectSummaryResponse>>("/projects", Ct))!.Single();

        await SeedAsync(
            new Item { Title = "open-old", ProjectId = proj.Id },
            new Item { Title = "done-old", ProjectId = proj.Id, IsDone = true },
            new Item { Title = "open-new", ProjectId = proj.Id },
            new Item { Title = "done-new", ProjectId = proj.Id, IsDone = true },
            new Item { Title = "other-project" });

        var detail = await Client.GetFromJsonAsync<ProjectDetailResponse>($"/projects/{proj.Id}", Ct);

        Assert.NotNull(detail);
        Assert.Equal("Sprint", detail.Name);
        Assert.Equal(["open-new", "open-old", "done-new", "done-old"], detail.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task Get_project_unknown_returns_404()
    {
        var response = await Client.GetAsync("/projects/999", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── DELETE /projects/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task Delete_empty_project_returns_204()
    {
        await SeedAsync(new Project { Name = "ToDelete" });
        var proj = (await Client.GetFromJsonAsync<List<ProjectSummaryResponse>>("/projects", Ct))!.Single();

        var response = await Client.DeleteAsync($"/projects/{proj.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var list = await Client.GetFromJsonAsync<List<ProjectSummaryResponse>>("/projects", Ct);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Delete_project_with_items_returns_409()
    {
        await SeedAsync(new Project { Name = "Busy" });
        var proj = (await Client.GetFromJsonAsync<List<ProjectSummaryResponse>>("/projects", Ct))!.Single();
        await SeedAsync(new Item { Title = "blocker", ProjectId = proj.Id });

        var response = await Client.DeleteAsync($"/projects/{proj.Id}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_unknown_project_returns_404()
    {
        var response = await Client.DeleteAsync("/projects/999", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PUT /items/{id}/project ─────────────────────────────────────────────

    [Fact]
    public async Task Assign_item_to_project_returns_200_with_item()
    {
        await SeedAsync(new Project { Name = "Alpha" }, new Item { Title = "task" });
        var proj = (await Client.GetFromJsonAsync<List<ProjectSummaryResponse>>("/projects", Ct))!.Single();
        var item = (await Client.GetFromJsonAsync<List<ItemResponse>>("/items", Ct))!.Single();

        var response = await Client.PutAsJsonAsync($"/items/{item.Id}/project", new { projectId = proj.Id }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ItemResponse>(Ct);
        Assert.Equal(proj.Id, updated!.ProjectId);
    }

    [Fact]
    public async Task Assign_item_to_null_removes_from_project()
    {
        await SeedAsync(new Project { Name = "Beta" });
        var proj = (await Client.GetFromJsonAsync<List<ProjectSummaryResponse>>("/projects", Ct))!.Single();
        await SeedAsync(new Item { Title = "task", ProjectId = proj.Id });
        var item = (await Client.GetFromJsonAsync<List<ItemResponse>>("/items", Ct))!.Single();

        var response = await Client.PutAsJsonAsync($"/items/{item.Id}/project", new { projectId = (int?)null }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ItemResponse>(Ct);
        Assert.Null(updated!.ProjectId);
    }

    [Fact]
    public async Task Assign_unknown_item_returns_404()
    {
        var response = await Client.PutAsJsonAsync("/items/999/project", new { projectId = (int?)null }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Assign_item_to_unknown_project_returns_400()
    {
        await SeedAsync(new Item { Title = "task" });
        var item = (await Client.GetFromJsonAsync<List<ItemResponse>>("/items", Ct))!.Single();

        var response = await Client.PutAsJsonAsync($"/items/{item.Id}/project", new { projectId = 9999 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // DTOs matching the shapes the endpoints return.
    private sealed record ProjectResponse(int Id, string Name);

    private sealed record ProjectSummaryResponse(int Id, string Name, int Total, int Done, int PercentDone);

    private sealed record ProjectDetailResponse(int Id, string Name, int Total, int Done, int PercentDone, List<ItemResponse> Items);

    private sealed record ItemResponse(int Id, string Title, int? ProjectId);
}
