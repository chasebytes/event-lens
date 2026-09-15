using EventLens.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventLens.Worker;

internal sealed class CollectionWorker(
    IDbContextFactory<EventLensDbContext> dbFactory,
    ProfileCollectionService collectionService,
    ILogger<CollectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueProfiles(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "The collection scheduler failed; it will retry.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task ProcessDueProfiles(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var candidates = await db.Profiles.AsNoTracking().Include(x => x.Checkpoint)
            .Where(x => x.IsEnabled)
            .ToListAsync(cancellationToken);

        foreach (var profile in candidates)
        {
            if (!CollectionSchedule.IsDue(profile, now)) continue;
            try
            {
                await collectionService.CollectAsync(profile.Id, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "The collection use case failed unexpectedly for profile {ProfileId} ({ProfileName}).", profile.Id, profile.Name);
            }
        }
    }
}
