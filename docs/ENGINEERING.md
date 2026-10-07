# Engineering Specification

## Technology stack

### Runtime

- C#
- .NET 10 LTS
- Windows projects target `net10.0-windows`

### UI

- WPF / XAML
- MVVM
- `CommunityToolkit.Mvvm` is a reasonable lightweight choice

Why WPF:
- Windows-only V1
- mature Win32 interop
- transparent/borderless overlay support
- suitable for always-on-top / no-activate behavior
- straightforward integration with Raw Input, HID and shared memory

Do not use Electron, Tauri, React or WebView as the production architecture.

### Persistence

- SQLite
- `Microsoft.Data.Sqlite`
- explicit schema/migrations

Persist useful session/lap/model data rather than indiscriminate high-frequency telemetry.

### Input

Primary production path:

- Windows Raw Input / HID

Keyboard/gamepad/wheel support should work while LMU owns focus.

### Visualization

Implement the strategy timeline in native WPF drawing/custom controls.

Use SkiaSharp only later if profiling/design complexity justifies it.

### Testing/logging

- xUnit
- structured logging, preferably Serilog

## Architecture

Separate:

1. LMU adapter
2. persistence/model learning
3. strategy engine
4. desktop UI
5. overlay

The strategy engine consumes immutable snapshots and never reads telemetry directly.

## Core domain

### StrategyInput

Contains:

- session constraints
- current vehicle/race state
- forecast
- driver models
- pit-service model
- buffer laps
- loaded strategy
- optional user-edited constraints

### StrategyResult

Contains:

- Flat Out candidate
- optional Fuel Save candidate
- recommended candidate
- confidence/coverage
- material-change explanation

### RacePlan

~~~text
RacePlan
  Stint[]
  Pit[]
  predicted finish
  dry tyre inventory remaining
  predicted total time
  confidence
~~~

### Stint

At minimum:

- index
- start/end lap
- lap count
- compound
- starting Fuel
- starting VE
- Fuel Ratio
- target Fuel/lap
- target VE/lap
- saving intensity
- predicted tyre start/end
- predicted pace/degradation

### Pit

At minimum:

- pit lap
- target Fuel
- target VE
- Fuel Ratio
- tyre action
- next compound
- pit-lane loss
- service time
- incremental cost

## Timed race projection

LMU V1 is treated as timed-race focused.

Do not freeze an estimated lap count before the race.

Maintain a rolling estimate from:

- time remaining
- robust current pace
- expected pit loss
- neutralization/race-control state

Buffer laps extend strategy energy coverage, not official race duration.

## Flat Out

Flat Out means normal modeled consumption and pace.

Optimize:

- race-start Fuel
- race-start VE
- stop count
- pit laps
- refill amounts
- Fuel Ratio
- tyre changes
- compound

Do not force start VE = 100%.
Do not force minimum start VE.

Compare carrying more physical fuel against future pit service saved.

## Fuel Save

Show only when a distinct feasible candidate exists.

Internally evaluate options such as:

- extending first stint
- extending later stints
- eliminating a splash
- eliminating a full stop
- uneven saving by stint

Economics:

~~~text
NetFuelSaveBenefit =
    PitTimeAvoided
  + ServiceTimeAvoided
  - DrivingTimeLostWhileSaving
  - AdditionalTyreCost
~~~

### Personal saving model

Desired learned relationship:

~~~text
consumption reduction -> corrected lap-time penalty
~~~

A simple early personal model may be convex:

~~~text
DeltaTimeSave = a*s + b*s^2
~~~

Before sufficient personal coverage, a documented empirical prior may be used with lower confidence.

## Tyres

### Wear

UI shows average four-tyre wear.

Backend keeps FL/FR/RL/RR separately.

Simple projection:

~~~text
remaining_next =
    remaining_now
  - wear_per_lap * laps
~~~

### Pace loss

Wear prediction and pace-loss prediction are separate.

Possible personal degradation model:

~~~text
x = fraction of tyre life used
DeltaTimeTyre = a*x + b*x^2
~~~

Extrapolation is allowed, but uncertainty must increase outside observed coverage.

### Change economics

~~~text
Keep:
  cumulative predicted tyre-related pace loss

Change:
  tyre service
  + cold/outlap penalty
  + pit-lane loss if extra stop
  + dry tyre inventory consequence
~~~

If already stopping for Fuel/VE, pit-lane loss is already paid; compare marginal tyre-service cost.

### Dry allocation

Wet tyres never decrement dry tyre allocation.

Track dry inventory across the whole race.

### Compound selection

Do not treat Soft / Medium / Hard as a universal fast-to-slow ladder.

Auto should begin from:

1. compounds actually available in the event,
2. verified LMU condition/recommendation metadata,
3. personal wear/degradation economics.

## Lap data and model partitioning

Persist enough context to reproduce model selection later.

A clean lap should include:

- driver/car/class/track/layout/session identity
- lap/sector times
- Fuel start/end/used
- VE start/end/used
- Fuel Ratio / engine-map context
- compound
- tyre age/remaining/wear per corner
- road-condition family
- road wetness
- track/air temperature
- RealRoad/grip context if available
- validity/race-control/incident/traffic flags
- game build / schema / physics/BOP identity when available

### Filtering

Hard reject from normal learning:

- incomplete lap
- pit in/out
- formation
- telemetry loss
- severe incident/spin

Exclude neutralized laps from normal pace learning:

- yellow
- Slow Zone
- Safety Car

Traffic can still help consumption while being downweighted/corrected for pace.

### Model keys

Tyre model at minimum:

~~~text
driver + car + track/layout + road-condition family + compound
~~~

Fuel/VE should generally survive dry compound changes:

~~~text
driver + car + track/layout + road-condition family + relevant environment
~~~

## Weather and no-personal-Wet fallback

Dry and Wet models remain separate.

### Preferred source order

1. personal Wet model
2. normalized same-car field data
3. normalized same-class field data
4. no Wet prediction if evidence is insufficient

Do **not** copy the fastest Wet driver's absolute lap time.

For usable opponent `i`:

~~~text
wet_factor_i =
    representative_wet_lap_i
  / representative_dry_lap_i
~~~

Use comparable valid samples:

- same car preferred, otherwise same class
- reject pit laps
- reject yellow / Slow Zone / Safety Car laps
- reject obvious incidents
- require enough stable Wet samples
- prefer samples near the current road-wetness / track-condition region

Combine with a robust statistic:

~~~text
field_wet_factor = robust_median(wet_factor_i)
~~~

Then:

~~~text
predicted_user_wet_pace =
    user_dry_baseline
  * field_wet_factor
~~~

At the same time measure/predict the user's current slick pace as the track gets wetter.

Wet crossover compares:

~~~text
predicted user Wet pace
vs
user slick pace at current wetness
~~~

The field fallback is temporary. As valid personal Wet laps accumulate, transition toward the personal model.

If opponent compound is not reliably exposed, do not label a field lap as a Wet-tyre sample solely because it is raining. Reduce or disable the fallback unless the adapter has sufficient evidence.

Use hysteresis to prevent Slick/Wet recommendation flicker.

Wet switch economics:

~~~text
future Wet time saved
>
Wet tyre service
+ extra pit-lane loss if off-cycle
+ cold/outlap effects
~~~

If already stopping, pit-lane loss is already paid.

## LMU adapter

A value being visible in LMU's UI does not prove external readability.

Suggested capability-oriented adapter:

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

Every field must be classified as:

- verified current shared-memory field
- verified current REST field
- derived
- cached
- unavailable

Persist current build/schema/capabilities.

Never make a hard-coded car fuel-capacity table the primary source.

## Pit estimate

Preferred source order:

1. verified current LMU estimate
2. app-maintained values validated against the current build
3. explicit low-confidence fallback

Do not require a user calibration pit stop just to use the app.

## Live replanning

~~~text
loaded plan
 -> observe race
 -> update model state
 -> project remaining race
 -> optimize remaining race
 -> compare to loaded plan
 -> surface only material action change
~~~

A material change can include:

- next pit lap
- stop count
- tyre action
- compound
- Wet/Slick crossover
- meaningful Fuel/VE target change
- predicted race-distance change
