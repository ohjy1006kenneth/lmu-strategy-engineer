# Simulation Results

The current browser prototype has been stress-tested with synthetic races. These are design/algorithm validation fixtures, not claims about exact LMU physics.

## 24-hour Le Mans Hypercar stress test

Purpose:
- prove the original two-stint solver was insufficient,
- test arbitrary-length planning,
- exercise weather, neutralization and long-race tyre allocation.

Key finding:
- the original model incorrectly represented "first stint + entire remaining race."
- this directly motivated the arbitrary-length RacePlan architecture.

A later medium-driver endurance simulation used:
- deliberately slower-than-professional pace,
- lap-time variation,
- multi-class traffic,
- weather transitions,
- consumption drift,
- incident,
- telemetry loss,
- neutralization,
- dry-tyre allocation pressure.

The resulting architecture requirement is now permanent:
- every stint and pit must be a first-class object,
- dry tyre allocation is tracked across the whole race,
- rolling timed-race distance must be recalculated.

## 1h45 Le Mans LMGT3 stress test

A synthetic average-sim-racer profile was used, with approximately 4:12 clean baseline pace and several seconds of normal lap variation.

The projection produced roughly:
- 25 predicted race laps,
- +1 buffer lap,
- 26-lap strategy distance,
- capacity-limited stint length around 12 laps in the fixture.

The test exercised:
- traffic,
- off-track lap rejection,
- aggressive consumption,
- Slow Zone,
- short drizzle,
- telemetry dropout,
- spin/tyre scrub,
- late shower.

Representative backend reactions:
- sustained Fuel/VE drift moved the pit earlier,
- Slow Zone was excluded from pace learning,
- a short drizzle did not justify Wets because crossover gain could not repay pit/service cost,
- telemetry loss froze the last valid strategy,
- a spin lap was rejected from clean learning,
- a late shower with too little race remaining did not trigger an unnecessary Wet stop.

## Important limitation

The synthetic Fuel/lap, VE/lap, tyre wear, tank sizes and some service constants used in these tests are fixtures.

Production behavior must replace them with:
- verified current LMU values,
- personal driver models,
- or explicitly lower-confidence fallbacks.

## Future simulation suite

The production repository should convert these ideas into deterministic automated scenarios rather than relying on browser-prototype-only testing.
