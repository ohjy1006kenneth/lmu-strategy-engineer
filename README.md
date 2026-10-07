# LMU Strategy Engineer

A standalone Windows race-strategy application for **Le Mans Ultimate**.

It learns from the driver's own laps, builds pre-race Flat Out / Fuel Save strategies, and continuously re-evaluates the remaining race as Fuel, VE, tyres, weather and race conditions change.

## Status

Product specification and interactive prototype are complete enough to begin production implementation.

The current `prototype/index.html` is an **executable UX reference using synthetic data**. It is not the production application.

## Production stack

- C# / .NET 10 LTS
- WPF desktop app + in-game overlay
- pure C# strategy/domain libraries
- SQLite / `Microsoft.Data.Sqlite`
- verified LMU shared memory + local REST/Swagger behind an adapter
- Windows Raw Input / HID for global wheel controls
- xUnit deterministic simulation tests
- structured local logging

## Documentation

There are intentionally only five project docs:

1. **[HANDOFF](docs/HANDOFF.md)** — start here for Hermes/orchestration and implementation order.
2. **[PRODUCT](docs/PRODUCT.md)** — product scope, UI/UX and accepted behavior.
3. **[ENGINEERING](docs/ENGINEERING.md)** — architecture, strategy algorithms, data model and LMU integration.
4. **[TESTING](docs/TESTING.md)** — reusable simulator, 17 canonical scenarios, invariants and result policy.
5. **[RESEARCH](docs/RESEARCH.md)** — public references and unresolved implementation questions.

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

Follow `docs/HANDOFF.md` and build the production .NET solution, domain model, reusable simulator and initial strategy engine before polishing the WPF UI.
