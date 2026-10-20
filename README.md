# LMU Strategy Engineer

A publicly distributed, Windows-first race-strategy application for **Le Mans Ultimate**. The strategy engine and in-race overlay run locally; authenticated personal-data synchronization is an additional service hosted on the project's Raspberry Pi.

It learns from the driver's own laps, builds pre-race Flat Out / Fuel Save strategies, and continuously re-evaluates the remaining race as Fuel, VE, tyres, weather and race conditions change.

## Public distribution and personal data

Each registered user owns an isolated local SQLite database on their desktop and an independently stored server-side SQLite database on the Pi's USB HDD. The app remains capable of racing and recording data while offline; synchronization resumes later. The server never calculates live race strategy and is never required for the overlay to operate.

Users access only their own records through a secured HTTPS API. The server is not a shared SQLite file and does not expose SMB/database ports to the public. Authentication, synchronization, consent, account deletion, database backups, and outage recovery are **public-release requirements**, not implemented features yet. See [PRODUCT](docs/PRODUCT.md), [ENGINEERING](docs/ENGINEERING.md), and [TESTING](docs/TESTING.md).

## Status

Product specification and interactive prototype are complete enough to begin production implementation.

The current `prototype/index.html` is an **executable UX reference using synthetic data**. It is not the production application.

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

Follow `docs/HANDOFF.md` and build the production .NET solution, domain model, reusable simulator and initial strategy engine before polishing the WPF UI.
