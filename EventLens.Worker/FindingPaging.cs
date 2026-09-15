namespace EventLens.Worker;

internal static class FindingPaging
{
    public const int PageSize = 200;

    public static long SelectCursor(long? after, long currentCursor, IReadOnlyList<long> returnedIds) =>
        after.HasValue && returnedIds.Count == PageSize
            ? returnedIds[^1]
            : currentCursor;
}
