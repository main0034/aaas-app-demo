using App.Data;
using App.Notebook;

namespace App.Tests.Notebook;

public sealed class NotebookLogicTests
{
    // ── ClassifyLine ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("What is the weather?", LineKind.Question)]
    [InlineData("  How many items?  ", LineKind.Question)]
    [InlineData("Why?", LineKind.Question)]
    public void ClassifyLine_question_ends_with_question_mark(string text, LineKind expected)
    {
        Assert.Equal(expected, NotebookLogic.ClassifyLine(text));
    }

    [Fact]
    public void ClassifyLine_lone_question_mark_is_invalid()
    {
        Assert.Equal(LineKind.Invalid, NotebookLogic.ClassifyLine("?"));
        Assert.Equal(LineKind.Invalid, NotebookLogic.ClassifyLine("  ?  "));
    }

    [Fact]
    public void ClassifyLine_empty_or_whitespace_is_invalid()
    {
        Assert.Equal(LineKind.Invalid, NotebookLogic.ClassifyLine(""));
        Assert.Equal(LineKind.Invalid, NotebookLogic.ClassifyLine("   "));
    }

    [Theory]
    [InlineData("Buy milk")]
    [InlineData("  Fix bug  ")]
    [InlineData("hello world")]
    public void ClassifyLine_plain_text_is_item(string text)
    {
        Assert.Equal(LineKind.Item, NotebookLogic.ClassifyLine(text));
    }

    // ── IsCheckMark ───────────────────────────────────────────────────────────

    // A clean check: down-right to valley, then up-right.
    [Fact]
    public void IsCheckMark_clean_check_returns_true()
    {
        var points = MakeCheck(startX: 0, valleyX: 20, valleyY: 30, endX: 80, endY: 5);
        Assert.True(NotebookLogic.IsCheckMark(points));
    }

    // Check with slight jitter still passes.
    [Fact]
    public void IsCheckMark_jittered_check_returns_true()
    {
        var points = new List<StrokePoint>
        {
            new(0, 0), new(3, 5), new(5, 10), new(8, 14), new(10, 18),
            new(14, 23), new(18, 28), new(22, 30),   // valley near index 7/12
            new(28, 25), new(35, 20), new(44, 14), new(55, 8), new(66, 3),
        };
        Assert.True(NotebookLogic.IsCheckMark(points));
    }

    // A straight horizontal line — valley is at an end.
    [Fact]
    public void IsCheckMark_horizontal_line_returns_false()
    {
        var points = Enumerable.Range(0, 10)
            .Select(i => new StrokePoint(i * 10.0, 5.0))
            .ToList();
        Assert.False(NotebookLogic.IsCheckMark(points));
    }

    // A dot — bounding box too small.
    [Fact]
    public void IsCheckMark_dot_returns_false()
    {
        var points = new List<StrokePoint> { new(5, 5), new(6, 6), new(5, 7) };
        Assert.False(NotebookLogic.IsCheckMark(points));
    }

    // A downward stroke with no rightward return — valley at the end.
    [Fact]
    public void IsCheckMark_straight_down_returns_false()
    {
        var points = Enumerable.Range(0, 10)
            .Select(i => new StrokePoint(5.0, i * 10.0))
            .ToList();
        Assert.False(NotebookLogic.IsCheckMark(points));
    }

    // A circle-ish shape — the "right half" goes leftward.
    [Fact]
    public void IsCheckMark_circle_returns_false()
    {
        var points = new List<StrokePoint>();
        for (int i = 0; i <= 12; i++)
        {
            double angle = Math.PI * i / 12; // 0 → π (top → bottom → top via left)
            points.Add(new StrokePoint(50 + 40 * Math.Cos(angle), 40 * Math.Sin(angle)));
        }
        Assert.False(NotebookLogic.IsCheckMark(points));
    }

    // A scribble with large bounding box but valley at the start.
    [Fact]
    public void IsCheckMark_scribble_returns_false()
    {
        var points = new List<StrokePoint>
        {
            new(0, 100), new(20, 50), new(40, 80), new(60, 30), new(80, 70),
            new(100, 20), new(80, 50), new(60, 90), new(40, 40), new(20, 60),
        };
        Assert.False(NotebookLogic.IsCheckMark(points));
    }

    // Fewer than 3 points.
    [Fact]
    public void IsCheckMark_too_few_points_returns_false()
    {
        Assert.False(NotebookLogic.IsCheckMark([]));
        Assert.False(NotebookLogic.IsCheckMark([new(0, 0), new(10, 10)]));
    }

    // ── FindNearestItem ───────────────────────────────────────────────────────

    [Fact]
    public void FindNearestItem_check_just_left_of_text_matches()
    {
        var item = MakeItem(x: 100, y: 10, w: 80, h: 20);
        // Check bounding box ends 30 px to the left of item
        var result = NotebookLogic.FindNearestItem([item], 40, 12, 70, 28);
        Assert.Equal(item.Id, result?.Id);
    }

    [Fact]
    public void FindNearestItem_check_just_right_of_text_matches()
    {
        var item = MakeItem(x: 100, y: 10, w: 80, h: 20);
        // Check starts 30 px to the right of item's right edge (100+80=180)
        var result = NotebookLogic.FindNearestItem([item], 190, 12, 230, 28);
        Assert.Equal(item.Id, result?.Id);
    }

    [Fact]
    public void FindNearestItem_check_too_far_left_no_match()
    {
        var item = MakeItem(x: 100, y: 10, w: 80, h: 20);
        // Check ends 110 px to the left of item — beyond 100 px threshold
        var result = NotebookLogic.FindNearestItem([item], 0, 12, -10, 28);
        Assert.Null(result);
    }

    [Fact]
    public void FindNearestItem_wrong_line_no_match()
    {
        var item = MakeItem(x: 100, y: 10, w: 80, h: 20);
        // Check's vertical centre (150) is far below item's line (10–30)
        var result = NotebookLogic.FindNearestItem([item], 40, 140, 70, 160);
        Assert.Null(result);
    }

    [Fact]
    public void FindNearestItem_two_candidates_returns_nearest()
    {
        // Two items on the same Y range; check is to the left of both.
        var near = MakeItem(id: 1, x: 100, y: 10, w: 80, h: 20);
        var far = MakeItem(id: 2, x: 200, y: 10, w: 80, h: 20);
        // Check ends at x=70; near item starts at x=100 (gap 30), far at x=200 (gap 130, out of range)
        var result = NotebookLogic.FindNearestItem([near, far], 40, 12, 70, 28);
        Assert.Equal(near.Id, result?.Id);
    }

    [Fact]
    public void FindNearestItem_item_without_position_is_ignored()
    {
        var item = new Item { Id = 1, Title = "no position" };
        var result = NotebookLogic.FindNearestItem([item], 40, 12, 70, 28);
        Assert.Null(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<StrokePoint> MakeCheck(
        double startX, double valleyX, double valleyY, double endX, double endY)
    {
        var pts = new List<StrokePoint>();
        // Left side: straight line from (startX, 0) to (valleyX, valleyY)
        for (int i = 0; i <= 5; i++)
        {
            double t = i / 5.0;
            pts.Add(new StrokePoint(startX + t * (valleyX - startX), t * valleyY));
        }
        // Right side: straight line from valley to end
        for (int i = 1; i <= 5; i++)
        {
            double t = i / 5.0;
            pts.Add(new StrokePoint(valleyX + t * (endX - valleyX), valleyY + t * (endY - valleyY)));
        }
        return pts;
    }

    private static Item MakeItem(int id = 1, double x = 0, double y = 0, double w = 80, double h = 20) =>
        new() { Id = id, Title = $"item-{id}", X = x, Y = y, Width = w, Height = h };
}
