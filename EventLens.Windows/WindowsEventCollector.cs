using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using EventLens.Core;

namespace EventLens.Windows;

public sealed class WindowsEventCollector : IWindowsEventCollector
{
    public Task<CollectionBatch> CollectAsync(MonitoringProfile profile, int maximumEvents, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows Event Log collection is available only on Windows.");

        return Task.FromResult(Collect(profile, maximumEvents, cancellationToken));
    }

    [SupportedOSPlatform("windows")]
    private static CollectionBatch Collect(MonitoringProfile profile, int maximumEvents, CancellationToken cancellationToken)
    {
        var query = new EventLogQuery(profile.Channel, PathType.LogName, BuildXPath(profile))
        {
            ReverseDirection = false,
            TolerateQueryErrors = false
        };

        using var reader = new EventLogReader(query);
        var matches = new List<CollectedEvent>();
        var lastRecordId = profile.Checkpoint.LastRecordId;
        var lastTimestamp = profile.Checkpoint.LastEventTimestampUtc;
        var requestedIds = ProfileRules.ParseEventIds(profile.EventIds).ToHashSet();
        var read = 0;

        while (read < maximumEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var record = reader.ReadEvent();
            if (record is null) break;
            read++;

            if (record.RecordId is { } recordId)
                lastRecordId = !lastRecordId.HasValue || recordId > lastRecordId ? recordId : lastRecordId;
            if (record.TimeCreated is { } created)
            {
                var timestamp = new DateTimeOffset(created.ToUniversalTime(), TimeSpan.Zero);
                lastTimestamp = !lastTimestamp.HasValue || timestamp > lastTimestamp ? timestamp : lastTimestamp;
            }

            var severity = MapSeverity(record.Level);
            if (!WindowsEventFilter.Matches(profile.Severities, profile.Provider, requestedIds,
                    severity, record.ProviderName, record.Id)) continue;

            string? message = null;
            try { message = record.FormatDescription(); }
            catch (Exception exception) when (exception is EventLogException or InvalidOperationException or UnauthorizedAccessException) { }

            var rawXml = "";
            try { rawXml = record.ToXml(); }
            catch (EventLogException) { }

            matches.Add(new CollectedEvent(
                profile.Channel,
                record.ProviderName,
                record.Id,
                record.RecordId,
                severity,
                record.TimeCreated is { } eventTime
                    ? new DateTimeOffset(eventTime.ToUniversalTime(), TimeSpan.Zero)
                    : DateTimeOffset.UtcNow,
                message,
                rawXml));
        }

        return new CollectionBatch(matches, lastRecordId, lastTimestamp, read == maximumEvents);
    }

    internal static string BuildXPath(MonitoringProfile profile)
    {
        if (profile.Checkpoint.LastRecordId is { } recordId)
            return $"*[System[EventRecordID > {recordId}]]";

        var timestamp = profile.CreatedAtUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
        return $"*[System[TimeCreated[@SystemTime >= '{timestamp}']]]";
    }

    internal static EventSeverity MapSeverity(byte? level) => level switch
    {
        1 => EventSeverity.Critical,
        2 => EventSeverity.Error,
        3 => EventSeverity.Warning,
        4 => EventSeverity.Information,
        5 => EventSeverity.Verbose,
        _ => EventSeverity.None
    };
}
