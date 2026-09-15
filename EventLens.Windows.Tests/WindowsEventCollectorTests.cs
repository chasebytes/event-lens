using EventLens.Core;
using EventLens.Windows;

namespace EventLens.Windows.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class WindowsEventCollectorTests
{
    [TestMethod]
    public void Query_ResumesAfterTheStoredRecordId()
    {
        var profile = new MonitoringProfile { CreatedAtUtc = DateTimeOffset.Parse("2026-09-15T10:00:00Z") };
        profile.Checkpoint.LastRecordId = 812;

        Assert.AreEqual("*[System[EventRecordID > 812]]", WindowsEventCollector.BuildXPath(profile));
    }

    [TestMethod]
    public void InitialQuery_StartsAtProfileCreation_InsteadOfReadingTheEntireLog()
    {
        var profile = new MonitoringProfile { CreatedAtUtc = DateTimeOffset.Parse("2026-09-15T10:00:00Z") };

        StringAssert.Contains(WindowsEventCollector.BuildXPath(profile), "2026-09-15T10:00:00.0000000Z");
    }

    [TestMethod]
    public void InitialQuery_NormalizesProfileCreation_ToUtc()
    {
        var profile = new MonitoringProfile { CreatedAtUtc = DateTimeOffset.Parse("2026-09-15T05:00:00-05:00") };

        StringAssert.Contains(WindowsEventCollector.BuildXPath(profile), "2026-09-15T10:00:00.0000000Z");
    }

    [TestMethod]
    [DataRow((byte)1, EventSeverity.Critical)]
    [DataRow((byte)2, EventSeverity.Error)]
    [DataRow((byte)3, EventSeverity.Warning)]
    [DataRow((byte)4, EventSeverity.Information)]
    [DataRow((byte)5, EventSeverity.Verbose)]
    [DataRow((byte)0, EventSeverity.None)]
    public void WindowsLevels_Map_ToSupportedSeverities(byte level, EventSeverity expected)
    {
        Assert.AreEqual(expected, WindowsEventCollector.MapSeverity(level));
    }

    [TestMethod]
    public void MissingWindowsLevel_IsNotTreatedAsAMatch()
    {
        Assert.AreEqual(EventSeverity.None, WindowsEventCollector.MapSeverity(null));
    }
}
