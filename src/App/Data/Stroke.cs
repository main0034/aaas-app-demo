namespace App.Data;

public sealed class Stroke
{
    public int Id { get; set; }
    public List<StrokePoint> Points { get; set; } = [];
}

public record struct StrokePoint(double X, double Y);
