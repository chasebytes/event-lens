namespace EventLens.Core;

public sealed record FindingDetail(
    long Id,
    Guid ProfileId,
    string ProfileName,
    string Channel,
    string? Provider,
    int EventId,
    long? RecordId,
    EventSeverity Severity,
    DateTimeOffset EventTimestampUtc,
    DateTimeOffset CollectedAtUtc,
    string? Message,
    string RawXml);