# LMGT3 1h45 Le Mans backend stress test

## Calibration
- Car fixture: Ferrari 296 LMGT3 Evo
- Race duration: 1h45m
- Driver profile: average sim racer, deliberately inconsistent
- Synthetic clean-lap baseline: ~4:12, with several seconds of normal lap-to-lap variation
- Real-world reference: 2026 Le Mans LMGT3 race bests were ~3:54–3:55, so this test driver is intentionally much slower than pro pace.
- Fuel / VE / wear values below are synthetic backend fixtures, not claimed LMU constants.

## Pre-race projection
- Median clean lap used by projection: 4:13.98
- Expected pit loss used in projection: 93.5 s
- Projected race laps: 25
- Buffer: +1
- Strategy distance: 26 laps
- Learned average fuel use: 8.23 L/lap
- Learned average VE use: 8.17%/lap
- Learned average tyre wear: 0.560%/lap
- Capacity-limited max stint: 12 laps
- Recommended stint lengths: 12 / 12 / 2
- Race start VE: 100%
- Wet tyres: excluded from dry tyre allocation

## Simulated race
- Logged laps: 25
- Mean valid clean lap: 4:13.89
- Lap-time spread deliberately includes traffic, mistakes and weather.
- Two planned/replanned stops were exercised.

## Backend reactions
- Lap 8: **aggressive consumption** — Replan: first pit moves lap 13 → 12; next full-VE stint shortened to 11 laps
- Lap 11: **Slow Zone** — Slow Zone excluded from pace learning; race-lap projection rechecked, but current lap-12 stop remains best
- Lap 15: **brief drizzle** — No wet stop: predicted wet-tyre gain does not recover pit + 4-tyre service before drizzle clears
- Lap 17: **telemetry dropout** — Freeze live model and keep last valid strategy; no strategy notification
- Lap 19: **small spin / tyre scrub** — Spin lap rejected from clean learning; tyre scrub checked, but 4-tyre service is still slower than staying out
- Lap 21: **projection check** — Rolling pace + pit losses still predicts 25 race laps; 1-lap buffer means plan covers 26
- Lap 22: **late shower** — Late shower: stay on Medium slicks; too few laps remain for Wet crossover to repay stop cost

## Edge-case suite
Passed 42/42 checks.

- PASS — Recent clean-lap selector caps at available count
- PASS — Qualifying source hides recent-lap stepper
- PASS — No clean matching lap blocks strategy
- PASS — Fuel/VE model can use same-condition data across dry compounds
- PASS — Tyre wear model remains compound-specific
- PASS — Auto start compound displays only 'Auto'
- PASS — Auto uses game optimalCompoundConditions internally
- PASS — Start VE is 100%
- PASS — Full-stint-first planner creates 12/12/final splash rather than balanced stints
- PASS — Fuel capacity hard-limits stint length
- PASS — VE capacity hard-limits stint length
- PASS — Wet tyres do not reduce dry tyre allocation
- PASS — Switching back to dry consumes dry tyre allocation
- PASS — Dry tyre inventory never goes negative
- PASS — Custom duplicate pit laps rejected/deduplicated
- PASS — Custom stint longer than max is invalid
- PASS — Custom plan shows time delta vs recommended
- PASS — Fuel ratio is carried on each stint/pit object
- PASS — Per-stint tooltip exposes fuel, VE, ratio, tyre action
- PASS — Graph zoom-in/out bounded
- PASS — Graph horizontal pan works when zoomed
- PASS — Compound band is shown per stint
- PASS — Traffic lap can remain clean but pace variance persists
- PASS — Off-track lap excluded from clean learning
- PASS — Slow Zone excluded from pace learning
- PASS — Consumption drift triggers earlier pit
- PASS — Brief drizzle does not force wet stop
- PASS — Wet switch includes lane loss if off-cycle
- PASS — Wet switch excludes dry tyre allocation
- PASS — Telemetry loss freezes last valid strategy
- PASS — Telemetry resume can continue live model
- PASS — Spin lap rejected from clean model
- PASS — Tyre scrub can trigger service only when time gain exceeds service
- PASS — Late shower with few laps remaining stays on slicks
- PASS — Timed-race projected laps include pit loss
- PASS — Buffer lap changes strategy energy distance, not displayed race duration
- PASS — Final short stint gets partial VE target
- PASS — Overlay has no Recalculate button
- PASS — Tap compact expands
- PASS — Expanded + update: tap keeps current
- PASS — Expanded + update: hold accepts updated strategy
- PASS — No update: tap collapses

## Product conclusions
1. Timed-race lap estimation should use a robust recent/qualifying lap estimate plus predicted pit loss; a one-lap buffer changes the strategy distance, not the displayed race duration.
2. The endurance planner should front-load maximum legal stints, because the race start has no refuelling service-time penalty. This also makes race-start VE = 100% the default.
3. Wet tyres are outside the dry-tyre allocation ledger.
4. Strategy editing should compare the edited plan against the recommended plan in total predicted race time.
5. Every stint is a first-class object carrying fuel, VE, fuel ratio, compound, tyre state and the pit action after it.
6. The overlay remains passive unless the optimizer finds a materially better plan: next pit at top, updated strategy at bottom. No Recalculate button.
7. One input remains: tap expands/collapses; with an updated strategy visible, tap keeps current and long-press accepts the update.