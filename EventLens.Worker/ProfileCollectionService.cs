using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using EventLens.Core;
using EventLens.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventLens.Worker;

internal sealed class ProfileCollectionService(
    IDbContextFactory<EventLensDbContext> dbFactory,
    IWindowsEventCollector collector,
    IConfiguration configuration,
    ILogger<ProfileCollectionService> logger)
{
    private readonly int _batchSize = Math.Clamp(configuration.GetValue("EventLens:MaximumEventsPerPoll", 1000), 1, 10_000);

    public async Task CollectAsync(Guid profileId, CancellationToken cancellationToken)
    {
        MonitoringProfile profile;
        await using (var statusDb = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var current = await statusDb.Profiles.Include(x => x.Checkpoint)
                .SingleOrDefaultAsync(x => x.Id == profileId, cancellationToken);
            if (current is null || !current.IsEnabled) return;

            profile = current;
            profile.Checkpoint.State = CollectionState.Collecting;
            profile.Checkpoint.LastAttemptUtc = DateTimeOffset.UtcNow;
            profile.Checkpoint.ErrorMessage = null;
            await statusDb.SaveChangesAsync(cancellationToken);
        }

        try
        {
            var batch = await collector.CollectAsync(profile, _batchSize, cancellationToken);
            await StoreBatch(profile, batch, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RecordFailure(profileId, exception, cancellationToken);
            logger.LogWarning(exception, "Collection failed for profile {ProfileId} ({ProfileName}).", profile.Id, profile.Name);
            return;
        }
    }

    private async Task StoreBatch(MonitoringProfile snapshot, CollectionBatch batch, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await db.Profiles.Include(x => x.Checkpoint)
            .SingleOrDefaultAsync(x => x.Id == snapshot.Id, cancellationToken);
        if (current is null || !current.IsEnabled || !HasSameCollectionDefinition(snapshot, current)) return;

        var recordIds = batch.Matches.Where(x => x.RecordId.HasValue).Select(x => x.RecordId!.Value).ToArray();
        var existing = recordIds.Length == 0
            ? new HashSet<long>()
            : (await db.Findings
                .Where(x => x.ProfileId == snapshot.Id && x.RecordId.HasValue && recordIds.Contains(x.RecordId.Value))
                .Select(x => x.RecordId!.Value)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var collectedAt = DateTimeOffset.UtcNow;
        db.Findings.AddRange(batch.Matches
            .Where(x => !x.RecordId.HasValue || !existing.Contains(x.RecordId.Value))
            .Select(x => new Finding
            {
                ProfileId = snapshot.Id,
                Channel = x.Channel,
                Provider = x.Provider,
                EventId = x.EventId,
                RecordId = x.RecordId,
                Severity = x.Severity,
                EventTimestampUtc = x.TimestampUtc,
                CollectedAtUtc = collectedAt,
                Message = x.Message,
                RawXml = x.RawXml
            }));
        current.Checkpoint.LastRecordId = batch.LastRecordId;
        current.Checkpoint.LastEventTimestampUtc = batch.LastEventTimestampUtc;
        current.Checkpoint.LastSuccessUtc = collectedAt;
        current.Checkpoint.State = CollectionState.Healthy;
        current.Checkpoint.ErrorMessage = batch.HasMore
            ? "Backlog detected; collection will continue on the next poll."
            : null;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RecordFailure(Guid profileId, Exception exception, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var checkpoint = await db.Checkpoints.SingleOrDefaultAsync(x => x.ProfileId == profileId, cancellationToken);
        if (checkpoint is null) return;

        checkpoint.LastAttemptUtc = DateTimeOffset.UtcNow;
        checkpoint.State = IsAccessDenied(exception) ? CollectionState.AccessDenied : CollectionState.Failed;
        checkpoint.ErrorMessage = FriendlyMessage(exception);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsAccessDenied(Exception exception) =>
        exception is UnauthorizedAccessException ||
        exception is Win32Exception { NativeErrorCode: 5 } ||
        exception is EventLogException && (exception.HResult & 0xffff) == 5 ||
        exception.InnerException is not null && IsAccessDenied(exception.InnerException);

    private static string FriendlyMessage(Exception exception) => IsAccessDenied(exception)
        ? "Access denied. Run the worker with permission to read this Windows Event Log channel."
        : $"{exception.GetType().Name}: {exception.Message}";

    private static bool HasSameCollectionDefinition(MonitoringProfile snapshot, MonitoringProfile current) =>
        snapshot.CreatedAtUtc == current.CreatedAtUtc &&
        string.Equals(snapshot.Channel, current.Channel, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(snapshot.Provider, current.Provider, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(snapshot.EventIds, current.EventIds, StringComparison.Ordinal) &&
        snapshot.Severities == current.Severities;
}
