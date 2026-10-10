using System.Text.Json.Serialization;

namespace App.Data;

public sealed class Project
{
    public int Id { get; set; }

    public required string Name { get; set; }

    [JsonIgnore]
    public ICollection<Item> Items { get; set; } = [];
}
