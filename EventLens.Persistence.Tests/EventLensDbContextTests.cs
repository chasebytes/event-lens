using EventLens.Core;
using EventLens.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventLens.Persistence.Tests;

[TestClass]
[TestCategory("Persistence")]
public sealed class EventLensDbContextTests
{
    [TestMethod]
    public async Task RecordId_IsUniqueWithinAProfile_AndFindingsCascadeOnDelete()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EventLensDbContext>().UseSqlite(connection).Options;
        var profile = NewProfile();

        await using (var db = new EventLensDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Profiles.Add(profile);
            db.Findings.Add(NewFinding(profile.Id, 42));
            await db.SaveChangesAsync();
            db.Findings.Add(NewFinding(profile.Id, 42));
            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var db = new EventLensDbContext(options))
        {
            db.Profiles.Remove(await db.Profiles.SingleAsync());
            await db.SaveChangesAsync();
            Assert.AreEqual(0, await db.Findings.CountAsync());
        }
    }

    private static MonitoringProfile NewProfile()
    {
        var profile = new MonitoringProfile { Name = "Application errors" };
        profile.Checkpoint.ProfileId = profile.Id;
        return profile;
    }

    private static Finding NewFinding(Guid profileId, long recordId) => new()
    {
        ProfileId = profileId,
        Channel = "Application",
        EventId = 1000,
        RecordId = recordId,
        Severity = EventSeverity.Error,
        EventTimestampUtc = DateTimeOffset.UtcNow,
        CollectedAtUtc = DateTimeOffset.UtcNow,
        RawXml = "<Event />"
    };
}
