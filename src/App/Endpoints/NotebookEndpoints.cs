using System.ComponentModel.DataAnnotations;
using App.Assistant;
using App.Data;
using App.Notebook;
using Microsoft.EntityFrameworkCore;

namespace App.Endpoints;

public static class NotebookEndpoints
{
    // PNG file signature: the first 8 bytes of every valid PNG.
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private const int MaxImageBytes = 2 * 1024 * 1024;

    public static IEndpointRouteBuilder MapNotebookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notebook/state", GetState);
        app.MapPost("/notebook/lines", PostLine);
        app.MapPost("/notebook/strokes", PostStroke);
        app.MapPost("/notebook/ink", PostInk);
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
    // next to an item. Check mark strokes matched to an item are marked read_at
    // immediately so they are not treated as pending handwriting.
    private static async Task<IResult> PostStroke(
        StrokeIn input, AppDbContext ctx, CancellationToken ct)
    {
        var stroke = new Stroke { Points = input.Points.Select(p => new StrokePoint(p.X, p.Y)).ToList() };
        ctx.Strokes.Add(stroke);
        await ctx.SaveChangesAsync(ct);

        if (!NotebookLogic.IsCheckMark(stroke.Points))
        {
            return Results.Ok(new { strokeId = stroke.Id, isCheckMark = false, readAt = stroke.ReadAt });
        }

        double left = stroke.Points.Min(p => p.X);
        double top = stroke.Points.Min(p => p.Y);
        double right = stroke.Points.Max(p => p.X);
        double bottom = stroke.Points.Max(p => p.Y);

        var items = await ctx.Items.Where(i => !i.IsDone).ToListAsync(ct);
        var matched = NotebookLogic.FindNearestItem(items, left, top, right, bottom);

        // Mark the stroke as read and optionally tick the matched item.
        stroke.ReadAt = DateTimeOffset.UtcNow;
        if (matched != null && !matched.IsDone)
        {
            matched.IsDone = true;
            matched.DoneAt = DateTimeOffset.UtcNow;
        }

        await ctx.SaveChangesAsync(ct);

        return Results.Ok(new { strokeId = stroke.Id, isCheckMark = true, itemId = (int?)matched?.Id, readAt = stroke.ReadAt });
    }

    // Receives pending stroke ids and a PNG of the pending ink. Sends the
    // image to the handwriting reader, then creates an item or question from
    // the recognised text. On success the strokes are marked read.
    //
    // Order of operations: validate image → call reader → (return early if
    // nothing legible or reader error) → validate stroke ids → create
    // entity → mark strokes read. This ordering lets "nothing legible" and
    // PNG-validation tests pass without a database.
    private static async Task<IResult> PostInk(
        InkIn input, AppDbContext ctx, IHandwritingReader reader,
        IAssistant assistant, CancellationToken ct)
    {
        // 1. Validate the image.
        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(input.Image);
        }
        catch (FormatException)
        {
            return Results.Problem(
                detail: "Image is not valid base64.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (imageBytes.Length > MaxImageBytes)
        {
            return Results.Problem(
                detail: "Image must not exceed 2 MB.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (imageBytes.Length < PngSignature.Length ||
            !imageBytes[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            return Results.Problem(
                detail: "Image must be a PNG.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // 2. Call the handwriting reader.
        var hwResult = await reader.ReadAsync(input.Image, ct);
        if (hwResult.Error != null)
        {
            return Results.Problem(
                detail: hwResult.Error,
                statusCode: StatusCodes.Status502BadGateway);
        }

        // Empty text means nothing legible — strokes stay pending.
        var text = hwResult.Text ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            return Results.Ok(new { type = "nothing", message = "No legible text found. Add more strokes and try again." });
        }

        // 3. Validate stroke ids against the database (all-or-nothing).
        var strokes = await ctx.Strokes
            .Where(s => input.StrokeIds.Contains(s.Id))
            .ToListAsync(ct);

        var foundIds = strokes.Select(s => s.Id).ToHashSet();
        var missingIds = input.StrokeIds.Where(id => !foundIds.Contains(id)).ToList();
        if (missingIds.Count > 0)
        {
            return Results.Problem(
                detail: $"Stroke {string.Join(", ", missingIds)} not found.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var alreadyRead = strokes.Where(s => s.ReadAt != null).ToList();
        if (alreadyRead.Count > 0)
        {
            return Results.Problem(
                detail: $"Stroke {string.Join(", ", alreadyRead.Select(s => s.Id))} has already been read.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // 4. Classify and create.
        var kind = NotebookLogic.ClassifyLine(text);
        if (kind == LineKind.Invalid)
        {
            // Treat unclassifiable text as nothing legible.
            return Results.Ok(new { type = "nothing", message = "No legible text found. Add more strokes and try again." });
        }

        var now = DateTimeOffset.UtcNow;

        // 5. Mark strokes read (regardless of item/question outcome).
        foreach (var s in strokes)
        {
            s.ReadAt = now;
        }

        if (kind == LineKind.Item)
        {
            var item = new Item
            {
                Title = text.Trim(),
                X = input.BoundingLeft,
                Y = input.BoundingTop,
                Width = input.BoundingWidth,
                Height = input.BoundingHeight,
                Handwritten = true,
            };
            ctx.Items.Add(item);
            try
            {
                await ctx.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Undo the ReadAt marks so strokes remain pending.
                foreach (var s in strokes)
                {
                    s.ReadAt = null;
                }

                return Results.Problem(
                    detail: $"An item named \"{item.Title}\" already exists. Each item must have a unique title.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            return Results.Created($"/items/{item.Id}", new { type = "item", item });
        }

        // Question
        var question = new Question
        {
            Text = text.Trim(),
            X = input.BoundingLeft,
            Y = input.BoundingTop,
            Width = input.BoundingWidth,
            Height = input.BoundingHeight,
            Handwritten = true,
            AskedAt = now,
        };
        ctx.Questions.Add(question);
        await ctx.SaveChangesAsync(ct);

        var aiResult = await assistant.AskAsync(question.Text, ct);
        question.Answer = aiResult.Answer;
        question.Error = aiResult.Error;
        question.AnsweredAt = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync(ct);

        return Results.Created($"/notebook/questions/{question.Id}", new { type = "question", question });
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

public sealed record InkIn(
    [property: Required, MinLength(1)] List<int> StrokeIds,
    [property: Required] string Image,
    double BoundingLeft,
    double BoundingTop,
    double BoundingWidth,
    double BoundingHeight);
