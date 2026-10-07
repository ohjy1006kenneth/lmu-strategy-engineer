# Architecture

## Required separation

Production code should separate four concerns:

1. **LMU adapter** — reads simulator/session data and exposes stable internal DTOs.
2. **Persistence and learning** — stores clean laps and fits driver models.
3. **Strategy engine** — predicts and optimizes the remaining race.
4. **Presentation** — pre-race desktop UI and in-race overlay.

The browser prototype is not the production architecture.

## Proposed production stack

A practical target:

- C# / .NET for the Windows application and LMU integration.
- SQLite for persistent local history.
- Pure strategy/model libraries independent of UI.
- Windows-capable transparent always-on-top overlay.
- Raw Input / DirectInput / HID for global wheel/gamepad/keyboard input.

The exact UI framework can be selected during implementation, but the domain layer should not depend on it.

## Component flow

~~~
LMU
 |
 | shared memory / REST / supported current interfaces
 v
+------------------+
| LMU Adapter      |
+------------------+
        |
        v
+------------------+      +------------------+
| Session State    |<---->| Lap Repository   |
+------------------+      | SQLite           |
        |                 +------------------+
        v                         |
+------------------+              v
| Driver Models    |<------- Model Fitting
+------------------+
        |
        v
+------------------+
| Strategy Core    |
+------------------+
     |         |
     v         v
Pre-race UI   Live Overlay
~~~

## Strategy-core boundary

The strategy engine should consume a snapshot, not read LMU directly.

Suggested input:

- session constraints,
- current vehicle state,
- forecast,
- current race state,
- driver models,
- pit-service model,
- buffer laps,
- currently loaded strategy,
- optional user edits.

Suggested result:

- Flat Out candidate,
- optional Fuel Save candidate,
- recommended candidate,
- confidence / unsupported regions,
- reason for a material strategy change.

## Receding-horizon replanning

Live strategy is:

~~~
initial plan
 -> observe actual race
 -> update model state
 -> predict remaining race
 -> optimize remaining race
 -> compare to loaded plan
 -> notify only if materially different
~~~

The engine may recompute frequently. Notification should be intentionally quiet.

Material changes include:
- different next pit lap,
- different number of stops,
- different tyre action,
- different compound,
- wet/dry crossover,
- meaningful Fuel/VE target change,
- predicted race-distance change.

## Timed races

A timed race must not be represented internally as a permanently fixed lap count.

Maintain:
- time remaining,
- robust rolling lap-time estimate,
- expected pit losses,
- neutralization state,

to update predicted laps remaining.

Buffer laps extend the strategy's required energy coverage; they do not change the official race duration shown to the user.

## Strategy representation

Never implement a two-stint-only solver.

A race plan is:

~~~
RacePlan
 -> Stint
 -> Pit
 -> Stint
 -> Pit
 -> ...
 -> Finish
~~~

All production algorithms must work for arbitrary-length race plans.
