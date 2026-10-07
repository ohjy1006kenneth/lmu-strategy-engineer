# LMU Strategy Engineer

A local Le Mans Ultimate (LMU) race-strategy application that learns from the driver's own laps, builds pre-race strategies, and continuously adapts the plan during the race.

## Product

The application has two primary surfaces:

- **Pre-race desktop app** — race/session information, forecast, strategy-data source, driver averages, Flat Out / Fuel Save recommendations, editable stint plan, tyre strategy, and predicted race-time deltas.
- **In-race overlay** — minimal Fuel / VE / tyre-wear targets plus live strategy-change decisions.

The application is **LMU-only for V1**. It is not intended to become a setup analyzer, generic telemetry dashboard, spotter/radar, multi-sim framework, or cloud team-management product.

## Product principles

1. **Personal first.** Fuel, VE, tyre wear, tyre pace loss, and fuel-saving cost should converge toward the user's own driving data.
2. **Do not invent confidence.** If a usable model does not exist, show that limitation instead of fabricating certainty.
3. **LMU owns hard constraints.** Session length, fuel capacity, tyre availability, tyre inventory, weather and game-defined pit behavior should come from LMU whenever the current build exposes them.
4. **Optimize total race time.** Fuel, VE, fuel saving, pit loss, tyre service, tyre degradation, weather and compound choice are solved together.
5. **Live but quiet.** Recalculate continuously, but surface an Updated Strategy only when the optimum changes materially.
6. **Keep the UI decision-focused.** Avoid generic dashboard clutter.

## Current state

This repository begins from an interactive browser prototype. The prototype uses synthetic fixtures and exists to communicate product behavior and strategy logic; it is **not** the intended production architecture.

The intended production target is a standalone Windows application with a clean separation between:

- LMU integration,
- persistent driver/lap data,
- strategy/model libraries,
- pre-race UI,
- transparent in-race overlay,
- global wheel / keyboard / gamepad input.

## Strategy concepts

The user-facing optimizer exposes at most two useful strategies:

- **Flat Out** — normal measured consumption and pace.
- **Fuel Save** — shown only when a distinct feasible saving strategy exists.

The backend may evaluate many candidate stint layouts internally, but the UI should present only the best Flat Out and best Fuel Save options.

## Documentation

Start here if you are implementing or orchestrating work:

1. [Orchestration Handoff](docs/ORCHESTRATION_HANDOFF.md)
2. [Project Brief](docs/PROJECT_BRIEF.md)
3. [Architecture](docs/ARCHITECTURE.md)
4. [Strategy Engine](docs/STRATEGY_ENGINE.md)
5. [Data Model](docs/DATA_MODEL.md)
6. [LMU Integration](docs/LMU_INTEGRATION.md)
7. [UI / UX](docs/UI_UX.md)
8. [Testing](docs/TESTING.md)
9. [Simulation Results](docs/SIMULATION_RESULTS.md)
10. [Engineering Decisions](docs/DECISIONS.md)
11. [Roadmap](docs/ROADMAP.md)
12. [Research Notes](docs/RESEARCH_NOTES.md)
13. [Open Questions](docs/OPEN_QUESTIONS.md)

## Prototype

The latest interactive prototype is at [prototype/index.html](prototype/index.html), with a concise behavior reference at [prototype/README.md](prototype/README.md). It uses synthetic data and is a product-design artifact, not production code.

Historical stress-test artifacts are committed under `tests/simulations/`.

## Current implementation priority

The next engineering milestone is to create the production solution and domain boundaries, then replace synthetic fixtures with an LMU adapter and persistent local lap database while preserving the behavior documented in this repository.
