using EventLens.Core;
using EventLens.Worker;

namespace EventLens.Worker.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class ApiMappingsTests
{
    [TestMethod]
    public void CreateProfile_NormalizesUserInput_AndCreatesCheckpoint()
    {
        var input = new ProfileInput("  Errors  ", true, " Application ", "  Source ", [9, 2, 9], EventSeverity.Error, 10);

        var profile = ApiMappings.CreateProfile(input);

        Assert.AreEqual("Errors", profile.Name);
        Assert.AreEqual("Application", profile.Channel);
        Assert.AreEqual("Source", profile.Provider);
        Assert.AreEqual("2,9", profile.EventIds);
        Assert.AreEqual(profile.Id, profile.Checkpoint.ProfileId);
        Assert.AreEqual(CollectionState.NeverRun, profile.Checkpoint.State);
    }

    [TestMethod]
    public void FindingCursor_AdvancesPastNonmatchingRows_WhenPageIsNotFull()
    {
        var cursor = FindingPaging.SelectCursor(40, 75, []);

        Assert.AreEqual(75, cursor);
    }

    [TestMethod]
    public void FindingCursor_StopsAtAFullPage_SoRemainingMatchesAreNotSkipped()
    {
        var ids = Enumerable.Range(41, FindingPaging.PageSize).Select(x => (long)x).ToArray();

        var cursor = FindingPaging.SelectCursor(40, 500, ids);

        Assert.AreEqual(ids[^1], cursor);
    }

    [TestMethod]
    public void DisabledProfile_StartsWithDisabledCheckpoint_AndNoBlankProvider()
    {
        var input = new ProfileInput("Quiet", false, "Application", "   ", [], EventSeverity.All, 30);

        var profile = ApiMappings.CreateProfile(input);

        Assert.AreEqual(CollectionState.Disabled, profile.Checkpoint.State);
        Assert.IsNull(profile.Provider);
    }

    [TestMethod]
    public void InitialFindingPage_UsesTheDatabaseHighWaterMark()
    {
        var cursor = FindingPaging.SelectCursor(null, 913, [900, 899]);

        Assert.AreEqual(913, cursor);
    }
}
