# Testing

## Simulation architecture

Use **one reusable deterministic race simulator**. Do not write a separate simulator script for every race length or edge case.

A simulation is composed from reusable configuration:

~~~text
Simulation Engine
    +
Track Profile
    +
Car Profile
    +
Driver Profile
    +
Timed Race Definition
    +
Weather Timeline
    +
Event Timeline
    =
Deterministic Race
~~~

LMU V1 is treated as **timed-race only**. Current product research has not verified a user-configurable fixed-lap race format in LMU, so do not maintain lap-count-race scenarios unless that capability is verified later.

A scenario describes **what happens in the race**. The strategy engine decides **what to do about it**.

Do not put expected strategy actions into the simulation physics/configuration itself. Expected decisions belong in test assertions.

## Canonical scenario count

Maintain **17 canonical scenarios**.

Do not create one handwritten scenario for every individual edge case. Reuse driver profiles, weather modules, event modules, failures and assertions.

### Sprint

1. `Sprint_Dry_NoStop`
   - short timed race,
   - Flat Out only,
   - start Fuel/VE optimization,
   - no pit stop,
   - no tyre service.

2. `Sprint_BufferChangesStartLoad`
   - no-stop sprint,
   - final-lap buffer materially changes required starting energy,
   - verifies buffer affects strategy coverage without changing official race duration.

### Medium races

3. `Medium_OneStop_FlatOut`
   - ordinary one-stop race,
   - normal consumption,
   - validates pit timing and service targets.

4. `Medium_FuelSave_RemovesStop`
   - Flat Out requires an extra stop/splash,
   - Fuel Save eliminates that stop,
   - validates saving-time penalty versus pit time avoided.

5. `Medium_SplashStop`
   - a final short refill is unavoidable,
   - validates partial final Fuel/VE target and pit-duration economics.

6. `Medium_ConsumptionDrift`
   - Fuel and/or VE usage rises during the race,
   - validates rolling model update and changed pit timing.

7. `Medium_TyreDecision`
   - compares no tyre service, fronts only and all four while already stopping,
   - validates marginal tyre-service economics.

### Endurance

8. `Endurance_Dry_MultiStop`
   - long timed race,
   - arbitrary-length stint plan,
   - many stops,
   - fuel-mass and tyre-state evolution.

9. `Endurance_TyreAllocationPressure`
   - dry tyre allocation becomes binding,
   - Wet tyres remain outside the dry allocation ledger,
   - validates whole-race inventory accounting.

10. `Endurance_CompoundTransitions`
    - changing environmental conditions,
    - LMU Auto compound recommendation can change,
    - validates Soft/Medium/Hard are not treated as universal pace ranking.

### Weather

11. `Weather_BriefShower_StaySlick`
    - rain appears,
    - Wet crossover never repays pit/service cost,
    - strategy stays on slicks.

12. `Weather_EarlyRain_NoWetHistory`
    - no personal Wet model,
    - normalized **same-car** field fallback is available,
    - validates relative pace normalization.

13. `Weather_FieldFallbackSameClass`
    - no usable same-car Wet samples,
    - normalized **same-class** fallback is used,
    - fastest opponent absolute pace must not be copied directly.

14. `Weather_DryWetDry`
    - Slick -> Wet -> Slick,
    - validates both crossovers,
    - includes hysteresis against repeated recommendation flicker.

15. `Weather_FieldToPersonalWetModel`
    - race begins with field fallback,
    - valid personal Wet laps accumulate,
    - strategy transitions toward the personal Wet model.

### Race control / failures

16. `RaceControl_Mixed`
    - yellow,
    - Slow Zone,
    - Safety Car,
    - validates pace-learning exclusions and timed-race lap projection changes.

17. `Failure_Recovery`
    - missed pit,
    - unscheduled stop,
    - telemetry dropout/reconnect,
    - pit-estimate unavailable,
    - tyre-inventory unavailable,
    - session transition and/or schema/build capability change.

## Driver profiles

Maintain reusable deterministic driver archetypes instead of inventing pace behavior inside each scenario.

Initial profiles:

- `Fast`
- `Average`
- `Casual`

The exact calibration will evolve, but the same profile name must mean the same behavior across scenarios.

Normal CI should run the 17 canonical scenarios primarily with `Average`.

Extended/nightly tests can run:
- Fast variants,
- Casual variants,
- multiple random seeds,
- parameter sweeps.

Do not run every possible combination on every commit.

## Pace variation model

Do not model driver inconsistency as one arbitrary random lap-time offset.

Conceptually:

~~~text
lap time =
    baseline pace
  + fuel-mass effect
  + tyre effect
  + road/weather effect
  + traffic
  + normal driver noise
  + discrete mistake
  + race-control effect
~~~

Simulations should include:
- normal lap-to-lap noise,
- traffic,
- mistakes,
- off-tracks,
- spins,
- cold tyres,
- fuel-mass effects.

A realistic Average sim racer should fluctuate materially more than a professional driver.

Discrete mistakes must remain distinguishable from ordinary pace noise so a spin does not teach the personal pace model that the driver suddenly became 20 seconds slower.

## Reusable event modules

Events should be attachable to any compatible scenario.

Examples:
- `TrafficEvent`
- `OffTrackEvent`
- `SpinEvent`
- `FuelDriftEvent`
- `VeDriftEvent`
- `YellowEvent`
- `SlowZoneEvent`
- `SafetyCarEvent`
- `TelemetryDropoutEvent`
- `MissedPitEvent`
- `PitEstimateUnavailableEvent`
- `TyreInventoryUnavailableEvent`
- `SessionTransitionEvent`
- `SchemaCapabilityChangeEvent`

Do not create separate Sprint/Medium/Endurance versions of the same failure unless race length changes the actual strategy behavior under test.

## Fuel / VE coverage

Across the canonical suite and parameterized variants, test:
- Fuel drift,
- VE drift,
- tank-capacity limits,
- VE limits,
- final-lap buffer,
- splash stop,
- save-to-eliminate-stop,
- partial final refill,
- start Fuel/VE optimization.

## Tyre coverage

Across the suite, test:
- no change,
- fronts only,
- all four,
- compound change,
- dry allocation exhaustion,
- Wet exemption from dry allocation,
- cold outlap,
- degradation outside observed personal coverage.

## Weather coverage

Across the suite, test:
- forecast correct,
- rain early,
- rain late,
- false shower,
- dry -> wet,
- wet -> dry,
- repeated crossover,
- no personal Wet data,
- normalized same-car field fallback,
- same-class fallback when same-car samples are unavailable,
- fastest-opponent absolute pace is **not** copied directly,
- insufficient/dirty field samples disable or lower confidence of fallback,
- field fallback transitions to personal Wet model,
- personal Wet model becoming available mid-race,
- hysteresis prevents Slick/Wet flicker around crossover.

Not every bullet needs its own canonical scenario. Use parameterized variants and assertions within the closest canonical scenario.

## Race control / failure coverage

Across the suite, test:
- yellow,
- Slow Zone,
- Safety Car,
- missed pit,
- unscheduled stop,
- telemetry dropout/reconnect,
- pit-estimate unavailable,
- tyre-inventory unavailable,
- session transition,
- schema/build change.

## Simulation result model

Every run should produce the same structured result shape:

~~~text
SimulationResult
- RaceSummary
- LapLog[]
- StrategyTimeline[]
- StrategyChanges[]
- PitStops[]
- ModelUpdates[]
- Warnings[]
- InvariantResults[]
~~~

A strategy change should record structured data, not only prose:

~~~text
StrategyChange
- timestamp / race time
- lap
- previous plan
- new plan
- trigger
- observed values
- predicted gain seconds
- surfaced to driver
~~~

## Result-storage policy

Simulation output lives under the repository root:

~~~text
simulation-results/
  Sprint_Dry_NoStop/
    report.md
    summary.json
    lap_log.csv
  Sprint_BufferChangesStartLoad/
    ...
  ...
  Failure_Recovery/
    report.md
    summary.json
    lap_log.csv
~~~

**Do not create timestamped result folders/files in normal development.**

Each new run of a canonical scenario overwrites that scenario's:
- `report.md`
- `summary.json`
- `lap_log.csv`

This keeps at most one current result set per canonical scenario in the repository.

If historical comparison is needed, use Git history rather than accumulating local result files.

Stress/Monte-Carlo runs should normally write to temporary/build output and should not be committed unless a specific failure is being preserved for debugging.

## Invariants

Every applicable simulation automatically asserts that the solver never:
- produces negative fuel,
- produces VE below zero,
- exceeds verified fuel capacity,
- uses unavailable dry tyres,
- counts Wet tyres against dry allocation,
- produces a stint beyond its energy constraints,
- shows an unsupported candidate as high-confidence.

These are assertions, not separate scenarios.

## UI behavior

UI behavior belongs primarily in component/UI tests rather than full race simulations.

Verify:
- Recent Clean Laps caps correctly,
- Qualifying hides the recent-lap stepper,
- graph zoom/pan works,
- edited strategy time delta updates,
- Back to Recommended restores optimizer plan,
- overlay update appears only for material changes,
- Keep Current dismisses that update,
- Replace makes the updated plan current,
- no Recalculate button exists.

## Test tiers

### Per-commit regression
- all 17 canonical scenarios using the Average driver unless a scenario specifies otherwise,
- unit tests,
- invariant checks,
- UI/component tests.

### Extended/nightly
- Fast and Casual profile variants,
- multiple deterministic seeds,
- broader weather/event parameter sweeps,
- randomized stress testing.

### Calibration
Once real LMU recordings exist:
- compare simulated model behavior against actual sessions,
- tune fixture priors,
- validate learning/model assumptions.

Randomized stress testing is for discovering failures; canonical deterministic scenarios are for preventing regressions.
