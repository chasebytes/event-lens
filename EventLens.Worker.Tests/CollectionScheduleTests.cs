using EventLens.Core;
using EventLens.Worker;

namespace EventLens.Worker.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class CollectionScheduleTests
{
    [TestMethod]
    public void ProfileThatHasNeverRun_IsDue_Immediately()
    {
        var profile = new MonitoringProfile { PollingIntervalSeconds = 30 };

        Assert.IsTrue(CollectionSchedule.IsDue(profile, DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void Profile_IsNotDue_BeforeItsPollingIntervalElapses()
    {
        var lastAttempt = DateTimeOffset.Parse("2026-09-15T10:00:00Z");
        var profile = new MonitoringProfile { PollingIntervalSeconds = 5 };
        profile.Checkpoint.LastAttemptUtc = lastAttempt;

        Assert.IsFalse(CollectionSchedule.IsDue(profile, lastAttempt.AddMilliseconds(4999)));
    }

    [TestMethod]
    public void Profile_IsDue_AtTheExactPollingIntervalBoundary()
    {
        var lastAttempt = DateTimeOffset.Parse("2026-09-15T10:00:00Z");
        var profile = new MonitoringProfile { PollingIntervalSeconds = 5 };
        profile.Checkpoint.LastAttemptUtc = lastAttempt;

        Assert.IsTrue(CollectionSchedule.IsDue(profile, lastAttempt.AddSeconds(5)));
    }
}
