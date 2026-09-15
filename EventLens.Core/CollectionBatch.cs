namespace EventLens.Core;

public sealed record CollectionBatch(IReadOnlyList<CollectedEvent> Matches, long? LastRecordId, DateTimeOffset? LastEventTimestampUtc, bool HasMore);