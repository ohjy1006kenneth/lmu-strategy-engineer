# Orchestration Handoff

## Purpose

This is the primary handoff document for an orchestration/coding agent taking over implementation.

Read the repository documentation before writing production code.

## Source-of-truth order

If documents appear to conflict, use this order:

1. docs/DECISIONS.md
2. docs/PROJECT_BRIEF.md
3. docs/UI_UX.md
4. docs/STRATEGY_ENGINE.md
5. docs/ARCHITECTURE.md
6. docs/DATA_MODEL.md
7. docs/LMU_INTEGRATION.md
8. docs/TESTING.md
9. docs/ROADMAP.md
10. docs/OPEN_QUESTIONS.md
11. prototype/index.html
12. prototype/README.md

The prototype is an executable UX reference, but some internal mock equations/constants are intentionally temporary.

## Current repository state

There is no production LMU application yet.

The committed browser prototype and its behavior specification demonstrate:
- pre-race strategy UX,
- Flat Out / Fuel Save selection,
- editable strategy,
- strategy graph,
- stint hover data,
- compact/expanded overlay,
- live Updated Strategy state,
- Keep Current / Replace actions.

The original browser prototype used synthetic data.

Do not recreate or wrap the browser prototype as the production application; implement the documented behavior using the production architecture.

## First implementation objective

Create a maintainable production skeleton that can be extended by multiple coding agents.

Recommended high-level projects:

~~~
src/
  LmuStrategy.Domain/
  LmuStrategy.Strategy/
  LmuStrategy.Persistence/
  LmuStrategy.LmuAdapter/
  LmuStrategy.App/
  LmuStrategy.Overlay/

tests/
  LmuStrategy.Strategy.Tests/
  LmuStrategy.Simulation.Tests/
  LmuStrategy.LmuAdapter.Tests/
~~~

Exact naming can change, but keep dependency direction clean.

Suggested dependency direction:

~~~
Domain
  ^
Strategy
  ^
App / Overlay

Domain
  ^
Persistence

Domain
  ^
LMU Adapter
~~~

UI must not own telemetry parsing or core strategy equations.

## First coding milestone

Before implementing polished UI:

1. establish solution/project structure,
2. implement domain records/types,
3. implement StrategyInput / RacePlan / Stint / Pit / StrategyResult,
4. implement deterministic simulation harness,
5. implement a basic Flat Out arbitrary-length planner,
6. encode invariants from docs/TESTING.md,
7. implement LMU adapter interfaces without fabricating unavailable data,
8. establish SQLite schema/migrations.

Then begin wiring real LMU reads.

## LMU integration rule

Do not assume a field exists because it appears in this document, a third-party tool, or the LMU UI.

For every field:
- identify the current data source,
- verify it on the installed/current build,
- record support in a capability model,
- handle absence explicitly.

## Strategy implementation priorities

Implement in this order:

1. rolling timed-race lap prediction,
2. Flat Out arbitrary-length stint construction,
3. fuel capacity and VE constraints,
4. pit/service model,
5. dry tyre allocation ledger,
6. tyre wear prediction,
7. editable-plan evaluation,
8. tyre service economics,
9. Auto compound,
10. Fuel Save candidate generation,
11. weather crossover,
12. live material-change detection,
13. personal saving-cost model.

Do not optimize premature details before the plan representation is correct.

## Testing expectation

Every behavior-changing strategy PR should add or update deterministic simulation coverage.

At minimum, regularly run:
- short no-stop sprint,
- one-stop race,
- race with splash-stop opportunity,
- Fuel Save eliminates a stop,
- long endurance multi-stop race,
- dry -> wet -> dry,
- telemetry dropout,
- missed pit,
- dry tyre allocation pressure.

## Agent coordination guidance

Prefer small, reviewable commits.

For each task:
- state the assumption,
- identify relevant docs,
- implement tests first or alongside code,
- avoid changing accepted product decisions silently.

When a required LMU fact is uncertain:
- document it as an open integration question,
- implement capability-aware behavior,
- do not invent a constant.

## What not to do

Do not:
- build a two-stint-only solver,
- hard-code per-car fuel capacities as the primary source,
- hard-code Soft/Medium/Hard as pace rankings,
- count Wet tyres against dry allocation,
- reintroduce a Recalculate overlay button,
- show Ahead/Behind text in the compact overlay,
- add a generic telemetry dashboard,
- require users to drive every compound combination before Auto works.

## Definition of a good first handoff result

A strong first implementation pass should leave the repository with:

- compiling solution,
- clear project boundaries,
- unit-aware domain model,
- simulation tests,
- initial arbitrary-length Flat Out strategy engine,
- adapter capability interfaces,
- local database schema,
- concise developer setup instructions.

It does not need polished production UI yet.
