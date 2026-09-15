using System.ComponentModel.DataAnnotations;

namespace EventLens.Core;

public sealed class ProfileCheckpoint
{
    public Guid ProfileId { get; set; }
    public long? LastRecordId { get; set; }
    public DateTimeOffset? LastEventTimestampUtc { get; set; }
    public DateTimeOffset? LastAttemptUtc { get; set; }
    public DateTimeOffset? LastSuccessUtc { get; set; }
    public CollectionState State { get; set; } = CollectionState.NeverRun;
    [MaxLength(2000)] public string? ErrorMessage { get; set; }
}