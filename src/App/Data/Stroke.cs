namespace App.Data;

public sealed class Stroke
{
    public int Id { get; set; }
    public List<StrokePoint> Points { get; set; } = [];

    // Set when the stroke has been read by the handwriting reader (or
    // immediately for check marks that matched an item). Null means pending.
    public DateTimeOffset? ReadAt { get; set; }
}

public record struct StrokePoint(double X, double Y);
