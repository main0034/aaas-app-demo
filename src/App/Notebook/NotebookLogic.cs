using App.Data;

namespace App.Notebook;

public enum LineKind
{
    Item,
    Question,
    Invalid,
}

public static class NotebookLogic
{
    // A line is a question if its trimmed text ends with '?' and is not just '?'.
    public static LineKind ClassifyLine(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return LineKind.Invalid;
        }

        if (trimmed == "?")
        {
            return LineKind.Invalid;
        }

        if (trimmed.EndsWith('?'))
        {
            return LineKind.Question;
        }

        return LineKind.Item;
    }

    // A check mark is one stroke that:
    //   - goes down-and-right to its lowest point (the valley), then up-and-right
    //   - the valley is not at either end (between 15 % and 85 % of the stroke)
    //   - both halves have a meaningful rightward and vertical component (>= 5 px each)
    //   - the bounding box is between 10 and 200 px in each dimension
    public static bool IsCheckMark(IReadOnlyList<StrokePoint> points)
    {
        if (points.Count < 3)
        {
            return false;
        }

        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        int valleyIdx = 0;

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (p.X < minX)
            {
                minX = p.X;
            }

            if (p.X > maxX)
            {
                maxX = p.X;
            }

            if (p.Y < minY)
            {
                minY = p.Y;
            }

            if (p.Y > maxY)
            {
                maxY = p.Y;
            }

            if (p.Y > points[valleyIdx].Y)
            {
                valleyIdx = i;
            }
        }

        double bboxW = maxX - minX;
        double bboxH = maxY - minY;
        if (bboxW < 10 || bboxH < 10 || bboxW > 200 || bboxH > 200)
        {
            return false;
        }

        // Valley must not be at the very start or end.
        double valleyFrac = (double)valleyIdx / (points.Count - 1);
        if (valleyFrac < 0.15 || valleyFrac > 0.85)
        {
            return false;
        }

        var start = points[0];
        var valley = points[valleyIdx];
        var end = points[^1];

        // Left half: must go right (dx >= 5) and down (dy >= 5).
        double leftDx = valley.X - start.X;
        double leftDy = valley.Y - start.Y;
        if (leftDx < 5 || leftDy < 5)
        {
            return false;
        }

        // Right half: must go right (dx >= 5) and up (dy <= -5).
        double rightDx = end.X - valley.X;
        double rightDy = end.Y - valley.Y;
        if (rightDx < 5 || rightDy > -5)
        {
            return false;
        }

        return true;
    }

    // Returns the item nearest to the check bounding box, or null if none qualifies.
    // "Nearest" means the item whose text boundary is closest horizontally, among
    // items whose line the check's vertical centre falls within.
    public static Item? FindNearestItem(
        IEnumerable<Item> items,
        double checkLeft, double checkTop,
        double checkRight, double checkBottom)
    {
        const double MaxProximity = 100.0;
        const double OverlapAllowance = 15.0;

        double checkCenterY = (checkTop + checkBottom) / 2;

        Item? best = null;
        double bestDist = double.MaxValue;

        foreach (var item in items)
        {
            if (item.X is null || item.Y is null || item.Width is null || item.Height is null)
            {
                continue;
            }

            double itemLeft = item.X.Value;
            double itemTop = item.Y.Value;
            double itemRight = itemLeft + item.Width.Value;
            double itemBottom = itemTop + item.Height.Value;

            // The check's vertical centre must fall within the item's line.
            if (checkCenterY < itemTop || checkCenterY > itemBottom)
            {
                continue;
            }

            // Just left of text: check's right edge ends within 100 px before the text
            // (or overlaps up to 15 px into it).
            bool leftOfText = checkRight >= itemLeft - MaxProximity
                           && checkRight <= itemLeft + OverlapAllowance;

            // Just right of text: check's left edge starts within 100 px after the text
            // (or overlaps up to 15 px into it from the right).
            bool rightOfText = checkLeft >= itemRight - OverlapAllowance
                            && checkLeft <= itemRight + MaxProximity;

            if (!leftOfText && !rightOfText)
            {
                continue;
            }

            double dist = leftOfText
                ? Math.Abs(checkRight - itemLeft)
                : Math.Abs(checkLeft - itemRight);

            if (dist < bestDist)
            {
                best = item;
                bestDist = dist;
            }
        }

        return best;
    }
}
