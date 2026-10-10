using App.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Endpoints;

public static class ProjectsEndpoints
{
    public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/projects", CreateProject);
        app.MapGet("/projects", ListProjects);
        app.MapGet("/projects/{id:int}", GetProject);
        app.MapDelete("/projects/{id:int}", DeleteProject);
        app.MapPut("/items/{id:int}/project", AssignItemProject);
        return app;
    }

    private static async Task<IResult> CreateProject(ProjectIn input, AppDbContext ctx, CancellationToken ct)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > 100)
        {
            return Results.BadRequest();
        }

        var nameLower = name.ToLower();
        if (await ctx.Projects.AnyAsync(p => p.Name.ToLower() == nameLower, ct))
        {
            return Results.Conflict();
        }

        var project = new Project { Name = name };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync(ct);
        return Results.Created($"/projects/{project.Id}", new ProjectCreated(project.Id, project.Name));
    }

    private static async Task<IResult> ListProjects(AppDbContext ctx, CancellationToken ct)
    {
        var rows = await ctx.Projects
            .OrderBy(p => p.Name.ToLower())
            .Select(p => new
            {
                p.Id,
                p.Name,
                Total = p.Items.Count(),
                Done = p.Items.Count(i => i.IsDone),
            })
            .ToListAsync(ct);

        return Results.Ok(rows.Select(p => new ProjectSummary(
            p.Id, p.Name, p.Total, p.Done,
            p.Total == 0 ? 0 : p.Done * 100 / p.Total)));
    }

    private static async Task<IResult> GetProject(int id, AppDbContext ctx, CancellationToken ct)
    {
        var project = await ctx.Projects.FindAsync([id], ct);
        if (project is null)
        {
            return Results.NotFound();
        }

        var items = await ctx.Items.AsNoTracking()
            .Where(i => i.ProjectId == id)
            .OrderBy(i => i.IsDone ? 1 : 0)
            .ThenByDescending(i => i.Id)
            .ToListAsync(ct);

        int total = items.Count;
        int done = items.Count(i => i.IsDone);
        int percentDone = total == 0 ? 0 : done * 100 / total;

        return Results.Ok(new ProjectDetail(project.Id, project.Name, total, done, percentDone, items));
    }

    private static async Task<IResult> DeleteProject(int id, AppDbContext ctx, CancellationToken ct)
    {
        var project = await ctx.Projects.FindAsync([id], ct);
        if (project is null)
        {
            return Results.NotFound();
        }

        if (await ctx.Items.AnyAsync(i => i.ProjectId == id, ct))
        {
            return Results.Conflict();
        }

        ctx.Projects.Remove(project);
        await ctx.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AssignItemProject(int id, ItemProjectIn input, AppDbContext ctx, CancellationToken ct)
    {
        var item = await ctx.Items.FindAsync([id], ct);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (input.ProjectId is not null && !await ctx.Projects.AnyAsync(p => p.Id == input.ProjectId, ct))
        {
            return Results.BadRequest();
        }

        item.ProjectId = input.ProjectId;
        await ctx.SaveChangesAsync(ct);
        return Results.Ok(item);
    }
}

public sealed record ProjectIn(string? Name);
public sealed record ItemProjectIn(int? ProjectId);
public sealed record ProjectCreated(int Id, string Name);
public sealed record ProjectSummary(int Id, string Name, int Total, int Done, int PercentDone);
public sealed record ProjectDetail(int Id, string Name, int Total, int Done, int PercentDone, List<Item> Items);
