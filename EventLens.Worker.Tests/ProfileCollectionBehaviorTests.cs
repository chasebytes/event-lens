using EventLens.Core;
using EventLens.Persistence;
using EventLens.Worker;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventLens.Worker.Tests.Behavioral;

[TestClass]
[TestCategory("Behavioral")]
public sealed class ProfileCollectionBehaviorTests
{
    [TestMethod(DisplayName = "Given a configured profile, when collection runs, then its filters and batch limit are honored")]
    public async Task ConfiguredProfile_IsHonored()
    {
        await using var scenario = await CollectionScenario.CreateAsync(maximumEvents: 37);
        var input = new ProfileInput(
            "  Information events  ", true, " System ", " Service Control Manager ",
            [7040, 7036, 7040], EventSeverity.Information | EventSeverity.Warning, 5);
        var profile = await scenario.AddProfileAsync(input);
        var occurredAt = DateTimeOffset.Parse("2026-09-15T14:43:00Z");
        scenario.Collector.NextBatch = new CollectionBatch(
            [new CollectedEvent("System", "Service Control Manager", 7040, 891, EventSeverity.Information,
                occurredAt, "A service changed state.", "<Event id=\"7040\" />")],
            891, occurredAt, false);

        await scenario.Service.CollectAsync(profile.Id, CancellationToken.None);

        var request = AssertSingle(scenario.Collector.Requests);
        Assert.AreEqual("Information events", request.Name);
        Assert.AreEqual("System", request.Channel);
        Assert.AreEqual("Service Control Manager", request.Provider);
        Assert.AreEqual("7036,7040", request.EventIds);
        Assert.AreEqual(EventSeverity.Information | EventSeverity.Warning, request.Severities);
        Assert.AreEqual(5, request.PollingIntervalSeconds);
        Assert.AreEqual(37, request.MaximumEvents);

        await using var db = scenario.CreateDbContext();
        var finding = AssertSingle(await db.Findings.AsNoTracking().ToListAsync());
        Assert.AreEqual(profile.Id, finding.ProfileId);
        Assert.AreEqual(891, finding.RecordId);
        Assert.AreEqual(EventSeverity.Information, finding.Severity);
        Assert.AreEqual("A service changed state.", finding.Message);
        Assert.AreEqual("<Event id=\"7040\" />", finding.RawXml);
        var checkpoint = await db.Checkpoints.AsNoTracking().SingleAsync();
        Assert.AreEqual(CollectionState.Healthy, checkpoint.State);
        Assert.AreEqual(891, checkpoint.LastRecordId);
        Assert.AreEqual(occurredAt, checkpoint.LastEventTimestampUtc);
        Assert.IsNotNull(checkpoint.LastAttemptUtc);
        Assert.IsNotNull(checkpoint.LastSuccessUtc);
        Assert.IsNull(checkpoint.ErrorMessage);
    }

    [TestMethod(DisplayName = "Given an event already collected, when the next poll overlaps, then it is not stored twice")]
    public async Task OverlappingPolls_DoNotDuplicateFindings()
    {
        await using var scenario = await CollectionScenario.CreateAsync();
        var profile = await scenario.AddProfileAsync(EnabledProfile());
        var item = new CollectedEvent("Application", "EventLens", 100, 42, EventSeverity.Information,
            DateTimeOffset.Parse("2026-09-15T14:43:00Z"), "same event", "<Event />");
        scenario.Collector.NextBatch = new CollectionBatch([item], 42, item.TimestampUtc, false);

        await scenario.Service.CollectAsync(profile.Id, CancellationToken.None);
        await scenario.Service.CollectAsync(profile.Id, CancellationToken.None);

        await using var db = scenario.CreateDbContext();
        Assert.AreEqual(1, await db.Findings.CountAsync());
        Assert.HasCount(2, scenario.Collector.Requests);
    }

    [TestMethod(DisplayName = "Given an inaccessible event log, when collection runs, then the profile reports actionable access denied state")]
    public async Task AccessDenied_IsReported_WithoutEscapingTheProfileBoundary()
    {
        await using var scenario = await CollectionScenario.CreateAsync();
        var profile = await scenario.AddProfileAsync(EnabledProfile());
        scenario.Collector.Exception = new UnauthorizedAccessException("denied");

        await scenario.Service.CollectAsync(profile.Id, CancellationToken.None);

        await using var db = scenario.CreateDbContext();
        var checkpoint = await db.Checkpoints.AsNoTracking().SingleAsync();
        Assert.AreEqual(CollectionState.AccessDenied, checkpoint.State);
        StringAssert.Contains(checkpoint.ErrorMessage, "Run the worker with permission");
        Assert.AreEqual(0, await db.Findings.CountAsync());
    }

    [TestMethod(DisplayName = "Given a collector fault, when collection runs, then the failure is retained for diagnosis")]
    public async Task CollectorFailure_IsRetained_ForDiagnosis()
    {
        await using var scenario = await CollectionScenario.CreateAsync();
        var profile = await scenario.AddProfileAsync(EnabledProfile());
        scenario.Collector.Exception = new InvalidOperationException("synthetic failure");

        await scenario.Service.CollectAsync(profile.Id, CancellationToken.None);

        await using var db = scenario.CreateDbContext();
        var checkpoint = await db.Checkpoints.AsNoTracking().SingleAsync();
        Assert.AreEqual(CollectionState.Failed, checkpoint.State);
        Assert.AreEqual("InvalidOperationException: synthetic failure", checkpoint.ErrorMessage);
    }

    [TestMethod(DisplayName = "Given a disabled profile, when collection is requested, then the collector is not called")]
    public async Task DisabledProfile_IsNotCollected()
    {
        await using var scenario = await CollectionScenario.CreateAsync();
        var profile = await scenario.AddProfileAsync(EnabledProfile() with { IsEnabled = false });

        await scenario.Service.CollectAsync(profile.Id, CancellationToken.None);

        Assert.IsEmpty(scenario.Collector.Requests);
        await using var db = scenario.CreateDbContext();
        Assert.AreEqual(CollectionState.Disabled, (await db.Checkpoints.AsNoTracking().SingleAsync()).State);
    }

    [TestMethod(DisplayName = "Given more matching events than one batch, when collection completes, then backlog remains visible")]
    public async Task Backlog_IsVisible_OnTheCheckpoint()
    {
        await using var scenario = await CollectionScenario.CreateAsync();
        var profile = await scenario.AddProfileAsync(EnabledProfile());
        scenario.Collector.NextBatch = new CollectionBatch([], 1000, DateTimeOffset.UtcNow, true);

        await scenario.Service.CollectAsync(profile.Id, CancellationToken.None);

        await using var db = scenario.CreateDbContext();
        var checkpoint = await db.Checkpoints.AsNoTracking().SingleAsync();
        Assert.AreEqual(CollectionState.Healthy, checkpoint.State);
        StringAssert.Contains(checkpoint.ErrorMessage, "Backlog detected");
    }

    [TestMethod(DisplayName = "Given a profile changes during collection, when the old batch returns, then stale findings are discarded")]
    public async Task ProfileEditDuringCollection_DiscardsTheStaleBatch()
    {
        await using var scenario = await CollectionScenario.CreateAsync();
        var profile = await scenario.AddProfileAsync(EnabledProfile());
        var enteredCollector = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCollector = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scenario.Collector.Handler = async (_, cancellationToken) =>
        {
            enteredCollector.SetResult();
            await releaseCollector.Task.WaitAsync(cancellationToken);
            return new CollectionBatch(
                [new CollectedEvent("Application", "Old provider", 10, 77, EventSeverity.Error,
                    DateTimeOffset.UtcNow, "stale", "<Event />")],
                77, DateTimeOffset.UtcNow, false);
        };

        var collection = scenario.Service.CollectAsync(profile.Id, CancellationToken.None);
        await enteredCollector.Task;
        await using (var db = scenario.CreateDbContext())
        {
            var edited = await db.Profiles.Include(x => x.Checkpoint).SingleAsync();
            edited.Channel = "System";
            edited.CreatedAtUtc = edited.CreatedAtUtc.AddTicks(1);
            edited.Checkpoint.LastRecordId = null;
            edited.Checkpoint.State = CollectionState.NeverRun;
            await db.SaveChangesAsync();
        }
        releaseCollector.SetResult();
        await collection;

        await using var verificationDb = scenario.CreateDbContext();
        Assert.AreEqual(0, await verificationDb.Findings.CountAsync());
        var checkpoint = await verificationDb.Checkpoints.AsNoTracking().SingleAsync();
        Assert.AreEqual(CollectionState.NeverRun, checkpoint.State);
        Assert.IsNull(checkpoint.LastRecordId);
    }

    private static ProfileInput EnabledProfile() => new(
        "All events", true, "Application", null, [], EventSeverity.All, 5);

    private static T AssertSingle<T>(IEnumerable<T> items)
    {
        var materialized = items.ToArray();
        Assert.HasCount(1, materialized);
        return materialized[0];
    }

    private sealed class CollectionScenario : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<EventLensDbContext> _options;

        private CollectionScenario(SqliteConnection connection, DbContextOptions<EventLensDbContext> options,
            RecordingCollector collector, ProfileCollectionService service)
        {
            _connection = connection;
            _options = options;
            Collector = collector;
            Service = service;
        }

        public RecordingCollector Collector { get; }
        public ProfileCollectionService Service { get; }

        public static async Task<CollectionScenario> CreateAsync(int maximumEvents = 1000)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<EventLensDbContext>().UseSqlite(connection).Options;
            await using (var db = new EventLensDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var factory = new TestDbContextFactory(options);
            var collector = new RecordingCollector();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EventLens:MaximumEventsPerPoll"] = maximumEvents.ToString()
            }).Build();
            var service = new ProfileCollectionService(factory, collector, configuration,
                NullLogger<ProfileCollectionService>.Instance);
            return new CollectionScenario(connection, options, collector, service);
        }

        public EventLensDbContext CreateDbContext() => new(_options);

        public async Task<MonitoringProfile> AddProfileAsync(ProfileInput input)
        {
            var profile = ApiMappings.CreateProfile(input);
            await using var db = CreateDbContext();
            db.Profiles.Add(profile);
            await db.SaveChangesAsync();
            return profile;
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<EventLensDbContext> options)
        : IDbContextFactory<EventLensDbContext>
    {
        public EventLensDbContext CreateDbContext() => new(options);

        public Task<EventLensDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class RecordingCollector : IWindowsEventCollector
    {
        public List<CollectionRequest> Requests { get; } = [];
        public CollectionBatch NextBatch { get; set; } = new([], null, null, false);
        public Exception? Exception { get; set; }
        public Func<MonitoringProfile, CancellationToken, Task<CollectionBatch>>? Handler { get; set; }

        public Task<CollectionBatch> CollectAsync(MonitoringProfile profile, int maximumEvents,
            CancellationToken cancellationToken)
        {
            Requests.Add(new CollectionRequest(profile.Name, profile.Channel, profile.Provider, profile.EventIds,
                profile.Severities, profile.PollingIntervalSeconds, maximumEvents));
            if (Exception is not null) throw Exception;
            return Handler?.Invoke(profile, cancellationToken) ?? Task.FromResult(NextBatch);
        }
    }

    private sealed record CollectionRequest(string Name, string Channel, string? Provider, string EventIds,
        EventSeverity Severities, int PollingIntervalSeconds, int MaximumEvents);
}
