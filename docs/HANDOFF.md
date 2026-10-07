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
- only interrupts the driver when a materially better plan exists.

Do not turn the product into a setup analyzer, generic telemetry dashboard, spotter, multi-sim framework or cloud team product.

## Production stack

Use:

- C# / .NET 10 LTS
- WPF for desktop UI and overlay
- pure C# domain/strategy libraries with no UI dependency
- SQLite via `Microsoft.Data.Sqlite`
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

The UI can depend on the core. The core must never depend on WPF.

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
9. establish SQLite schema/migrations,
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
- local database schema,
- concise developer setup instructions.

It does not need polished production UI yet.
