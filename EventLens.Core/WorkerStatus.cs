namespace EventLens.Core;

public sealed record WorkerStatus(bool Available, DateTimeOffset StartedAtUtc, int EnabledProfiles, string DatabasePath);