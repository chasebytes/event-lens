using EventLens.Core;
using EventLens.Windows;

namespace EventLens.Windows.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class WindowsEventFilterTests
{
    [TestMethod]
    public void EmptyProviderAndEventIds_LeaveThoseFiltersOpen()
    {
        var matches = WindowsEventFilter.Matches(
            EventSeverity.Error, null, new HashSet<int>(), EventSeverity.Error, "Any provider", 900);

        Assert.IsTrue(matches);
    }

    [TestMethod]
    public void Severity_MustBeOneOfTheSelectedLevels()
    {
        var matches = WindowsEventFilter.Matches(
            EventSeverity.Error | EventSeverity.Warning, null, new HashSet<int>(),
            EventSeverity.Information, "Provider", 100);

        Assert.IsFalse(matches);
    }

    [TestMethod]
    public void ProviderMatch_IsTrimmed_AndCaseInsensitive()
    {
        var matches = WindowsEventFilter.Matches(
            EventSeverity.Information, "  EVENTLENS  ", new HashSet<int>(),
            EventSeverity.Information, "EventLens", 100);

        Assert.IsTrue(matches);
    }

    [TestMethod]
    public void EventId_MustBeInTheSelectedSet_WhenItIsNotEmpty()
    {
        var selectedIds = new HashSet<int> { 100, 200 };

        Assert.IsTrue(WindowsEventFilter.Matches(
            EventSeverity.All, null, selectedIds, EventSeverity.Error, "Provider", 200));
        Assert.IsFalse(WindowsEventFilter.Matches(
            EventSeverity.All, null, selectedIds, EventSeverity.Error, "Provider", 201));
    }
}
