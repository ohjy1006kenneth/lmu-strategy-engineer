# Engineering Specification

This document defines the **production technology and architecture boundaries**.

For strategy equations, candidate generation, plan scoring and backend function contracts, use **[BACKEND.md](BACKEND.md)**.

## Technology stack

### Runtime

- C#
- .NET 10 LTS
- Windows-specific projects target `net10.0-windows`

### Desktop UI

- WPF / XAML
- MVVM
- `CommunityToolkit.Mvvm` is a reasonable lightweight choice

WPF is preferred because V1 is Windows-only and the product requires:

- mature Win32 interop,
- transparent/borderless windows,
- always-on-top and no-activate overlay behavior,
- global input,
- shared-memory/native integration,
- custom strategy visualization.

Do not use Electron, Tauri, React or WebView as the primary production architecture.

### Persistence

- **Desktop:** each installation has its own local SQLite database, using Microsoft.Data.Sqlite, with explicit migrations and durable offline-first lap/session/model storage.
- **Server:** each registered account has a **separate SQLite database file**, stored on the Raspberry Pi's mounted USB HDD. Other users' personal records must never be stored in that file.
- **Transport:** authenticated, versioned HTTPS sync API built with cross-platform ASP.NET Core. Clients never connect to SQLite, SMB, or a database port over the network.
- **Data ownership:** the Windows copy is authoritative for live session observations and in-race decisions. Server storage is a delayed personal replica/backup and sync destination; it is never required to run the strategy engine.
- **Identity:** a protected account-to-database mapping/identity registry is necessary; that small control-plane registry is distinct from the required one-personal-database-per-account storage layout.
- **Migrations:** both client and server databases require explicit forward-compatible schema versioning. See the public storage/sync requirements below.

Persist useful session/lap/model state rather than indiscriminate high-frequency telemetry.

### Input

Primary global control path:

- Windows Raw Input / HID

Wheel/gamepad/keyboard strategy controls must work while LMU owns focus.

### Visualization

Implement the strategy timeline as a native WPF custom control/drawing surface.

Prefer WPF drawing primitives first. Evaluate SkiaSharp only if later profiling or visual complexity justifies it.

### Testing and logging

- xUnit
- structured logging, preferably Serilog

## Project structure

Recommended:

~~~text
src/
  LmuStrategy.Domain/
  LmuStrategy.Strategy/
  LmuStrategy.Simulation/
  LmuStrategy.Persistence/
  LmuStrategy.SyncContracts/
  LmuStrategy.SyncClient/
  LmuStrategy.SyncServer/
  LmuStrategy.LmuAdapter/
  LmuStrategy.Windows/
  LmuStrategy.Overlay/

tests/
  LmuStrategy.Strategy.Tests/
  LmuStrategy.Simulation.Tests/
  LmuStrategy.LmuAdapter.Tests/
  LmuStrategy.Persistence.Tests/
  LmuStrategy.Sync.Tests/
~~~

Dependency direction:

~~~text
Domain
  ↑
Strategy / Simulation / Persistence / LMU Adapter
  ↑
Windows App / Overlay
~~~

Rules:

- Domain contains data/contracts, not UI.
- Strategy is deterministic for the same input and never reads LMU directly.
- Simulation drives the same strategy APIs used by production.
- LMU Adapter translates external game state into stable domain snapshots.
- Persistence stores lap/session/model history but does not decide strategy.
- SyncContracts and SyncClient depend on shared data contracts, not Windows UI or live LMU telemetry.
- SyncServer is a separate ASP.NET Core application: it authenticates accounts, enforces tenant isolation, and syncs personal data without strategy calculations.
- The local strategy engine, UI, and overlay never depend on server availability.
- WPF App and Overlay consume strategy results; they do not own strategy equations.

## Development and test topology

Hermes runs on a Raspberry Pi 5 / Ubuntu Server ARM64, so keep most of the solution cross-platform. The public sync API is an independent deployable service on that Pi; do not combine it with the Hermes gateway process.

~~~text
Raspberry Pi / Linux
- Domain
- Strategy
- Simulation
- Persistence
- LMU adapter abstractions
- xUnit + 17 canonical simulations
- Sync contracts, schema migration and isolated multi-user API tests
- Optional integration tests against a disposable local mounted-volume fixture

GitHub Actions / Windows
- full solution build
- WPF/XAML compile
- Windows-only tests
- win-x64 publish check

Windows gaming PC / real LMU
- local SQLite and offline sync-client tests
- shared-memory/REST verification
- overlay behavior
- Raw Input / wheel controls
- live integration validation
~~~

Raspberry Pi / Ubuntu Server deployment
- ASP.NET Core authenticated personal sync API listening on a private/loopback endpoint
- Application binaries, service configuration and logs on NVMe
- One user SQLite database per account on the mounted 1 TB USB HDD
- Cloudflare Tunnel for HTTPS public ingress; no exposed SQLite/SMB/admin ports
- Explicit HDD mount dependencies, backups, resource isolation and health checks

Keep Windows-specific code isolated in Windows-targeted projects such as:

~~~text
LmuStrategy.LmuAdapter.Windows
LmuStrategy.Windows
LmuStrategy.Overlay
~~~

Core projects should target normal `net10.0` and remain runnable on Linux.

The simulator and test doubles must exercise the same strategy APIs used by production, so Hermes can develop and regression-test backend behavior without LMU installed.

### Initial foundation (Issue #2)

`LmuStrategy.Core.slnx` contains the five portable libraries and the architecture test project. `LmuStrategy.slnx` adds the three Windows projects. Both solutions reference the same project files. [README](../README.md#build-the-foundation) provides the reproducible build/test commands.

Current project references are:

| Project (`LmuStrategy.` prefix) | Target | References / responsibility |
| --- | --- | --- |
| Domain | net10.0 | No project dependencies; domain types follow in Issue #4 |
| Strategy | net10.0 | Domain; deterministic strategy boundary |
| Simulation | net10.0 | Domain, Strategy; future simulator drives production strategy APIs |
| Persistence | net10.0 | Domain; future storage, no strategy decisions |
| LmuAdapter | net10.0 | Domain; portable adapter contract boundary |
| LmuAdapter.Windows | net10.0-windows | Domain, LmuAdapter; future verified native transport |
| Overlay | net10.0-windows / WPF | Domain, Strategy; presentation boundary |
| Windows | net10.0-windows / WPF executable | Domain, Strategy, Persistence, LmuAdapter, LmuAdapter.Windows, Overlay; composition root |

The libraries deliberately contain no placeholder racing logic, units, storage schema or game API. Adapter interfaces/capabilities will be defined against the domain snapshots in subsequent tasks; the Windows adapter does not connect to the game. WPF currently compiles only the application bootstrap and an empty overlay resource dictionary. Startup exits without a screen or background service. There is no rendered UI to accept in this foundation.

The xUnit architecture tests evaluate MSBuild properties and project references, including imported settings, to enforce the portable targets and permitted dependency directions. They also guard solution membership. `linux-core` runs them natively on Linux ARM64; `windows-wpf` builds the entire solution and XAML, runs tests, and checks self-contained win-x64 publish. Neither job is proof of visual or real-game behavior. Add meaningful domain and simulation tests as those features are implemented; there are no canonical scenario results yet.

SyncContracts, SyncClient and SyncServer remain future projects. Their canonical boundaries above still apply: shared data contracts only, no UI/native LMU dependency in sync, no server dependency in local strategy, and no live strategy calculation in the server. This foundation neither creates nor deploys a sync service.

## Runtime data flow

~~~text
LMU
  ↓
LMU Adapter
  ↓
Session / Telemetry / Scoring snapshots
  ├──> Persistence + model learning
  └──> StrategyInput
           ↓
       Strategy Core
           ↓
       StrategyResult
         ├──> Pre-race UI
         └──> Overlay
~~~

Personal backup/sync is a separate asynchronous path that must not block the strategy loop:

~~~text
Windows desktop
  local personal.db (SQLite) ──> durable outbound change queue
                                  |
                                  | authenticated HTTPS / TLS when online
                                  v
Raspberry Pi sync API ──> authenticate and authorize account ──>
  /srv/nas/lmu-strategy-engineer/users/<opaque-account-id>/personal.db

Remote accepted changes ──> local merge/revision checks ──> local personal.db

Any outage or sync conflict ──> explicit status / retry / user decision;
                                 strategy and overlay continue locally
~~~

Live strategy is receding-horizon:

~~~text
loaded plan
 -> observe current race
 -> update clean model state / fast deviations
 -> project remaining timed race
 -> solve remaining strategy
 -> compare against loaded plan
 -> surface only material action changes
~~~

## Domain ownership

Core domain types include:

- `StrategyInput`
- `StrategyResult`
- `RacePlan`
- `Stint`
- `Pit`
- `RaceState`
- `VehicleState`
- `LapRecord`
- tyre/energy/weather model snapshots
- capability/provenance records

Detailed required fields and equations are in `BACKEND.md`.

Use explicit units in names or types:

- `LapTimeSeconds`
- `FuelLitres`
- `FuelPerLapLitres`
- `VePercent`
- `TyreRemainingPercent`

Do not pass ambiguous unitless doubles across subsystem boundaries when a dedicated value type is practical.

## Persistence model

Persist enough lap context to reproduce model selection later.

A lap record should capture, when available:

- driver/car/class/track/layout/session identity
- lap and sector timing
- Fuel start/end/used
- VE start/end/used
- Fuel Ratio / mode context
- compound
- tyre age, remaining and wear per corner
- road-condition family
- road wetness
- track/air temperature
- RealRoad/grip state
- pit/formation/race-control/incident/traffic validity
- game build / schema / physics/BOP identity

Keep raw high-frequency telemetry only if a defined future feature needs it.

## Database versioning

Use explicit migrations.

At minimum maintain tables or equivalent entities for:

- sessions
- laps
- model metadata
- fitted model parameters
- capability/build snapshots
- user preferences

Every stored model should retain enough provenance to know which source/build/data produced it.

## Public per-user storage topology

**For the public app, registered users have two separate personal databases:**

1. Windows desktop: a local SQLite file in the user's application data directory (example: LocalApplicationData/LMUStrategyEngineer/personal.db). It must not require administrative privileges or write to the program installation directory. If the same account uses several PCs, each installation gets a local SQLite database.
2. Pi server: one private SQLite file per account on the mounted USB HDD, e.g. /srv/nas/lmu-strategy-engineer/users/<opaque-account-id>/personal.db. The opaque directory name must be generated server-side and must never be derived from a user-controlled path, email address or display name.

The HDD is the persistent store for server **personal user databases**. The Pi NVMe may hold the server code, service configuration, logs and a minimal protected identity/tenant registry, but must not substitute an NVMe personal-data database for the mandated HDD layout.

HDD filesystem should be a locally mounted Linux filesystem with correct file locking (ext4 is suitable). Each server SQLite database is opened by server-owned processes only. SQLite WAL sidecars must remain on the same local filesystem; do not serve database files over SMB, NFS, Cloudflare Tunnel, or a client-mounted network share. Restrict account database directories to the service identity and maintain explicit account-level authorization in the API; filesystem isolation alone is not sufficient. Provide bounded connection/transaction concurrency per user.

Before handling requests, the service verifies that the configured HDD filesystem is actually mounted, writable, and the expected volume. If it is disconnected, unmounted or read-only, **fail closed**, expose a health/readiness failure, and never create new personal databases on the underlying NVMe mountpoint.

The Pi is the developer-operated hosting platform and currently a single point of failure. A working API or Cloudflare Tunnel is not proof of production reliability. Implement off-device encrypted backups, database-consistent snapshots/restore tests, resource isolation from Hermes and the NAS, free-space monitoring, and a documented maintenance/outage policy before public release.

## Authenticated personal synchronization

The server's role is registration/login/account recovery, tenant authorization and syncing *only that account's personal data*. It does not run strategy calculations and must not read live LMU telemetry from the game.

- Use a versioned HTTPS API, exposed through the existing Cloudflare Tunnel, with authenticated per-request account identity, standard secure token/session lifecycle, revocation, timeouts, rate limits and input/size limits. Keep internal APIs and administrative services private.
- Choose and document a maintained authentication implementation (and account recovery/verification policy) before public rollout. The account registry maps authenticated identity to an opaque user database. Client-provided account IDs or database paths are never trusted authorization.
- Record stable globally unique logical record IDs, origin device IDs, a schema/sync protocol version, per-record revision and deletion markers, and idempotent client operation IDs. Use durable local pending-change queues with acknowledgements only after the server transaction commits.
- Pull server changes incrementally using a durable, account-scoped server sequence/cursor; retries or repeated batches must not create duplicate laps or corrupt trained model provenance.
- Support local writes and strategy evaluation with no network. A network loss, unavailable Pi, expired token, or full HDD must **never** prevent a race from continuing. The app visibly distinguishes unsynced, syncing, synced, authentication-required, conflicted and error states.
- Define deterministic reconciliation for append-only lap observations and revision-checked updates of editable preferences/model state. For simultaneous conflicting edits from multiple devices, retain both versions or request a user resolution; **never** silently apply last-writer-wins or erase data. Propagate deletes with durable tombstones and explicitly defined retention windows.
- Account linking of previously offline client data must be explicit and idempotent. Do not automatically mix one user's offline history with another account merely because a device is reused or a user signs in.
- Provide user-accessible data export, account deletion (including server personal DB and associated identity mapping), privacy notice, retention/backup policy, and clear reporting of deletion/backup-lag semantics. Do not treat a server copy as an independently durable backup unless a verified off-host backup exists.
- Default sync to compact personal session, lap, model and settings records. Do not upload continuous high-frequency raw telemetry, wheel identifiers, local credentials, or unrelated PC files without an approved necessity.
- Use authenticated per-user API contract tests and cross-account IDOR tests. Never create an unauthenticated endpoint for arbitrary DB enumeration, file downloads or database administration.

Server-side account isolation and synchronization correctness are **release-blocking**. The exact endpoint names, account provider and user-facing merge UX should be settled as versioned contracts in scoped implementation issues before code is shipped.

## LMU adapter

A value being visible in LMU's UI does **not** prove an external application can read it.

Expose capability-oriented interfaces, conceptually:

~~~text
GetSession()
GetVehicle()
GetLiveTelemetry()
GetScoring()
GetForecast()
GetTyreInventory()
GetAvailableCompounds()
GetRecommendedCompoundConditions()
GetPitEstimate()
~~~

Potential transports:

- shared memory / memory-mapped structures
- local REST / Swagger

The adapter must classify required data as:

- verified current shared-memory field
- verified current REST field
- derived
- cached
- unavailable

Persist:

- game build
- adapter/schema version
- capability flags

On schema/build change, fail closed: unknown/missing fields become unavailable rather than being silently reinterpreted.

## Fuel capacity

Use the current loaded car/session capacity only when verified from LMU.

Do not make a hard-coded car-name-to-capacity database the primary source.

If capacity is unavailable, expose that capability failure to the strategy core instead of inventing a number.

## Tyre integration

When verified, expose:

- legal/available compounds
- dry tyre inventory
- tyre-management options
- compound-condition recommendation metadata

Wet tyres must never decrement the dry-tyre inventory ledger.

## Opponent timing for Wet fallback

The strategy backend can use normalized same-car/same-class opponent pace when the user has no personal Wet model.

The adapter's responsibility is to expose only opponent observations that can be supported by current LMU data:

- lap/sector timing
- class/car identity
- race-control/pit context where available
- tyre compound only if reliably exposed

Do not infer "Wet tyre" solely from rainfall if opponent compound is not observable.

The exact normalization equations are in `BACKEND.md`.

## Pit-service integration

Preferred source order:

1. verified current LMU pit/service estimate,
2. app-maintained model validated against the current game build,
3. explicit lower-confidence fallback.

Do not require the user to perform a calibration stop just to use the product.

Keep pit-service provenance visible to the strategy core.

## Overlay architecture

Use a WPF borderless transparent window plus Win32 styles where necessary for:

- always-on-top,
- no activation,
- click-through behavior,
- exclusion from normal task-switching when appropriate.

Do not place the strategy engine inside the overlay process/view model.

The overlay receives a small immutable presentation state from the application/service layer.

## Global input

Use Raw Input / HID as the primary direction.

Required semantic action:

- tap compact -> expand
- tap expanded/no update -> compact
- tap with strategy update -> Keep Current
- long press with strategy update -> Replace

Keep the physical binding configurable and separate from those semantic actions.

## Packaging

The Windows executable is distributed publicly, while the authenticated Pi sync API is a **separately deployed server component**. Never embed server administrative credentials, database paths, or private API tokens in the client installer. Publish only client-safe configuration such as the HTTPS origin. Provide client/server protocol compatibility and upgrade gates before public distribution.

Phase 1:

- standard Visual Studio / `dotnet` development build.

Initial distribution:

- self-contained Windows x64 publish.

Choose installer/updater technology after the core app works. Packaging must not block strategy/backend implementation.

## Failure boundaries

Subsystem failure must degrade explicitly:

- LMU adapter loss -> hold last valid live state where safe
- local database write failure -> strategy continues with in-memory state and logs warning
- server unavailable / token expired / sync failure -> continue local operation and queue changes, show explicit sync status
- server HDD unavailable -> sync API refuses writes and remains unready; never fall back to NVMe for users' personal databases
- sync conflict -> preserve both versions/evidence and require deterministic reconciliation, not silent overwrite
- unsupported LMU capability -> feature disabled/degraded, not fabricated
- overlay failure -> core strategy/session service should remain alive where practical

Backend-specific failure behavior is detailed in `BACKEND.md`.
