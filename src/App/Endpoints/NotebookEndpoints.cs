using System.ComponentModel.DataAnnotations;
using App.Assistant;
using App.Data;
using App.Notebook;
using Microsoft.EntityFrameworkCore;

namespace App.Endpoints;

public static class NotebookEndpoints
{
    public static IEndpointRouteBuilder MapNotebookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notebook/state", GetState);
        app.MapPost("/notebook/lines", PostLine);
        app.MapPost("/notebook/strokes", PostStroke);
        return app;
    }

    // Returns everything needed to render the notebook on reload.
    private static async Task<IResult> GetState(AppDbContext ctx, CancellationToken ct)
    {
        var items = await ctx.Items.AsNoTracking()
            .OrderBy(i => i.Id)
            .ToListAsync(ct);

        var strokes = await ctx.Strokes.AsNoTracking()
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

        var questions = await ctx.Questions.AsNoTracking()
            .OrderBy(q => q.Id)
            .ToListAsync(ct);

        return Results.Ok(new { items, strokes, questions });
    }

    // Receives a typed line, classifies it, and either creates an item or asks the AI.
    private static async Task<IResult> PostLine(
        LineIn input, AppDbContext ctx, IAssistant assistant, CancellationToken ct)
    {
        var kind = NotebookLogic.ClassifyLine(input.Text);
        if (kind == LineKind.Invalid)
        {
            return Results.Problem(
                detail: "'?' on its own is not a valid line. Type a question (ending with '?') or a plain item.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (kind == LineKind.Item)
        {
            var item = new Item
            {
                Title = input.Text.Trim(),
                X = input.X,
                Y = input.Y,
                Width = input.Width,
                Height = input.Height,
            };
            ctx.Items.Add(item);
            try
            {
                await ctx.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return Results.Problem(
                    detail: $"An item named \"{item.Title}\" already exists. Each item must have a unique title.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            return Results.Created($"/items/{item.Id}", new { type = "item", item });
        }

        // Question
        var question = new Question
        {
            Text = input.Text.Trim(),
            X = input.X,
            Y = input.Y,
            AskedAt = DateTimeOffset.UtcNow,
        };
        ctx.Questions.Add(question);
        await ctx.SaveChangesAsync(ct);

        var result = await assistant.AskAsync(question.Text, ct);
        question.Answer = result.Answer;
        question.Error = result.Error;
        question.AnsweredAt = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync(ct);

        return Results.Created($"/notebook/questions/{question.Id}", new { type = "question", question });
    }

    // Receives a mouse stroke, saves it, and checks whether it is a check mark
    // next to an item.
    private static async Task<IResult> PostStroke(
        StrokeIn input, AppDbContext ctx, CancellationToken ct)
    {
        var stroke = new Stroke { Points = input.Points.Select(p => new StrokePoint(p.X, p.Y)).ToList() };
        ctx.Strokes.Add(stroke);
        await ctx.SaveChangesAsync(ct);

        if (!NotebookLogic.IsCheckMark(stroke.Points))
        {
            return Results.Ok(new { strokeId = stroke.Id, isCheckMark = false });
        }

        double left = stroke.Points.Min(p => p.X);
        double top = stroke.Points.Min(p => p.Y);
        double right = stroke.Points.Max(p => p.X);
        double bottom = stroke.Points.Max(p => p.Y);

        var items = await ctx.Items.Where(i => !i.IsDone).ToListAsync(ct);
        var matched = NotebookLogic.FindNearestItem(items, left, top, right, bottom);

        if (matched is null)
        {
            return Results.Ok(new { strokeId = stroke.Id, isCheckMark = true, itemId = (int?)null });
        }

        if (!matched.IsDone)
        {
            matched.IsDone = true;
            matched.DoneAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync(ct);
        }

        return Results.Ok(new { strokeId = stroke.Id, isCheckMark = true, itemId = (int?)matched.Id });
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // Npgsql error code 23505 = unique_violation
        var inner = ex.InnerException;
        return inner is Npgsql.PostgresException pg && pg.SqlState == "23505";
    }
}

public sealed record LineIn(
    [property: Required, StringLength(2000, MinimumLength = 1)] string Text,
    double X,
    double Y,
    double Width,
    double Height);

public sealed record StrokeIn(
    [property: Required, MinLength(1)] List<PointIn> Points);

public sealed record PointIn(double X, double Y);
