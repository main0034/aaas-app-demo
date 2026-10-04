namespace App.Data;

public sealed class ItemTag
{
    public int ItemId { get; set; }
    public required string Tag { get; set; }

    public Item Item { get; set; } = null!;
}
