namespace EventLens.Core;

public sealed record ProfileInput(
    string Name,
    bool IsEnabled,
    string Channel,
    string? Provider,
    IReadOnlyList<int>? EventIds,
    EventSeverity Severities,
    int PollingIntervalSeconds);