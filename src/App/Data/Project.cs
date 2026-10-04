using System.ComponentModel.DataAnnotations;

namespace App.Data;

public sealed class Project
{
    public int Id { get; set; }

    [MaxLength(100)]
    public required string Name { get; set; }

    public ICollection<Item> Items { get; } = [];
}
