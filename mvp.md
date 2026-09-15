# EventLens MVP scope

EventLens is a local Windows Event Log collector with two independently running processes:

- `EventLens.Worker` owns SQLite, polls Windows Event Log channels, advances durable checkpoints, and exposes a loopback HTTP API.
- `EventLens.App` is a Blazor Server client of that API. Closing it does not interrupt collection.

## Included

- Create, edit, enable, disable, and delete monitoring profiles.
- One channel per profile with optional provider and event-ID filters.
- Critical, Error, Warning, Information, and Verbose severity selection.
- Configurable polling intervals.
- Durable findings, checkpoints, and understandable profile failure status.
- Newest-first finding lists with severity, provider, event ID, and message filters.
- Cursor-based UI polling and full finding details, including raw event XML.
- Configurable local storage and loopback endpoints with no external services.

## Completion boundary

The MVP is complete when the worker and UI start independently on Windows, enabled profiles collect without duplicate replay after restart, findings remain available while the UI is closed, filters and details are functional, and one inaccessible log does not stop unrelated profiles.

## Explicitly deferred

Push subscriptions, SignalR, server-sent events, remote collection, multi-channel profiles, advanced XML queries, detection rules, correlation, dashboards, OpenTelemetry, Aspire, `.evtx` import, export, annotations, authentication, cloud services, notifications, remediation, installers, plugins, and custom query languages are outside this MVP.
