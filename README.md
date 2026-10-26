# LMU Strategy Engineer

A publicly distributed, Windows-first race-strategy application for **Le Mans Ultimate**. The strategy engine and in-race overlay run locally; authenticated personal-data synchronization is an additional service hosted on the project's Raspberry Pi.

It learns from the driver's own laps, builds pre-race Flat Out / Fuel Save strategies, and continuously re-evaluates the remaining race as Fuel, VE, tyres, weather and race conditions change.

## Public distribution and personal data

Each registered user owns an isolated local SQLite database on their desktop and an independently stored server-side SQLite database on the Pi's USB HDD. The app remains capable of racing and recording data while offline; synchronization resumes later. The server never calculates live race strategy and is never required for the overlay to operate.

Users access only their own records through a secured HTTPS API. The server is not a shared SQLite file and does not expose SMB/database ports to the public. Authentication, synchronization, consent, account deletion, database backups, and outage recovery are **public-release requirements**, not implemented features yet. See [PRODUCT](docs/PRODUCT.md), [ENGINEERING](docs/ENGINEERING.md), and [TESTING](docs/TESTING.md).

## Status

The .NET 10 solution and project boundaries are bootstrapped, with Linux ARM64 core and Windows/WPF CI. The portable Domain library provides immutable race, energy, per-wheel tyre and strategy DTOs with explicit units and JSON roundtrip coverage. The portable LMU adapter now provides snapshot/provenance contracts, a compatibility guard and a deterministic scripted source. Strategy generation, the reusable race simulator, persistence and verified real-game transports remain subsequent tasks.

The current `prototype/index.html` is an **executable UX reference using synthetic data**. It is not the production application.

## Build the foundation

Install a stable .NET 10 SDK for your OS/architecture (`linux-arm64` on the Hermes Pi 5). `global.json` permits stable .NET 10 feature-band updates. From the repository root:

~~~sh
dotnet --info
dotnet restore LmuStrategy.Core.slnx
dotnet build LmuStrategy.Core.slnx --configuration Release --no-restore
dotnet test LmuStrategy.Core.slnx --configuration Release --no-build
~~~

These commands run natively on Ubuntu ARM64 without Windows targeting packs, WPF, LMU, databases or services. Initial restore needs access to NuGet. Specify a solution explicitly because the repository contains two solutions.

On Windows with the .NET 10 SDK:

~~~powershell
dotnet restore LmuStrategy.slnx
dotnet build LmuStrategy.slnx --configuration Release --no-restore
dotnet test LmuStrategy.slnx --configuration Release --no-build
dotnet publish src/LmuStrategy.Windows/LmuStrategy.Windows.csproj --configuration Release --runtime win-x64 --self-contained true --output out/windows-win-x64
~~~

The `linux-core` GitHub Actions job builds/tests on hosted Ubuntu ARM64. The `windows-wpf` job performs the full solution build, WPF/XAML compilation, tests and self-contained Windows x64 publish check on Windows. Hermes must rely on that Windows job for these checks. The desktop bootstrap exits without opening a window; it is not a usable racing application yet. Publish output is a local CI build check, not a release or installer.

The current xUnit suites check evaluated project targets, dependency boundaries, solution membership, domain unit bounds, immutable snapshots, JSON roundtrips and adapter contracts (missing data, provenance, disconnect/reconnect and identity changes). They do not implement the 17 racing simulations or verify WPF rendering, overlay/input behavior, SQLite/sync, or real LMU interfaces. Those require their later scoped tasks and, where applicable, the Windows gaming PC. See [ENGINEERING](docs/ENGINEERING.md) for the exact boundaries.

Architecture tests inspect the source checkout recorded in the test assembly at build time. The checkout must remain available; rebuild if it moves. Build outputs can live elsewhere: pass the same `--artifacts-path /tmp/lmu-foundation-artifacts` to restore, build and test for a read-only source checkout. `linux-core` uses external artifacts to guard this workflow.

## Production stack

- C# / .NET 10 LTS
- WPF desktop app + in-game overlay
- pure C# strategy/domain libraries
- One personal SQLite database on each user's Windows PC and a separate per-user SQLite database on the Raspberry Pi's USB HDD
- Authenticated HTTPS sync service (ASP.NET Core) for personal backup and cross-device synchronization; no direct remote SQLite access
- verified LMU shared memory + local REST/Swagger behind an adapter
- Windows Raw Input / HID for global wheel controls
- xUnit deterministic simulation tests
- structured local logging

## Documentation

There are intentionally only six project docs:

1. **[HANDOFF](docs/HANDOFF.md)** — start here for Hermes/orchestration and implementation order.
2. **[PRODUCT](docs/PRODUCT.md)** — product scope, UI/UX and accepted behavior.
3. **[ENGINEERING](docs/ENGINEERING.md)** — production stack, project architecture, persistence and LMU adapter boundaries.
4. **[BACKEND](docs/BACKEND.md)** — strategy-generation equations, models, scoring and backend function contracts.
5. **[TESTING](docs/TESTING.md)** — reusable simulator, 17 canonical scenarios, invariants and result policy.
6. **[RESEARCH](docs/RESEARCH.md)** — public references and unresolved implementation questions.

Do not create or modify `AGENTS.md`; the repository owner manages it separately.

## Prototype

Open:

`prototype/index.html`

Use it to understand interaction and visual behavior. Do not copy its temporary synthetic constants/equations into production without validation.

## Simulation results

Canonical simulator output belongs in:

~~~text
simulation-results/<ScenarioName>/
  report.md
  summary.json
  lap_log.csv
~~~

Each new run overwrites the previous files for that scenario. Do not create timestamped committed result folders; Git history is the history.

## Product principles

- LMU only for V1.
- Personal driver data becomes the normal baseline.
- Game-defined constraints come from verified LMU interfaces.
- Strategy optimizes complete predicted race time, not one metric.
- Flat Out and optional Fuel Save are the user-facing strategy choices.
- Dry/Wet models remain separate.
- Wet tyres do not consume dry tyre allocation.
- Timed-race lap count is continuously projected.
- Live replanning is automatic but intentionally quiet.
- The overlay only asks the driver to act when the recommended plan materially changes.

## Next step

Build the reusable simulator and strategy engine on the unit-safe domain contracts, following `docs/HANDOFF.md`, before polishing the WPF UI.
