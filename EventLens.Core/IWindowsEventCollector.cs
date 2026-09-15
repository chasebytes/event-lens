namespace EventLens.Core;

public interface IWindowsEventCollector
{
    Task<CollectionBatch> CollectAsync(MonitoringProfile profile,
        int maximumEvents,
        CancellationToken cancellationToken);
}