using System.ComponentModel.DataAnnotations;
using App.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Endpoints;

public static class TagEndpoints
{
    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/items/{id:int}/tags", async (int id, AppDbContext ctx, CancellationToken ct) =>
        {
            var exists = await ctx.Items.AnyAsync(i => i.Id == id, ct);
            if (!exists)
            {
                return Results.NotFound();
            }

            var tags = await ctx.ItemTags
                .Where(t => t.ItemId == id)
                .Select(t => t.Tag)
                .OrderBy(t => t)
                .ToListAsync(ct);

            return Results.Ok(tags);
        });

        app.MapPut("/items/{id:int}/tags", async (int id, TagsIn input, AppDbContext ctx, CancellationToken ct) =>
        {
            var normalized = input.Tags!
                .Select(t => (t ?? "").Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            if (normalized.Count > 10 || normalized.Any(t => t.Length == 0 || t.Length > 30))
            {
                return Results.BadRequest();
            }

            var item = await ctx.Items.FindAsync([id], ct);
            if (item is null)
            {
                return Results.NotFound();
            }

            var existing = await ctx.ItemTags.Where(t => t.ItemId == id).ToListAsync(ct);
            ctx.ItemTags.RemoveRange(existing);
            ctx.ItemTags.AddRange(normalized.Select(t => new ItemTag { ItemId = id, Tag = t }));
            await ctx.SaveChangesAsync(ct);

            return Results.Ok(normalized.OrderBy(t => t).ToArray());
        });

        app.MapGet("/tags", async (AppDbContext ctx, CancellationToken ct) =>
            await ctx.ItemTags
                .GroupBy(t => t.Tag)
                .Select(g => new TagSummary(g.Key, g.Count()))
                .OrderBy(t => t.Name)
                .ToListAsync(ct));

        return app;
    }
}

public sealed record TagsIn([property: Required] List<string>? Tags);

public sealed record TagSummary(string Name, int Items);
