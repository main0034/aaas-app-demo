using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace App.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Stroke> Strokes => Set<Stroke>();
    public DbSet<Question> Questions => Set<Question>();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>(e =>
        {
            e.ToTable("items");
            e.Property(i => i.Id).HasColumnName("id");
            e.Property(i => i.Title).HasColumnName("title");
            e.Property(i => i.Note).HasColumnName("note");
            e.Property(i => i.Priority).HasColumnName("priority");
            e.Property(i => i.IsDone).HasColumnName("is_done");
            e.Property(i => i.DoneAt).HasColumnName("done_at");
            e.Property(i => i.X).HasColumnName("x");
            e.Property(i => i.Y).HasColumnName("y");
            e.Property(i => i.Width).HasColumnName("width");
            e.Property(i => i.Height).HasColumnName("height");
            e.Property(i => i.Handwritten).HasColumnName("handwritten").HasDefaultValue(false);
            e.HasIndex(i => i.Title).IsUnique();
        });

        var pointsConverter = new ValueConverter<List<StrokePoint>, string>(
            v => JsonSerializer.Serialize(v, JsonOpts),
            v => JsonSerializer.Deserialize<List<StrokePoint>>(v, JsonOpts) ?? new List<StrokePoint>());

        modelBuilder.Entity<Stroke>(e =>
        {
            e.ToTable("strokes");
            e.Property(s => s.Id).HasColumnName("id");
            e.Property(s => s.Points)
                .HasColumnName("points")
                .HasColumnType("jsonb")
                .HasConversion(pointsConverter);
            e.Property(s => s.ReadAt).HasColumnName("read_at");
        });

        modelBuilder.Entity<Question>(e =>
        {
            e.ToTable("questions");
            e.Property(q => q.Id).HasColumnName("id");
            e.Property(q => q.Text).HasColumnName("text");
            e.Property(q => q.X).HasColumnName("x");
            e.Property(q => q.Y).HasColumnName("y");
            e.Property(q => q.Width).HasColumnName("width");
            e.Property(q => q.Height).HasColumnName("height");
            e.Property(q => q.Handwritten).HasColumnName("handwritten").HasDefaultValue(false);
            e.Property(q => q.Answer).HasColumnName("answer");
            e.Property(q => q.Error).HasColumnName("error");
            e.Property(q => q.AskedAt).HasColumnName("asked_at");
            e.Property(q => q.AnsweredAt).HasColumnName("answered_at");
        });
    }
}
