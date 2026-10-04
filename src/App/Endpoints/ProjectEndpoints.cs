using System.ComponentModel.DataAnnotations;
using App.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace App.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/projects", async (ProjectIn input, AppDbContext ctx, CancellationToken ct) =>
        {
            var name = input.Name.Trim();
            if (name.Length == 0)
            {
                return Results.BadRequest();
            }
            var project = new Project { Name = name };
            ctx.Projects.Add(project);
            try
            {
                await ctx.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return Results.Conflict();
            }
            return Results.Created($"/projects/{project.Id}", new ProjectOut(project.Id, project.Name));
        });

        app.MapGet("/projects", async (AppDbContext ctx, CancellationToken ct) =>
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

            return rows.Select(p => ToSummary(p.Id, p.Name, p.Total, p.Done));
        });

        app.MapGet("/projects/{id:int}", async (int id, AppDbContext ctx, CancellationToken ct) =>
        {
            var row = await ctx.Projects
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    Total = p.Items.Count(),
                    Done = p.Items.Count(i => i.IsDone),
                    Items = p.Items
                        .OrderBy(i => i.IsDone)
                        .ThenByDescending(i => i.Id)
                        .ToList(),
                })
                .FirstOrDefaultAsync(ct);

            if (row is null)
            {
                return Results.NotFound();
            }

            var summary = ToSummary(row.Id, row.Name, row.Total, row.Done);
            return Results.Ok(new ProjectDetailOut(
                summary.Id, summary.Name, summary.Total, summary.Done, summary.PercentDone, row.Items));
        });

        app.MapDelete("/projects/{id:int}", async (int id, AppDbContext ctx, CancellationToken ct) =>
        {
            var project = await ctx.Projects.FindAsync([id], ct);
            if (project is null)
            {
                return Results.NotFound();
            }

            var hasItems = await ctx.Items.AnyAsync(i => i.ProjectId == id, ct);
            if (hasItems)
            {
                return Results.Conflict();
            }

            ctx.Projects.Remove(project);
            await ctx.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapPut("/items/{id:int}/project", async (int id, ProjectAssignIn input, AppDbContext ctx, CancellationToken ct) =>
        {
            var item = await ctx.Items.FindAsync([id], ct);
            if (item is null)
            {
                return Results.NotFound();
            }

            if (input.ProjectId is not null)
            {
                var projectExists = await ctx.Projects.AnyAsync(p => p.Id == input.ProjectId, ct);
                if (!projectExists)
                {
                    return Results.BadRequest();
                }
            }

            item.ProjectId = input.ProjectId;
            await ctx.SaveChangesAsync(ct);
            return Results.Ok(item);
        });

        return app;
    }

    private static ProjectSummaryOut ToSummary(int id, string name, int total, int done) =>
        new(id, name, total, done, total == 0 ? 0 : done * 100 / total);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;
}

public sealed record ProjectIn(
    [property: Required, StringLength(100, MinimumLength = 1)] string Name);

public sealed record ProjectAssignIn(int? ProjectId);

public sealed record ProjectSummaryOut(int Id, string Name, int Total, int Done, int PercentDone);

public sealed record ProjectOut(int Id, string Name);

public sealed record ProjectDetailOut(int Id, string Name, int Total, int Done, int PercentDone, IEnumerable<Item> Items);
