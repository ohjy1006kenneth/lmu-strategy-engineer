# Architecture

## Required separation

Production code should separate four concerns:

1. **LMU adapter** — reads simulator/session data and exposes stable internal DTOs.
2. **Persistence and learning** — stores clean laps and fits driver models.
3. **Strategy engine** — predicts and optimizes the remaining race.
4. **Presentation** — pre-race desktop UI and in-race overlay.

The browser prototype is not the production architecture.

## Chosen production stack

The production app is **not** the browser prototype.

### Runtime and language

- **C#**
- **.NET 10 LTS**
- Windows-specific projects target `net10.0-windows`.

### Desktop UI

- **WPF on .NET 10**
- XAML for layout/styles.
- MVVM for application state and commands.
- Prefer `CommunityToolkit.Mvvm` for lightweight observable/view-model plumbing.

Why WPF:
- Windows-only is acceptable for V1.
- mature Win32 interop,
- straightforward transparent/borderless windows,
- suitable for an always-on-top non-activating overlay,
- easy integration with Raw Input / HID / shared-memory code,
- strong data binding and custom drawing support.

Do not use Electron, Tauri, React, or a WebView as the primary production UI.

WinUI 3 is a valid modern Windows framework, but for this product WPF is preferred because overlay/window/input interoperability is more important than modern Windows shell styling.

### Strategy and domain

Use pure C# class libraries with **no WPF dependency**:

- `LmuStrategy.Domain`
- `LmuStrategy.Strategy`
- `LmuStrategy.Simulation`

The strategy engine must be runnable from unit tests and simulation tools without launching the desktop app.

### LMU integration

- Memory-mapped/shared-memory access in C# for verified LMU shared-memory structures.
- `HttpClient` for verified local LMU REST/Swagger endpoints.
- Win32 interop only behind adapter/platform abstractions.
- Capability detection per current LMU build.

### Persistence

- **SQLite**
- Prefer `Microsoft.Data.Sqlite` with explicit schema/migrations.
- Keep persistence DTOs separate from domain objects.
- Do not store raw high-frequency telemetry unless a future feature truly requires it; persist useful lap/session/model data.

### Overlay and input

Overlay:
- WPF borderless transparent window.
- Win32 extended window styles for no-activate / tool-window / click-through behavior when required.
- always-on-top state controlled explicitly.

Input:
- **Windows Raw Input / HID** as the primary global wheel/button path.
- keyboard through Win32/WPF input as appropriate.
- add specialized controller APIs only when needed; do not make a deprecated DirectInput wrapper a core dependency.

### Strategy visualization

Implement the stint/tyre strategy timeline as a custom WPF control or drawing surface.

Prefer:
- WPF `DrawingContext` / custom `FrameworkElement`,
- or SkiaSharp only if profiling/design complexity justifies it later.

Do not embed the HTML graph in a WebView.

### Logging

- structured logging, preferably **Serilog**,
- rolling local files,
- diagnostics must never block the telemetry/strategy loop.

### Testing

- **xUnit** for unit and deterministic simulation tests.
- Keep scenario fixtures serializable and reproducible.
- Add property/invariant tests where valuable.

### Packaging

Initial development:
- normal `dotnet`/Visual Studio build.

Distribution:
- self-contained x64 Windows publish initially,
- choose installer/update technology only after the core app works.
- MSIX/Velopack-style packaging can be evaluated later; it is not a Phase 1 dependency.

### Repository layout

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

Dependency rule:

~~~text
Domain
  ↑
Strategy / Simulation / Persistence / LMU Adapter
  ↑
Windows App / Overlay
~~~

The UI can depend on the core. The core must never depend on the UI.

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
