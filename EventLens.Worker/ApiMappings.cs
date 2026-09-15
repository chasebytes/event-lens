using EventLens.Core;

namespace EventLens.Worker;

internal static class ApiMappings
{
    public static MonitoringProfile CreateProfile(ProfileInput input)
    {
        var profile = new MonitoringProfile();
        profile.Checkpoint.ProfileId = profile.Id;
        Apply(profile, input);
        profile.Checkpoint.State = profile.IsEnabled ? CollectionState.NeverRun : CollectionState.Disabled;
        return profile;
    }

    public static void Apply(MonitoringProfile profile, ProfileInput input)
    {
        profile.Name = input.Name.Trim();
        profile.IsEnabled = input.IsEnabled;
        profile.Channel = input.Channel.Trim();
        profile.Provider = string.IsNullOrWhiteSpace(input.Provider) ? null : input.Provider.Trim();
        profile.EventIds = ProfileRules.SerializeEventIds(input.EventIds);
        profile.Severities = input.Severities;
        profile.PollingIntervalSeconds = input.PollingIntervalSeconds;
    }

    public static ProfileSummary ToSummary(MonitoringProfile profile) => new(
        profile.Id, profile.Name, profile.IsEnabled, profile.Channel, profile.Provider,
        ProfileRules.ParseEventIds(profile.EventIds), profile.Severities, profile.PollingIntervalSeconds,
        profile.Checkpoint.State, profile.Checkpoint.ErrorMessage,
        profile.Checkpoint.LastAttemptUtc, profile.Checkpoint.LastSuccessUtc);

    public static FindingSummary ToSummary(Finding finding) => new(
        finding.Id, finding.ProfileId, finding.EventTimestampUtc, finding.Severity,
        finding.Provider, finding.EventId, finding.Message);

    public static FindingDetail ToDetail(Finding finding, string profileName) => new(
        finding.Id, finding.ProfileId, profileName, finding.Channel, finding.Provider,
        finding.EventId, finding.RecordId, finding.Severity, finding.EventTimestampUtc,
        finding.CollectedAtUtc, finding.Message, finding.RawXml);
}
