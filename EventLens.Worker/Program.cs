using EventLens.Core;
using EventLens.Persistence;
using EventLens.Windows;
using EventLens.Worker;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "HH:mm:ss ");
builder.Logging.AddDebug();
var databasePath = DatabasePath.Resolve(builder.Configuration["EventLens:DatabasePath"]);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

builder.Services.AddSingleton(new WorkerRuntime(DateTimeOffset.UtcNow, databasePath));
builder.Services.AddDbContextFactory<EventLensDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddSingleton<IWindowsEventCollector, WindowsEventCollector>();
builder.Services.AddSingleton<ProfileCollectionService>();
builder.Services.AddHostedService<CollectionWorker>();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EventLensDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/api/status", async (IDbContextFactory<EventLensDbContext> factory, WorkerRuntime runtime, CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    var enabled = await db.Profiles.CountAsync(x => x.IsEnabled, ct);
    return Results.Ok(new WorkerStatus(true, runtime.StartedAtUtc, enabled, runtime.DatabasePath));
});

var profiles = app.MapGroup("/api/profiles");
profiles.MapGet("/", async (IDbContextFactory<EventLensDbContext> factory, CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    var rows = await db.Profiles.AsNoTracking().Include(x => x.Checkpoint).OrderBy(x => x.Name).ToListAsync(ct);
    return Results.Ok(rows.Select(ApiMappings.ToSummary));
});

profiles.MapGet("/{id:guid}", async (Guid id, IDbContextFactory<EventLensDbContext> factory, CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    var row = await db.Profiles.AsNoTracking().Include(x => x.Checkpoint).SingleOrDefaultAsync(x => x.Id == id, ct);
    return row is null ? Results.NotFound() : Results.Ok(ApiMappings.ToSummary(row));
});

profiles.MapPost("/", async (ProfileInput input, IDbContextFactory<EventLensDbContext> factory, CancellationToken ct) =>
{
    var errors = ProfileRules.Validate(input);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    await using var db = await factory.CreateDbContextAsync(ct);
    var profile = ApiMappings.CreateProfile(input);
    db.Profiles.Add(profile);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/profiles/{profile.Id}", ApiMappings.ToSummary(profile));
});

profiles.MapPut("/{id:guid}", async (Guid id, ProfileInput input, IDbContextFactory<EventLensDbContext> factory, CancellationToken ct) =>
{
    var errors = ProfileRules.Validate(input);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    await using var db = await factory.CreateDbContextAsync(ct);
    var profile = await db.Profiles.Include(x => x.Checkpoint).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (profile is null) return Results.NotFound();
    var channelChanged = !string.Equals(profile.Channel, input.Channel.Trim(), StringComparison.OrdinalIgnoreCase);
    ApiMappings.Apply(profile, input);
    if (channelChanged)
    {
        profile.CreatedAtUtc = DateTimeOffset.UtcNow;
        profile.Checkpoint.LastRecordId = null;
        profile.Checkpoint.LastEventTimestampUtc = null;
        profile.Checkpoint.LastAttemptUtc = null;
        profile.Checkpoint.LastSuccessUtc = null;
        profile.Checkpoint.State = profile.IsEnabled ? CollectionState.NeverRun : CollectionState.Disabled;
        profile.Checkpoint.ErrorMessage = null;
    }
    else if (!profile.IsEnabled)
    {
        profile.Checkpoint.State = CollectionState.Disabled;
        profile.Checkpoint.ErrorMessage = null;
    }
    else if (profile.Checkpoint.State == CollectionState.Disabled)
    {
        profile.Checkpoint.State = CollectionState.NeverRun;
    }
    await db.SaveChangesAsync(ct);
    return Results.Ok(ApiMappings.ToSummary(profile));
});

profiles.MapDelete("/{id:guid}", async (Guid id, IDbContextFactory<EventLensDbContext> factory, CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    var profile = await db.Profiles.FindAsync([id], ct);
    if (profile is null) return Results.NotFound();
    db.Profiles.Remove(profile);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

profiles.MapGet("/{id:guid}/findings", async (
    Guid id, long? after, EventSeverity? severity, string? provider, int? eventId, string? message,
    IDbContextFactory<EventLensDbContext> factory, CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    if (!await db.Profiles.AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
    var query = db.Findings.AsNoTracking().Where(x => x.ProfileId == id);
    if (after.HasValue) query = query.Where(x => x.Id > after.Value);
    if (severity.HasValue && severity != EventSeverity.None) query = query.Where(x => x.Severity == severity);
    if (!string.IsNullOrWhiteSpace(provider)) query = query.Where(x => x.Provider != null && x.Provider.Contains(provider));
    if (eventId.HasValue) query = query.Where(x => x.EventId == eventId.Value);
    if (!string.IsNullOrWhiteSpace(message)) query = query.Where(x => x.Message != null && x.Message.Contains(message));
    // Read the high-water mark first. A concurrent insert may then be returned twice,
    // but it cannot be skipped by advancing the cursor past an unread row.
    var currentCursor = await db.Findings
        .Where(x => x.ProfileId == id)
        .MaxAsync(x => (long?)x.Id, ct) ?? after ?? 0;
    var rows = after.HasValue
        ? await query.OrderBy(x => x.Id).Take(FindingPaging.PageSize).ToListAsync(ct)
        : await query.OrderByDescending(x => x.Id).Take(FindingPaging.PageSize).ToListAsync(ct);
    var cursor = FindingPaging.SelectCursor(after, currentCursor, rows.Select(x => x.Id).ToArray());
    return Results.Ok(new FindingPage(rows.Select(ApiMappings.ToSummary).ToArray(), cursor));
});

app.MapGet("/api/findings/{id:long}", async (long id, IDbContextFactory<EventLensDbContext> factory, CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    var row = await db.Findings.AsNoTracking()
        .Where(x => x.Id == id)
        .Join(db.Profiles, finding => finding.ProfileId, profile => profile.Id,
            (finding, profile) => new { Finding = finding, ProfileName = profile.Name })
        .SingleOrDefaultAsync(ct);
    if (row is null) return Results.NotFound();
    return Results.Ok(ApiMappings.ToDetail(row.Finding, row.ProfileName));
});

await app.RunAsync();

public sealed record WorkerRuntime(DateTimeOffset StartedAtUtc, string DatabasePath);
