using System.ComponentModel.DataAnnotations;

namespace App.Data;

public sealed class Question
{
    public int Id { get; set; }

    [MaxLength(2000)]
    public required string Text { get; set; }

    public double X { get; set; }
    public double Y { get; set; }

    // Null for questions created before the handwriting feature.
    public double? Width { get; set; }
    public double? Height { get; set; }

    // True when the question was created from handwritten ink rather than typed text.
    public bool Handwritten { get; set; }

    public string? Answer { get; set; }

    [MaxLength(500)]
    public string? Error { get; set; }

    public DateTimeOffset AskedAt { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
}
