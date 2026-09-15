namespace EventLens.Core;

public sealed record FindingPage(IReadOnlyList<FindingSummary> Items, long LatestCursor);