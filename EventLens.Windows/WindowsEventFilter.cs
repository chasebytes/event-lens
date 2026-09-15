using EventLens.Core;

namespace EventLens.Windows;

internal static class WindowsEventFilter
{
    public static bool Matches(
        EventSeverity selectedSeverities,
        string? selectedProvider,
        IReadOnlySet<int> selectedEventIds,
        EventSeverity eventSeverity,
        string? eventProvider,
        int eventId)
    {
        if (eventSeverity == EventSeverity.None || !selectedSeverities.HasFlag(eventSeverity)) return false;
        if (!string.IsNullOrWhiteSpace(selectedProvider) &&
            !string.Equals(selectedProvider.Trim(), eventProvider, StringComparison.OrdinalIgnoreCase)) return false;
        return selectedEventIds.Count == 0 || selectedEventIds.Contains(eventId);
    }
}
