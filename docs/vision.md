# EventLens vision

## Vision statement

EventLens gives a household one understandable place to see what is happening across its Windows computers.

It collects selected Windows events from each enrolled machine, preserves enough context to investigate problems after they occur, and presents the results through a local Blazor dashboard. It is designed for the person who maintains several family computers but does not have enterprise infrastructure, dedicated security staff, or time to sign in to every machine individually.

EventLens remains self-hosted on the local network. It does not require a cloud account, send household telemetry to an external service, or attempt to become an enterprise monitoring platform.

## The problem

A household may have several Windows computers used by people with different levels of technical judgment. Software is installed, services change, applications crash, Windows Defender raises findings, and machines restart unexpectedly. The useful evidence is scattered across local Windows Event Logs and is often inspected only after the affected computer has already become difficult to use.

Windows Event Viewer can expose that evidence, but it requires visiting each machine and knowing where to look. EventLens should make the common question easier to answer:

> What changed, failed, or needs attention across the computers I look after?


## Intended outcome

One computer acts as the EventLens hub. Each monitored Windows computer runs a lightweight EventLens agent. Agents collect only the events selected by monitoring profiles and send durable findings to the hub over the local network.

The hub provides a browser-based dashboard that allows an authorized household administrator to:

- See whether enrolled computers and their agents are healthy.
- Create and assign monitoring profiles without visiting every machine.
- Review findings from one computer or across the household.
- Understand when collection is healthy, delayed, denied access, or failing.
- Reopen the dashboard and see findings gathered while it was closed.
- Trace a finding back to its device, profile, channel, provider, event ID, severity, timestamps, rendered message, and raw event XML.

The experience should feel like a household appliance: install it, pair a machine, select what matters, and let it run.

## Who it is for

EventLens is for a technically capable person responsible for a small number of Windows computers on a trusted home or similarly small private network.

It is especially useful when those computers are used by family members who install unfamiliar applications, change system settings, encounter recurring crashes, or otherwise create support mysteries. EventLens helps the administrator investigate operating-system and application evidence from a single point.

It is not intended to monitor a person's private content or browsing activity.

Basically, the goal is to create a centralized location for troubleshooting and diagnostics when you might have a couple of kids (or adults) that like to install questionable apps or browse the dark web in teenage angst.

## Product principles

### Local first

Collection, storage, identity, and administration remain under the user's control. Normal operation does not depend on a cloud service or external database.

### Useful evidence, not surveillance

EventLens reports selected Windows events and collection health. It is not a parental surveillance tool, browser-history collector, keylogger, antivirus product, or endpoint detection and response platform.

### Secure by default

Network access must be deliberate. Agents initiate outbound connections to the hub; monitored machines do not expose remote administration endpoints. Every agent has a unique, revocable identity established through short-lived, single-use pairing. Traffic is encrypted and authenticated, secrets are protected by Windows facilities, and permissions are limited to what collection requires.

The local worker API must not become a network API merely by changing its bind address. Enrollment, ingestion, dashboard access, and local agent administration are separate trust boundaries.

### Durable before immediate

Temporary network outages must not lose findings. Agents retain checkpoints and a bounded local outbox, the hub acknowledges accepted batches, and retries are idempotent. Live presentation is valuable, but stored evidence remains the source of truth.

### Explainable operation

The dashboard should make ownership and failure visible: which device produced a finding, which profile matched it, when the event occurred, when it arrived, and why collection is delayed or unavailable.

### Constrained configuration

Profiles remain understandable data, not remote code. A profile selects a Windows Event Log channel, optional provider and event IDs, severity levels, polling interval, and enabled state. EventLens does not use profile distribution as a path for arbitrary commands, scripts, executable extensions, or unrestricted queries.

### Small-network scale

The design should work smoothly for a household-sized fleet without importing enterprise deployment, identity, or operations machinery. Features must justify their cost for that setting. This is not an enterprise application, nor does it try to be.

## The product shape

### EventLens Agent

The agent runs independently as a Windows Service on every monitored computer. It:

- Receives versioned profile assignments from the hub.
- Periodically reads matching events newer than the durable checkpoint.
- Isolates profile failures so one inaccessible log does not stop other collection.
- Retains useful event data even when Windows cannot render the message.
- Queues unsent findings locally when the hub is unavailable.
- Retries safely without creating visible duplicates.
- Reports health, version, collection status, and queue state to the hub.

Collectors run as bounded work inside the agent. The hub does not remotely launch processes or directly query Windows Event Logs.

### EventLens Hub

The hub is the single network destination and system of record for the household. It:

- Enrolls, identifies, and revokes agents.
- Stores devices, desired profiles, acknowledgements, findings, and status.
- Accepts authenticated, bounded, idempotent batches from agents.
- Provides the administrator's authenticated application boundary.
- Keeps agent ingestion separate from browser-facing administration.

Only the hub requires an inbound local-network firewall rule.

### Blazor dashboard

The Blazor application is the reason EventLens can become a zero-install dashboard for the local network rather than a UI tied to one Windows desktop. It should provide:

- A household overview showing enrolled devices, last contact, and collection health.
- Device-level profile management and finding history.
- A unified findings timeline with device and profile filters.
- Clear detail views containing the complete retained evidence.
- Live updates backed by durable cursors or acknowledgements, so reconnecting cannot skip stored findings.
- Bookmarkable device, profile, finding, and filtered-list locations.

Remote browser access must be authenticated and encrypted. Keeping the dashboard loopback-only remains a valid secure default until certificate trust and administrator setup can be presented cleanly.

## Monitoring profiles

A monitoring profile contains:

- Name
- Enabled state
- One target device or an explicit device assignment
- One Windows Event Log channel
- Optional provider name
- Optional event IDs
- Selected severity levels: Critical, Error, Warning, Information, and Verbose
- Polling interval
- Version or revision used to reconcile agent state

The user must be able to create, edit, enable, disable, assign, and delete profiles from the hub. Changes are desired state: an agent retrieves a newer revision, validates it, applies it, and reports the result.

## Findings and status

A finding is a Windows event matched by a profile on a specific device. EventLens retains, when available:

- Device and profile identity
- Channel and provider
- Event ID and Windows Event Record ID
- Severity
- Event timestamp
- Agent collection timestamp
- Hub receipt timestamp
- Rendered message
- Raw event XML

Collection status must distinguish at least:

- Device online, delayed, or offline
- Profile collecting normally
- Profile disabled
- Agent unable to access the configured log
- Profile collection failed
- Findings waiting to be delivered
- Hub unavailable from the agent's perspective

Errors must be understandable and retained without crashing the dashboard or stopping unrelated profiles or devices.

## Security and privacy outcome

EventLens treats the home network as untrusted enough to require real protection. A machine on the network must not gain access merely because it can reach the hub.

The intended security posture includes:

- HTTPS for every network connection.
- Unique device credentials rather than one household-wide agent secret.
- Short-lived, single-use enrollment material stored only in protected form.
- Device credentials generated and protected on the enrolled machine.
- Immediate device revocation and a clear re-pairing path.
- Authorization that prevents one agent from acting as another device or accessing household findings.
- Authenticated administrator sessions for the dashboard, using secure server-managed cookies rather than browser-stored bearer tokens.
- Bounded request sizes, rate limits, validation, and safe handling of event text and XML as untrusted input.
- Least-privilege Windows service access, with sensitive logs requiring explicit opt-in.
- Configurable retention and a clear explanation of what data remains on agents and the hub.

Mutual TLS device certificates are the preferred initial agent identity. JWT access tokens remain an option if EventLens later needs multiple independent APIs or standards-based application delegation, but a bearer token is not a substitute for secure enrollment, transport encryption, revocation, or protected long-lived credentials.

## Relationship to the original MVP

The original MVP deliberately proves the local collection chain before introducing network concerns:

1. Create a constrained monitoring profile.
2. Run collection independently from the UI.
3. Store findings and checkpoints durably in local SQLite.
4. Review and filter findings through a local Blazor UI.
5. Resume after restart without intentional replay or visible duplicates.
6. Retain profile-specific access and collection failures.

That local vertical slice remains the foundation. The networked vision extends its ownership boundaries rather than discarding them:

- The current worker evolves into the installed agent.
- The local checkpoint remains authoritative for Windows Event Log progress.
- A durable outbox is added between local collection and hub ingestion.
- The hub becomes the household-wide store and administration surface.
- Device identity is added to every profile assignment, status report, batch, and finding.

## Deliberate non-goals

EventLens is not intended to become:

- An enterprise SIEM, observability platform, or fleet-management system
- A cloud-hosted telemetry service
- An antivirus, EDR, intrusion-prevention, or automatic-remediation product
- A parental surveillance or content-monitoring tool
- A remote shell, software deployment mechanism, or arbitrary command runner
- A replacement for Windows Event Viewer for advanced forensic analysis
- A domain, Active Directory, or enterprise identity-management product
- A plugin platform or general-purpose query language

Advanced correlation, detection rules, alerts, notifications, `.evtx` import, exports, charts, and retention controls may be considered only when they serve the household diagnostic outcome and do not compromise the product's clarity or security.

## Definition of success

The vision is realized when a household administrator can:

1. Install the hub on one Windows computer.
2. Securely pair several Windows computers without configuring inbound access on them.
3. Assign understandable monitoring profiles from the dashboard.
4. Have agents continue collecting through dashboard closures and temporary network outages.
5. Open one authenticated dashboard and see trustworthy device health and findings from across the household.
6. Investigate a finding back to its source evidence without visiting the affected machine.
7. Revoke a lost or untrusted device without disrupting the rest of the household.
8. Understand where EventLens stores data, who can access it, and what it does not collect.

EventLens succeeds by making small-network Windows diagnostics durable, centralized, secure, and understandable—not by accumulating the most features.
