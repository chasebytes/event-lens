namespace EventLens.Core;

public sealed record FindingSummary(
    long Id,
    Guid ProfileId,
    DateTimeOffset EventTimestampUtc,
    EventSeverity Severity,
    string? Provider,
    int EventId,
    string? Message);