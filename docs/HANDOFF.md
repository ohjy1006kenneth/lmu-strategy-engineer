# Hermes Orchestration Handoff

## Task-scoped reading

Do not load the entire engineering/research/reference package into
every implementation session. Begin with the assigned GitHub issue,
its Kanban card, previous checkpoint and relevant actual source/tests.

- Read `README.md` and the **relevant section** of this handoff.
- Product/UX: `docs/PRODUCT.md` and `prototype/index.html` only if
  the task affects the user-facing interaction. The prototype uses
  synthetic data and temporary equations.
- Architecture/persistence/LMU adapters: `docs/ENGINEERING.md`.
- Strategy math: `docs/BACKEND.md`.
- Simulation/acceptance: `docs/TESTING.md`.
- Unknown external LMU capability: `docs/RESEARCH.md` and actual
  verified evidence.

Preserve previously accepted work and existing issue/card/branch/PR
identity. After an interrupted session, resume only the missing
step rather than restarting the plan.

If the prototype conflicts with the canonical documentation, follow
the documentation and surface any unresolved product decision.

Do **not** create or modify `AGENTS.md`. The repository owner manages
it separately.

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

## Autonomous development ownership — Hermes Kanban

The adopted software-development architecture uses GitHub Issues for
requirements/acceptance, Hermes Kanban for priority/dependency/execution
state, a deterministic Kanban dispatcher and a **single active
implementation task** in an isolated workspace. It replaces the
previous four permanent LMU profiles and Symphony-triggered development
jobs. Historic Symphony branches and existing PRs are preserved.

Target role names for this project's analogous Hermes workflow are
`lmu-lead` and `lmu-dev` **when configured**, not an instruction to
create profiles without an approved installation. The owner has
specified a **10 a.m./10 p.m. planning cadence for CupMemo only**;
verify or request the LMU lead cadence separately rather than copying
it. A scheduled lead handles issue decomposition, acceptance criteria,
Kanban priorities/dependency gates and review/merge handoffs; no
permanent LLM coordination conversation is required.

```text
You / ChatGPT / GitHub Issues
            |
            v
lmu-lead — scheduled planning (LMU cadence to verify)
            |-- GitHub requirements and acceptance criteria
            +-- Hermes Kanban priority, dependencies, state
                                      |
                                      v
                          Deterministic dispatcher
                                      |
                                      v
                                lmu-dev
                       One leased implementation task
                       Isolated branch and workspace
                                      |
                                      v
                         Hermes openai-codex
                           Credential A -> B
                                      |
                                      v
                            GitHub PR + CI
                                      |
                                      v
                       Review and merge gates
                                      |
                                      v
                 Windows app / server release gates
```

**Division of responsibility:**

- GitHub is the durable **source of requirements**, architecture
  decisions, acceptance evidence and final outcome. One root Kanban
  card maps to one GitHub Issue; Kanban is not a second product backlog.
- The `lmu-lead` owns bounded planning, dependency admission,
  acceptance/review and GitHub lifecycle, including reconciling existing
  branches/PRs. It cannot invent game-data capabilities, merge bypasses
  or live server permissions.
- The `lmu-dev` implements one issue at a time in a stable isolated
  branch/workspace, runs relevant portable tests and creates/updates
  **one** linked PR. It must not approve or merge its own PR.
- The `openai-codex` provider uses separately authorized credential
  pool **A -> B** only for verifiable ordinary quota/capacity shortfall
  and only if supported by the installed Hermes runtime and provider
  terms. Never use account switching to bypass an actual provider
  safety refusal or missing permission. If all permitted capacity is
  unavailable, preserve state and wait.
- A **deterministic, non-LLM supervisor** owns worker liveness,
  cooldowns, persisted attempt limits, leases, restart reconciliation,
  duplicate suppression and safe resumption. It must respect the
  actual shared Pi-wide capacity gate. It is not a code reviewer or a
  permanent project-lead agent.
- Review and merge require the actual current-head required GitHub CI
  and appropriate independent technical/security/visual evidence.
  Windows WPF, wheel inputs and real LMU interfaces cannot be
  established by Linux-only CI; those remain Windows/physical-device
  acceptance gates.
- Deployment of the public sync server and distribution of the Windows
  client are **separate, independently gated outcomes**. A merged PR,
  passing Linux build or already-running Pi service is not public
  product acceptance.

**Identity and recovery:** Use `ohjy1006kenneth/lmu-strategy-engineer#N`
as the durable GitHub issue key and record the real Kanban card ID,
assigned branch, PR URL/head SHA, attempts and checkpoint. Before
dispatch, reconcile the existing claim, issue, card, workspace, remote
branch and any PR, **including already-open PR #38 and PR #39**. Their
`symphony/GH-*` names are historical identities, not permission to
rename/delete them or open duplicate PRs. Do not create another worker
because the previous run is quiet; terminate or reconcile the exact
process and lease first.

The owner maintains `README.md` and these six canonical project docs.
The existing instruction **not to create or modify `AGENTS.md`**
remains in force. Do not introduce extra developer/reviewer profile
roles simply to mimic historical descriptions.

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
