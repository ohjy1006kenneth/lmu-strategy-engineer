# Hermes Orchestration Handoff

## Read first

This repository is ready to move from product prototype to production implementation.

Read these files in order:

1. `README.md`
2. `docs/PRODUCT.md`
3. `docs/ENGINEERING.md`
4. `docs/BACKEND.md`
5. `docs/TESTING.md`
6. `docs/RESEARCH.md`
7. `prototype/index.html`

If the prototype conflicts with the documentation, follow the documentation. The prototype contains synthetic data and temporary equations.

Do **not** create or modify `AGENTS.md`. The repository owner manages that separately.

## Mission

Build a standalone Windows strategy application for **Le Mans Ultimate** that:

- learns the driver's Fuel, VE, tyre wear, tyre degradation and fuel-saving behavior,
- creates pre-race Flat Out and, when useful, Fuel Save strategies,
- lets the user edit the strategy and compare predicted race-time deltas,
- continuously replans during the race,
- only interrupts the driver when a materially better plan exists,
- serves a public Windows user base with one local SQLite database per user installation and one isolated server SQLite database per registered user on the Pi's USB HDD, synchronized through an authenticated API.

Do not turn the product into a setup analyzer, generic telemetry dashboard, spotter, multi-sim framework or shared cloud team product. Personal account backup/sync **is** in scope; cloud live strategy execution is not.

## Production stack

Use:

- C# / .NET 10 LTS
- WPF for desktop UI and overlay
- pure C# domain/strategy libraries with no UI dependency
- SQLite via `Microsoft.Data.Sqlite` (local Windows user DB and private Pi HDD per-user DB)
- cross-platform ASP.NET Core personal sync API; local strategy remains fully offline-capable
- verified LMU shared memory and local REST/Swagger behind an adapter
- Windows Raw Input / HID for global wheel/button input
- xUnit for unit/simulation tests
- structured local logging, preferably Serilog

The HTML prototype is **not** the production product. Do not use Electron, Tauri, React or WebView as the primary application architecture.

## Recommended project structure

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
~~~

Dependency direction:

~~~text
Domain
  ↑
Strategy / Simulation / Persistence / LMU Adapter
  ↑
Windows App / Overlay
~~~

The UI can depend on the core. The core must never depend on WPF. The server sync project must not depend on Windows/UI/LMU adapter implementations; preserve local Domain and Strategy isolation.

## Development environment

Hermes runs on a Raspberry Pi 5 / Ubuntu Server ARM64. The Pi also hosts the public authenticated sync service, with **per-user SQLite databases on the mounted USB HDD** and application code/configuration on the NVMe. Implement safe mount checks before serving write traffic.

Do most development and testing there using the cross-platform Domain, Strategy, Simulation, Persistence and adapter-abstraction projects.

Use:
- Windows GitHub Actions for full WPF/Windows build validation.
- The Windows gaming PC only for real LMU integration, overlay and wheel/input validation.

Keep Windows-only code isolated so Linux development never blocks backend work.

## Autonomous development ownership

This repository is one of three projects managed by a single Hermes multiplexed gateway. LMU has four dedicated profiles: lmu-lead, lmu-implementer, lmu-code-reviewer, and lmu-visual-reviewer. Only the lead needs direct Discord routing; collaboration evidence belongs in linked Hermes Kanban cards, and GitHub Issues/PRs/CI are the durable integration record.

Role model preferences (verify availability in the installed Hermes version):
- Lead: GPT-6.1 Sol (high), fallback Kimi K2.8 Preview.
- Implementer: Kimi K3-256K (high), fallback GPT-6 Luna.
- Code reviewer: GPT-6.1 Sol (high), fallback Kimi K2.8 Preview.
- Visual reviewer: Kimi K2.8 Preview (high), fallback GPT-6 Luna.

The two Codex credentials are one provider pool; a role gets exactly one fallback model. Follow provider usage terms: do not use a membership credential for prohibited unattended work. If no compliant provider is available, persist state and retry rather than skipping review.

The external continuity controller owns dispatch/reconciliation across task completions, provider cooldowns, reviewer handoffs and restarts; it must not assume a lead chat remains running. Work in isolated branches, apply deterministic tests and independent code and visual reviews, and check actual Windows/WPF artifacts rather than claiming Linux renders represent WPF. Keep a single heavy worker globally on the Pi initially. Preserve the repo's instruction not to create or edit AGENTS.md.

## Public sync delivery boundary

Public distribution requires local-and-remote **per-user** SQLite persistence, secure account identity, independently scoped server database files, an authenticated versioned HTTPS sync API, offline retries, conflict-safe synchronization, export/delete, backup and restore, and privacy/abuse controls. The Pi USB drive is **not** a local network database clients may open directly.

Do not deploy or advertise the sync service as production-ready until tenant-isolation, migration, conflict, offline-recovery, load, and backup/restore gates in TESTING.md pass. A live Pi NAS or a functioning Cloudflare Tunnel is not evidence that a secure public API has been built.

## First implementation milestone

Before polished UI:

1. create the .NET solution and project boundaries,
2. implement unit-aware domain types,
3. implement `StrategyInput`, `StrategyResult`, `RacePlan`, `Stint` and `Pit`,
4. implement the reusable deterministic simulator described in `docs/TESTING.md`,
5. implement the plan evaluator and backend contracts in `docs/BACKEND.md`,
6. implement a basic arbitrary-length Flat Out planner,
7. enforce Fuel/VE/tyre invariants,
8. create LMU adapter interfaces plus capability detection,
9. establish local SQLite schema/migrations and sync-safe record identities; plan an isolated per-user server schema and migration process,
10. add structured logging and repeatable tests.

Then begin wiring verified LMU data.

## Backend source of truth

`docs/BACKEND.md` is the source of truth for strategy equations, plan scoring, candidate generation, weather fallback mathematics and backend function boundaries. Do not derive production equations from `prototype/index.html`.

## Strategy implementation order

1. rolling timed-race lap prediction,
2. Flat Out arbitrary-length stint construction,
3. fuel-capacity and VE constraints,
4. pit/service model,
5. dry tyre allocation ledger,
6. tyre wear prediction,
7. editable-plan evaluation,
8. tyre service economics,
9. Auto compound integration,
10. Fuel Save candidate generation,
11. weather crossover including normalized field fallback,
12. live material-change detection,
13. personal fuel-saving cost model.

Do not optimize small details before the race-plan representation is correct.

## Non-negotiable behavior

- arbitrary number of stints; never a two-stint-only solver,
- LMU V1 is timed-race focused,
- user-facing strategies are Flat Out and optional Fuel Save,
- race-start Fuel/VE are optimization variables,
- Wet tyres never consume dry tyre allocation,
- Auto compound uses verified LMU event availability/recommendation data when available,
- dry and Wet personal models are not blindly mixed,
- timed-race lap count is a rolling estimate,
- live replanning is automatic and quiet,
- no Recalculate button in the overlay,
- overlay update: tap = Keep Current, long press = Replace,
- compact overlay uses Fuel / VE / average tyre-wear target, actual and red/green delta,
- no Ahead/Behind wording.

## LMU integration discipline

Never assume a value is readable because the game UI displays it.

For every required field:

1. identify the current LMU data source,
2. verify it against the installed/current build,
3. document semantics,
4. expose support through adapter capability flags,
5. explicitly handle missing capability,
6. do not replace missing data with unexplained constants.

Open integration questions live in `docs/RESEARCH.md`.

## Development workflow

Prefer small reviewable commits.

For any strategy-behavior change:

- state the assumption,
- update/add deterministic simulation coverage,
- run relevant canonical scenarios,
- verify invariants,
- update documentation if behavior changed.

Use the 17 canonical scenarios in `docs/TESTING.md` as the regression backbone.

Canonical simulation outputs go to:

`simulation-results/<ScenarioName>/`

with fixed names:

- `report.md`
- `summary.json`
- `lap_log.csv`

Overwrite them on the next run. Do not create timestamped committed result trees.

## Good first handoff result

A strong first implementation leaves:

- a compiling .NET solution,
- clean project boundaries,
- unit-aware domain model,
- reusable deterministic simulator,
- initial Flat Out arbitrary-length planner,
- invariant tests,
- LMU adapter capability interfaces,
- local database schema and a documented per-user server/sync design (the full public sync service may follow the core milestone),
- concise developer setup instructions.

It does not need polished production UI yet.
