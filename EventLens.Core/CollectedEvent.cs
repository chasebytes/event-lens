namespace EventLens.Core;

public sealed record CollectedEvent(
    string Channel,
    string? Provider,
    int EventId,
    long? RecordId,
    EventSeverity Severity,
    DateTimeOffset TimestampUtc,
    string? Message,
    string RawXml);