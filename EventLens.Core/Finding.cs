using System.ComponentModel.DataAnnotations;

namespace EventLens.Core;

public sealed class Finding
{
    public long Id { get; set; }
    public Guid ProfileId { get; set; }
    [MaxLength(260)] public string Channel { get; set; } = "";
    [MaxLength(260)] public string? Provider { get; set; }
    public int EventId { get; set; }
    public long? RecordId { get; set; }
    public EventSeverity Severity { get; set; }
    public DateTimeOffset EventTimestampUtc { get; set; }
    public DateTimeOffset CollectedAtUtc { get; set; }
    public string? Message { get; set; }
    public string RawXml { get; set; } = "";
}