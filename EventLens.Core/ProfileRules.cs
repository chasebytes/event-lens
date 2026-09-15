namespace EventLens.Core;

public static class ProfileRules
{
    public const int MinimumPollingIntervalSeconds = 5;
    public const int MaximumPollingIntervalSeconds = 86400;

    public static IReadOnlyDictionary<string, string[]> Validate(ProfileInput input)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(input.Name)) errors[nameof(input.Name)] = ["Name is required."];
        else if (input.Name.Trim().Length > 120) errors[nameof(input.Name)] = ["Name must be 120 characters or fewer."];
        if (string.IsNullOrWhiteSpace(input.Channel)) errors[nameof(input.Channel)] = ["Channel is required."];
        else if (input.Channel.Trim().Length > 260) errors[nameof(input.Channel)] = ["Channel must be 260 characters or fewer."];
        if (input.Provider?.Trim().Length > 260) errors[nameof(input.Provider)] = ["Provider must be 260 characters or fewer."];
        if (input.Severities == EventSeverity.None || (input.Severities & ~EventSeverity.All) != 0)
            errors[nameof(input.Severities)] = ["Select at least one supported severity."];
        if (input.PollingIntervalSeconds is < MinimumPollingIntervalSeconds or > MaximumPollingIntervalSeconds)
            errors[nameof(input.PollingIntervalSeconds)] = [$"Polling interval must be between {MinimumPollingIntervalSeconds} and {MaximumPollingIntervalSeconds} seconds."];
        if (input.EventIds?.Any(id => id is < 0 or > 65535) == true)
            errors[nameof(input.EventIds)] = ["Event IDs must be between 0 and 65535."];
        return errors;
    }

    public static string SerializeEventIds(IEnumerable<int>? ids) =>
        string.Join(',', (ids ?? []).Distinct().Order());

    public static IReadOnlyList<int> ParseEventIds(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(int.Parse).Distinct().Order().ToArray();
}