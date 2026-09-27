// Template application.
//
// Deliberately small, but complete: it starts, serves /health without a database,
// and exercises Postgres on the endpoints that need it. That shape matters - the
// container must become healthy even when the database is unreachable, otherwise
// a database problem looks like a deployment failure and you debug the wrong thing.
//
// The same binary applies schema migrations when started with the single
// argument `migrate`. The platform runs that as an init container before each new
// revision starts. See AGENT.md.
//
// Agents extending this file: read AGENT.md first.

using System.ComponentModel.DataAnnotations;
using App;
using App.Assistant;
using App.Data;
using App.Endpoints;
using Microsoft.EntityFrameworkCore;

var migrateOnly = args is ["migrate"];

var builder = WebApplication.CreateBuilder(args);

// The platform injects PORT. Kestrel's own default (8080) is not the contract.
// BIND_ADDRESS lets the local notebook run on localhost only (default 0.0.0.0).
var port = builder.Configuration["PORT"] ?? "8000";
var bindAddress = builder.Configuration["BIND_ADDRESS"] ?? "0.0.0.0";
builder.WebHost.UseUrls($"http://{bindAddress}:{port}");

var appName = builder.Configuration["APP_NAME"] ?? "app";

// EF logs every SQL statement at Information, which buries the lines that matter.
// In migrate mode it is silenced entirely: when replicas race, EF's own queries
// fail harmlessly while another replica holds the lock, and a "fail:" line in an
// init container log reads like the cause of whatever goes wrong next. The runner
// reports real failures itself.
builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore.Database.Command",
    migrateOnly ? LogLevel.None : LogLevel.Warning);

builder.Services.AddSingleton<Database>();
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var db = sp.GetRequiredService<Database>();
    if (db.IsConfigured)
    {
        options.UseNpgsql(db.DataSource);
    }
    else
    {
        options.UseNpgsql().AddInterceptors(new NotConfiguredInterceptor());
    }
});

// Register the AI assistant and handwriting reader. CLI-backed implementations
// are used when enabled; otherwise no-op implementations return a short explanation.
var assistantEnabled = builder.Configuration.GetValue<bool>("Assistant:Enabled");
if (assistantEnabled)
{
    builder.Services.AddSingleton<IAssistant, ClaudeAssistant>();
    builder.Services.AddSingleton<IHandwritingReader, ClaudeHandwritingReader>();
}
else
{
    builder.Services.AddSingleton<IAssistant, DisabledAssistant>();
    builder.Services.AddSingleton<IHandwritingReader, DisabledHandwritingReader>();
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DatabaseUnavailableHandler>();
builder.Services.AddValidation();

var app = builder.Build();

if (migrateOnly)
{
    return await MigrationRunner.ApplyAsync(app.Services);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseStaticFiles();

// Liveness and readiness. Must never touch the database.
app.MapGet("/health", () => Results.Ok(new { status = "ok", app = appName }));

// Reports database connectivity. Not wired to the container probes.
app.MapGet("/ready", async (Database db, IServiceProvider sp, CancellationToken ct) =>
{
    if (!db.IsConfigured)
    {
        return Results.Ok(new { database = "unconfigured" });
    }

    try
    {
        var ctx = sp.GetRequiredService<AppDbContext>();
        var applied = await ctx.Database.GetAppliedMigrationsAsync(ct);
        return Results.Ok(new
        {
            database = "ok",
            auth = db.UsesManagedIdentity ? "managed-identity" : "password",
            migration = applied.LastOrDefault(),
        });
    }
    catch (Exception ex) when (DatabaseUnavailableHandler.IsDatabaseFailure(ex))
    {
        return Results.Ok(new { database = "unavailable", detail = ex.GetBaseException().Message });
    }
});

app.MapGet("/items", async (bool? open, AppDbContext ctx, CancellationToken ct) =>
{
    IQueryable<Item> query = ctx.Items.AsNoTracking();
    if (open == true)
    {
        query = query.WhereOpen();
    }
    return await query.OrderByDescending(i => i.Id).Take(100).ToListAsync(ct);
});

app.MapPost("/items", async (ItemIn input, AppDbContext ctx, CancellationToken ct) =>
{
    var item = new Item { Title = input.Title, Note = input.Note, Priority = input.Priority };
    ctx.Items.Add(item);
    await ctx.SaveChangesAsync(ct);
    return Results.Created($"/items/{item.Id}", item);
});

app.MapPatch("/items/{id:int}", async (int id, ItemPatchIn input, AppDbContext ctx, CancellationToken ct) =>
{
    var item = await ctx.Items.FindAsync([id], ct);
    if (item is null)
    {
        return Results.NotFound();
    }

    if (input.Done!.Value && !item.IsDone)
    {
        item.IsDone = true;
        item.DoneAt = DateTimeOffset.UtcNow;
    }
    else if (!input.Done.Value)
    {
        item.IsDone = false;
        item.DoneAt = null;
    }

    await ctx.SaveChangesAsync(ct);
    return Results.Ok(item);
});

app.MapNotebookEndpoints();

// Serve the notebook UI for the root path (index.html is in wwwroot/).
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;

public sealed record ItemIn(
    [property: Required, StringLength(200, MinimumLength = 1)] string Title,
    [property: StringLength(2000)] string? Note,
    [property: Range(1, 5)] int? Priority = null);

public sealed record ItemPatchIn([property: Required] bool? Done);

// Exposes Program to WebApplicationFactory in the tests.
public partial class Program;
