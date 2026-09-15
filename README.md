# EventLens

EventLens is a local Windows Event Log monitor. A standalone worker polls enabled monitoring profiles, persists matching events and collection status in SQLite, and exposes a loopback-only HTTP API. A separate Blazor Server application uses that API to manage profiles and review findings.

The delivered local scope is captured in the [MVP document](docs/mvp.md). The intended household-network outcome is described separately in the [project vision](docs/vision.md).

## Requirements

- Windows 10/11 or Windows Server
- .NET 10 SDK (developed and verified with SDK 10.0.401)
- Permission to read each configured Windows Event Log channel

No database server or external service is required.

## Run

### Visual Studio

After cloning the repository, open `event-lens.slnx`, select the shared `App` launch profile in the Visual Studio toolbar, and press F5 or Ctrl+F5. The profile starts both `EventLens.Worker` and `EventLens.App`, then opens the UI in the browser.

### Command line

Start the worker first and leave it running:

```powershell
dotnet run --project .\EventLens.Worker
```

In another terminal, start the UI:

```powershell
dotnet run --project .\EventLens.App --launch-profile http
```

Open `http://localhost:5249`. The UI can be closed without stopping collection. When it is reopened, it reads the findings accumulated by the worker.

The worker API listens on `http://127.0.0.1:5178` by default. It is deliberately bound to loopback and has no authentication because remote access is outside this MVP.

## Configuration

Settings live under `EventLens` in each executable's `appsettings.json`. They follow standard .NET precedence: base settings, `appsettings.{Environment}.json`, user secrets in Development where configured, environment variables, then command-line arguments.

Worker settings:

| Setting | Default | Purpose |
| --- | --- | --- |
| `DatabasePath` | `%LOCALAPPDATA%\EventLens\eventlens.db` | SQLite database owned exclusively by the worker process |
| `MaximumEventsPerPoll` | `1000` | Bounds the number of log records processed for one profile in one poll |

UI settings:

| Setting | Default | Purpose |
| --- | --- | --- |
| `WorkerApiBaseUrl` | `http://127.0.0.1:5178` | Local worker endpoint used by the Blazor application |
| `DataProtectionPath` | `%LOCALAPPDATA%\EventLens\DataProtectionKeys` | Optional override for DPAPI-protected ASP.NET Core keys |
| `FindingsPollIntervalSeconds` | `1` | Finding refresh cadence while the findings page is open; bounded to 1–60 seconds |
| `ProfileStatusPollIntervalSeconds` | `5` | Profile and worker status refresh cadence; bounded to 1–300 seconds |
| `WorkerApiTimeoutSeconds` | `10` | UI-to-worker HTTP timeout; bounded to 1–120 seconds |

For example:

```powershell
$env:EventLens__DatabasePath = 'D:\EventLens\eventlens.db'
dotnet run --project .\EventLens.Worker
```

The same double-underscore convention applies to every setting. For example, `EventLens__FindingsPollIntervalSeconds=2` overrides the UI refresh cadence for the selected environment without changing a tracked JSON file.

## Collection behavior

- A new profile begins at its creation time, so enabling it does not intentionally scan the entire existing log.
- Subsequent polls resume after the stored Windows Event Record ID.
- Findings and the new checkpoint are committed in one SQLite transaction.
- A unique `(ProfileId, RecordId)` index prevents a visible duplicate if work is retried.
- One profile's permission or collection failure is stored on that profile and does not stop other profiles.
- Message rendering failure does not discard the event; metadata and raw XML are still retained.
- While a findings page is open, the UI checks the worker every second using its durable finding cursor. The worker's collection interval remains controlled by the profile.

Some channels, especially `Security`, require an elevated worker or an account granted access to that log. EventLens reports access denial in the profile status rather than terminating the worker.

## Solution responsibilities

- `EventLens.Core` — domain models, API contracts, profile rules, and the collection port.
- `EventLens.Persistence` — EF Core SQLite schema and local application-data path policy.
- `EventLens.Windows` — bounded, checkpoint-aware Windows Event Log reader.
- `EventLens.Worker` — sole database owner, polling scheduler, transaction boundary, and local API.
- `EventLens.App` — Blazor UI and typed HTTP client; it never opens SQLite directly.

## Verify

The test suite is intentionally split by purpose. `Unit` tests cover deterministic rules, mappings, severity conversion, paging, and scheduling boundaries. `Behavioral` tests express profile-collection scenarios in Given/When/Then language while exercising the real SQLite boundary with a controlled event source. `Persistence` tests verify database constraints and cascade behavior.

```powershell
dotnet build .\event-lens.slnx
dotnet test .\event-lens.slnx --no-build
```

### Disclaimer
Artificial intelligence is actively used in the development of this project, including its documentation. Put plainly, I could not iterate as quickly without it. That does not mean the project has been “vibe-coded” or accepted without scrutiny.

An architect or structural engineer must understand how a building is put together to design it responsibly, but they do not personally perform every part of its construction. Much of that work is delegated to others operating within the design and its constraints. I use AI similarly: as a tool for accelerating implementation, exploration, and documentation. The architecture, technical direction, review, and responsibility for the finished work remain my own.
