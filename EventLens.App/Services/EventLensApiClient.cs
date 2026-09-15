using System.Net;
using System.Net.Http.Json;
using EventLens.Core;

namespace EventLens.App.Services;

public sealed class EventLensApiClient(HttpClient httpClient)
{
    public async Task<WorkerStatus?> GetStatusAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<WorkerStatus>("api/status", cancellationToken);

    public async Task<IReadOnlyList<ProfileSummary>> GetProfilesAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<ProfileSummary[]>("api/profiles", cancellationToken) ?? [];

    public async Task<ProfileSummary?> GetProfileAsync(Guid id, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<ProfileSummary>($"api/profiles/{id}", cancellationToken);

    public async Task SaveProfileAsync(Guid? id, ProfileInput input, CancellationToken cancellationToken = default)
    {
        using var response = id.HasValue
            ? await httpClient.PutAsJsonAsync($"api/profiles/{id}", input, cancellationToken)
            : await httpClient.PostAsJsonAsync("api/profiles", input, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
    }

    public async Task DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/profiles/{id}", cancellationToken);
        await EnsureSuccess(response, cancellationToken);
    }

    public async Task<FindingPage> GetFindingsAsync(
        Guid profileId, long? after = null, EventSeverity? severity = null,
        string? provider = null, int? eventId = null, string? message = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (after.HasValue) query.Add($"after={after.Value}");
        if (severity.HasValue && severity != EventSeverity.None) query.Add($"severity={(int)severity.Value}");
        if (!string.IsNullOrWhiteSpace(provider)) query.Add($"provider={Uri.EscapeDataString(provider)}");
        if (eventId.HasValue) query.Add($"eventId={eventId.Value}");
        if (!string.IsNullOrWhiteSpace(message)) query.Add($"message={Uri.EscapeDataString(message)}");
        var suffix = query.Count == 0 ? "" : "?" + string.Join('&', query);
        return await httpClient.GetFromJsonAsync<FindingPage>($"api/profiles/{profileId}/findings{suffix}", cancellationToken)
            ?? new FindingPage([], after ?? 0);
    }

    public async Task<FindingDetail?> GetFindingAsync(long id, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<FindingDetail>($"api/findings/{id}", cancellationToken);

    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest && detail.Contains("errors", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The worker rejected the profile. Check the form values and try again.");
        throw new HttpRequestException($"Worker returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
    }
}
