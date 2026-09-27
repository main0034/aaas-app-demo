using System.ComponentModel.DataAnnotations;

namespace App.Data;

public sealed class Item
{
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }

    [Range(1, 5)]
    public int? Priority { get; set; }

    public bool IsDone { get; set; }

    public DateTimeOffset? DoneAt { get; set; }

    // Position and size on the notebook sheet; null for items created before the notebook feature.
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }

    // True when the item was created from handwritten ink rather than typed text.
    public bool Handwritten { get; set; }
}
