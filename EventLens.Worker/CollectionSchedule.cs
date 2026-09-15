using EventLens.Core;

namespace EventLens.Worker;

internal static class CollectionSchedule
{
    public static bool IsDue(MonitoringProfile profile, DateTimeOffset now) =>
        !profile.Checkpoint.LastAttemptUtc.HasValue ||
        profile.Checkpoint.LastAttemptUtc.Value.AddSeconds(profile.PollingIntervalSeconds) <= now;
}
