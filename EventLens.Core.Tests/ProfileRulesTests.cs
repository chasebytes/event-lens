using EventLens.Core;

namespace EventLens.Core.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class ProfileRulesTests
{
    [TestMethod]
    public void Validate_RejectsAProfileThatCannotBeCollected()
    {
        var input = new ProfileInput(" ", true, "", null, [-1], EventSeverity.None, 1);

        var errors = ProfileRules.Validate(input);

        CollectionAssert.AreEquivalent(
            new[] { "Name", "Channel", "EventIds", "Severities", "PollingIntervalSeconds" },
            errors.Keys.ToArray());
    }

    [TestMethod]
    public void EventIds_RoundTrip_InAStableDistinctOrder()
    {
        var stored = ProfileRules.SerializeEventIds([1001, 12, 1001]);

        Assert.AreEqual("12,1001", stored);
        CollectionAssert.AreEqual(new[] { 12, 1001 }, ProfileRules.ParseEventIds(stored).ToArray());
    }

    [TestMethod]
    public void Validate_AcceptsInclusivePollingAndEventIdBoundaries()
    {
        var minimum = new ProfileInput("Minimum", true, "Application", null, [0], EventSeverity.Critical,
            ProfileRules.MinimumPollingIntervalSeconds);
        var maximum = new ProfileInput("Maximum", true, "Application", null, [65535], EventSeverity.Verbose,
            ProfileRules.MaximumPollingIntervalSeconds);

        Assert.IsEmpty(ProfileRules.Validate(minimum));
        Assert.IsEmpty(ProfileRules.Validate(maximum));
    }

    [TestMethod]
    public void Validate_RejectsUnknownSeverityFlags()
    {
        var input = new ProfileInput("Unknown severity", true, "Application", null, [], (EventSeverity)32, 30);

        var errors = ProfileRules.Validate(input);

        Assert.IsTrue(errors.ContainsKey(nameof(ProfileInput.Severities)));
    }

    [TestMethod]
    public void EmptyEventIdFilter_RoundTrips_AsNoFilter()
    {
        Assert.AreEqual(string.Empty, ProfileRules.SerializeEventIds(null));
        Assert.IsEmpty(ProfileRules.ParseEventIds(" , "));
    }
}
