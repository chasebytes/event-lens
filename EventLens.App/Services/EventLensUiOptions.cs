namespace EventLens.App.Services;

public sealed class EventLensUiOptions
{
    public const string SectionName = "EventLens";
    public int FindingsPollIntervalSeconds { get; set; } = 1;
    public int ProfileStatusPollIntervalSeconds { get; set; } = 5;
    public int WorkerApiTimeoutSeconds { get; set; } = 10;
    public TimeSpan FindingsPollInterval =>
        TimeSpan.FromSeconds(Math.Clamp(FindingsPollIntervalSeconds, 1, 60));

    public TimeSpan ProfileStatusPollInterval =>
        TimeSpan.FromSeconds(Math.Clamp(ProfileStatusPollIntervalSeconds, 1, 300));

    public TimeSpan WorkerApiTimeout =>
        TimeSpan.FromSeconds(Math.Clamp(WorkerApiTimeoutSeconds, 1, 120));
}
