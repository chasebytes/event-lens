namespace EventLens.Core;

public sealed record ProfileSummary(
    Guid Id,
    string Name,
    bool IsEnabled,
    string Channel,
    string? Provider,
    IReadOnlyList<int> EventIds,
    EventSeverity Severities,
    int PollingIntervalSeconds,
    CollectionState State,
    string? ErrorMessage,
    DateTimeOffset? LastAttemptUtc,
    DateTimeOffset? LastSuccessUtc);