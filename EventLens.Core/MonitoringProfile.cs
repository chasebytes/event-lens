using System.ComponentModel.DataAnnotations;

namespace EventLens.Core;

public sealed class MonitoringProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(120)] public string Name { get; set; } = "";
    public bool IsEnabled { get; set; }
    [MaxLength(260)] public string Channel { get; set; } = "Application";
    [MaxLength(260)] public string? Provider { get; set; }
    public string EventIds { get; set; } = "";
    public EventSeverity Severities { get; set; } = EventSeverity.Critical | EventSeverity.Error | EventSeverity.Warning;
    public int PollingIntervalSeconds { get; set; } = 30;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ProfileCheckpoint Checkpoint { get; set; } = new();
}