# Simulation Results

## Storage location

Current canonical simulation outputs belong in the root-level:

`simulation-results/`

Each canonical scenario owns one fixed directory:

~~~text
simulation-results/<ScenarioName>/
  report.md
  summary.json
  lap_log.csv
~~~

A new run **overwrites** those same files.

Do not create timestamped committed results for ordinary runs. Git history already provides historical versions.

Randomized stress/Monte-Carlo output should normally be temporary and uncommitted.

## Historical prototype simulations

Earlier product-design work used ad-hoc synthetic race scripts, including:
- a 24-hour Le Mans Hypercar stress race,
- a 1h45 Le Mans LMGT3 average-sim-racer race.

Those runs were useful for discovering architectural problems but are not the target simulation architecture.

Key findings retained from those tests:
- a two-stint solver is structurally invalid for endurance racing,
- timed-race lap count must be rolling rather than permanently fixed,
- dry tyre allocation must be whole-race state,
- Wet tyres must not decrement dry tyre allocation,
- neutralized laps must not contaminate normal pace learning,
- short showers should not automatically trigger Wet tyres,
- telemetry loss should hold the last valid strategy,
- consumption drift can materially change pit timing.

## Standardized simulator

Going forward there is one reusable simulator and **17 canonical timed-race scenarios**.

The canonical definitions and result policy are in:

`docs/TESTING.md`

The existing historical files should be treated as reference data only until they are replaced by results generated through the standardized simulator.
