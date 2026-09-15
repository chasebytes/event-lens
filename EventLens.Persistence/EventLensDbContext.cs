using EventLens.Core;
using Microsoft.EntityFrameworkCore;

namespace EventLens.Persistence;

public sealed class EventLensDbContext(DbContextOptions<EventLensDbContext> options) : DbContext(options)
{
    public DbSet<MonitoringProfile> Profiles => Set<MonitoringProfile>();
    public DbSet<ProfileCheckpoint> Checkpoints => Set<ProfileCheckpoint>();
    public DbSet<Finding> Findings => Set<Finding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonitoringProfile>(profile =>
        {
            profile.HasKey(x => x.Id);
            profile.Property(x => x.Severities).HasConversion<int>();
            profile.HasOne(x => x.Checkpoint)
                .WithOne()
                .HasForeignKey<ProfileCheckpoint>(x => x.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProfileCheckpoint>(checkpoint =>
        {
            checkpoint.HasKey(x => x.ProfileId);
            checkpoint.Property(x => x.State).HasConversion<string>();
        });

        modelBuilder.Entity<Finding>(finding =>
        {
            finding.HasKey(x => x.Id);
            finding.Property(x => x.Severity).HasConversion<int>();
            finding.HasIndex(x => new { x.ProfileId, x.Id });
            finding.HasIndex(x => new { x.ProfileId, x.RecordId })
                .IsUnique()
                .HasFilter("RecordId IS NOT NULL");
            finding.HasOne<MonitoringProfile>()
                .WithMany()
                .HasForeignKey(x => x.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public static class DatabasePath
{
    public static string Resolve(string? configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EventLens", "eventlens.db")
            : Environment.ExpandEnvironmentVariables(configuredPath);
        return Path.GetFullPath(path);
    }
}
