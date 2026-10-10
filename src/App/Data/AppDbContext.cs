using Microsoft.EntityFrameworkCore;

namespace App.Data;

// The data model. Change it here, then run `dotnet ef migrations add <Name>`.
// Never change the schema any other way - see AGENT.md.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // snake_case in the database, PascalCase in C#. Explicit rather than a
        // naming-convention package: one less dependency, and the SQL an agent
        // reads in a migration matches the table it will see in psql.
        modelBuilder.Entity<Item>(e =>
        {
            e.ToTable("items");
            e.Property(i => i.Id).HasColumnName("id");
            e.Property(i => i.Title).HasColumnName("title");
            e.Property(i => i.Note).HasColumnName("note");
            e.Property(i => i.Priority).HasColumnName("priority");
            e.Property(i => i.IsDone).HasColumnName("is_done");
            e.Property(i => i.DoneAt).HasColumnName("done_at");
            e.Property(i => i.DueDate).HasColumnName("due_date");
            e.Property(i => i.ProjectId).HasColumnName("project_id");
            e.HasIndex(i => i.Title).IsUnique();
            e.HasOne(i => i.Project)
                .WithMany(p => p.Items)
                .HasForeignKey(i => i.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("projects");
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.Name).HasColumnName("name").HasMaxLength(100);
        });
    }
}
