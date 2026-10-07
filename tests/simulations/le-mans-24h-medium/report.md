# 24h Le Mans medium-driver stress test

## Calibration
- Synthetic Ferrari 499P medium-driver clean dry baseline: **3:36.50**.
- Synthetic test tank: **90 L** (test fixture, not asserted as the real 499P tank capacity).
- Dry green consumption target: **7.31 L/lap**, **8.24% VE/lap**.
- Dry race tyre allocation: **56 tyres**.
- Initial strategy: **32 stints / 31 scheduled stops**, longest stint 12 laps, with 1 buffer lap.

## Result
- Completed **374 laps** in **24.05 h**.
- Pit stops: **31**.
- Best non-neutralized dry lap: **3:35.75**.
- Average simulated fuel use: **7.34 L/lap**.
- Average simulated VE use: **8.26%/lap**.
- Dry tyres used: **52/56**, leaving **4**. Wet tyres did not consume the dry allocation.
- Personal wet laps learned during the race: **21**.
- Automated checks: **41/41 passed**, **0 failed.**

## Injected race events
- Pre-race forecast expected damp conditions around 5.8 h; actual dampness arrived at 4.5 h.
- Sustained wet period, followed by a drying crossover.
- Short damp shower around hour 18 that was intentionally too brief to justify a wet stop.
- Slow Zone around hour 10.2.
- Safety-car-like neutralization around hour 14.0.
- +3.5% consumption drift from hour 13.
- Telemetry dropout around hour 12.2.
- Incident around hour 16.7 forcing an unscheduled stop and repair.
- Game-side optimal compound recommendation changes with the simulated operating window; Auto follows that signal at pit opportunities.

## Edge tests
- PASS — arbitrary stint planner sums exact race distance
- PASS — no planned stint exceeds VE/fuel limit
- PASS — pre-race plan has >2 stints
- PASS — buffer lap included
- PASS — recent-N clamp concept
- PASS — qualifying has no N modifier concept
- PASS — fuel cap never exceeded
- PASS — VE never exceeded 100
- PASS — VE never went negative
- PASS — fuel never went negative
- PASS — dry tyre allocation never exceeded 56
- PASS — wet tyre changes do not spend dry allocation
- PASS — rain produced wet compound use
- PASS — early rain produced strategy event
- PASS — field wet fallback available before personal wet sample
- PASS — personal wet samples learned during race
- PASS — brief evening damp shower did not force Wet
- PASS — optimal compound signal changed across temperatures
- PASS — soft used in cold dry/damp window
- PASS — hard used in hot window
- PASS — consumption drift represented
- PASS — telemetry dropout survived without NaN
- PASS — slow zone represented
- PASS — safety car represented
- PASS — incident forced unscheduled pit
- PASS — replanning version changed many times
- PASS — timed race lap projection remained finite
- PASS — race completed after 24h threshold
- PASS — medium driver slower than 2026 Ferrari best
- PASS — medium driver clean-best under 3:40
- PASS — pit count endurance scale
- PASS — final dry tyre stock nonnegative
- PASS — tyre extrapolation uncertainty increases with distance
- PASS — less than 3 pace points cannot fit quadratic robustly
- PASS — road-condition model separation key exists
- PASS — compound is persisted per lap
- PASS — fuel/VE can remain usable across dry compound changes
- PASS — pit loss includes lane transit
- PASS — extra wet stop cost includes four-tyre service
- PASS — drying track eventually returned to slick
- PASS — recommendation follows game-side optimal signal at pit opportunity

## Backend conclusions
1. Arbitrary-length stint planning works: a 24h plan is a list of stints/pits, not a two-stint special case.
2. Timed-race laps remaining must keep moving with observed race pace and neutralization.
3. Dry tyre allocation is a race-level resource constraint; tyre decisions cannot be optimized pit-by-pit in isolation.
4. Auto compound should consume LMU's game-side optimal-compound recommendation; driver history refines wear/pace economics but does not require the user to train every compound first.
5. No-personal-wet fallback can use normalized same-class field pace until enough personal wet laps are collected.
6. Recalculation is useful as a manual force-refresh, but the strategy engine should already surface material changes automatically. No long-press accept state is required.