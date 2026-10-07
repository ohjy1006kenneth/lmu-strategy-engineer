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

- SQLite
- `Microsoft.Data.Sqlite`
- explicit schema migrations

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

Rules:

- Domain contains data/contracts, not UI.
- Strategy is deterministic for the same input and never reads LMU directly.
- Simulation drives the same strategy APIs used by production.
- LMU Adapter translates external game state into stable domain snapshots.
- Persistence stores lap/session/model history but does not decide strategy.
- WPF App and Overlay consume strategy results; they do not own strategy equations.

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

Phase 1:

- standard Visual Studio / `dotnet` development build.

Initial distribution:

- self-contained Windows x64 publish.

Choose installer/updater technology after the core app works. Packaging must not block strategy/backend implementation.

## Failure boundaries

Subsystem failure must degrade explicitly:

- LMU adapter loss -> hold last valid live state where safe
- database write failure -> strategy continues with in-memory state and logs warning
- unsupported LMU capability -> feature disabled/degraded, not fabricated
- overlay failure -> core strategy/session service should remain alive where practical

Backend-specific failure behavior is detailed in `BACKEND.md`.
