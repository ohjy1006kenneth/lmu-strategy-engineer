# Backend / Strategy Engine Specification

This document is the source of truth for **strategy generation, equations, model functions and backend decision flow**.

It defines the intended production behavior. Prototype equations and synthetic constants are not authoritative.

## 1. Design goals

The backend must:

- generate arbitrary-length race plans,
- support timed LMU races,
- produce a Flat Out candidate,
- optionally produce a distinct Fuel Save candidate,
- optimize Fuel, VE, Fuel Ratio, pit timing, tyres and compound together,
- replan the remaining race from live state,
- evaluate user-edited strategies with the same scoring model,
- never hide infeasible states behind invented constants,
- attach confidence/provenance to predictions that depend on incomplete data.

The backend is a pure C# library. It does not read LMU directly and does not depend on WPF.

## 2. Units and notation

Use explicit units in field/type names wherever possible.

~~~text
t              time, seconds
L              physical fuel, litres
VE             Virtual Energy / NRG, percent unless adapter provides a better normalized unit
r_fuel         fuel consumption, L/lap
r_ve           VE consumption, %/lap
N              predicted race laps
B              buffer laps
n              stint length, laps
C_fuel         verified physical tank capacity, L
C_ve           verified VE capacity, normally represented as 100%
R              Fuel Ratio
w_k            tyre wear rate for corner k, percentage points/lap
q_k            remaining tyre life for corner k, %
s              fuel-saving intensity, 0 = normal consumption
Delta t        predicted time difference, seconds
~~~

Do not mix percentages, percentage points, litres and normalized fractions without explicit conversion.

## 3. Core backend types

### StrategyInput

Conceptually:

~~~text
StrategyInput
- SessionConstraints
- RaceState
- VehicleState
- Forecast
- DriverModels
- PitServiceModel
- LmuCapabilities
- UserPreferences
- LoadedPlan?
- UserEditedPlan?
~~~

### StrategyResult

~~~text
StrategyResult
- FlatOut
- FuelSave?              // null when no distinct feasible save strategy exists
- RecommendedPlan
- PredictedRaceLaps
- StrategyDistanceLaps   // race projection + buffer
- ModelConfidence
- Warnings[]
- MaterialChange?
~~~

### RacePlan

~~~text
RacePlan
- StrategyMode
- Stints[]
- Pits[]
- PredictedDrivingTime_s
- PredictedPitTime_s
- PredictedTotalTime_s
- DryTyresRemaining
- FinishFuel_l
- FinishVE_pct
- Confidence
- Provenance
~~~

### Stint

~~~text
Stint
- Index
- StartLap
- EndLap
- LapCount
- Compound
- StartFuel_l
- StartVE_pct
- FuelRatio
- TargetFuelPerLap_l
- TargetVEPerLap_pct
- SavingIntensity
- TyreStart[FL,FR,RL,RR]
- TyreEnd[FL,FR,RL,RR]
- PredictedDrivingTime_s
~~~

### Pit

~~~text
Pit
- AfterStint
- PitLap
- TargetFuel_l
- TargetVE_pct
- FuelRatio
- TyreAction
- NextCompound
- LaneLoss_s
- ServiceTime_s
- TotalPitCost_s
- DryTyresConsumed
~~~

## 4. Data selection and robust driver averages

The model-selection key must respect road conditions.

### Fuel / VE selection

Preferred grouping:

~~~text
driver + car + track/layout + road-condition family + relevant environment
~~~

Dry compound changes should not automatically partition Fuel/VE data unless measured evidence shows that they materially affect consumption.

### Tyre selection

At minimum:

~~~text
driver + car + track/layout + road-condition family + compound
~~~

### Clean-lap filter

Reject from normal model fitting:

~~~text
incomplete
pit in/out
formation
telemetry incomplete
severe crash/spin
~~~

Exclude from normal pace fitting:

~~~text
yellow
Slow Zone
Safety Car
~~~

Traffic may remain useful for consumption while being downweighted or corrected for pace.

### Robust average

For a selected set of valid laps, use a robust estimator rather than an unconditional arithmetic mean.

A reasonable initial implementation is:

~~~text
x_med = median(x_i)

MAD = median(|x_i - x_med|)

keep lap i when:
    |x_i - x_med| <= k * 1.4826 * MAD
~~~

Then compute a weighted mean over retained samples.

If MAD collapses to zero or sample count is very small, fall back to a simple mean without inventing precision.

The exact outlier multiplier `k` is a tunable implementation parameter and belongs in configuration/tests.

## 5. Timed-race lap projection

LMU V1 is timed-race focused.

The race-distance estimate must be rolling, not fixed forever at pre-race load.

### First estimate

Let:

~~~text
T_remaining = remaining race time
t_lap       = robust predicted representative lap time
t_pit       = expected future pit time inside the race clock
~~~

A first estimate can be:

~~~text
N0 = floor((T_remaining - t_pit) / t_lap)
~~~

Depending on LMU finish-line rules, the car may begin/complete one additional lap after the timer expires. That exact boundary must be verified against LMU behavior and encoded in one function rather than scattered throughout the solver.

Therefore production code should expose:

~~~csharp
int ProjectRaceLaps(RaceProjectionInput input);
~~~

instead of hard-coding a `+1` in multiple places.

### Iteration

Pit count depends on N, while N depends on pit time.

Use a short fixed-point iteration:

~~~text
N := initial estimate

repeat until stable or iteration limit:
    generate provisional energy-feasible plan for N
    estimate total future pit time
    recompute N from remaining time and predicted pace
~~~

The iteration must terminate deterministically.

### Strategy distance

~~~text
N_strategy = N_race + BufferLaps
~~~

Buffer laps change required strategy coverage, not the official race duration shown to the user.

## 6. Consumption model

For Flat Out, derive normal target rates from the selected personal model:

~~~text
r_fuel_flat = robust modeled Fuel/lap
r_ve_flat   = robust modeled VE/lap
~~~

Live prediction may condition these on:

- current road condition,
- track/air temperature,
- recent clean-lap trend,
- Fuel Ratio / engine mode if verified,
- neutralization state,
- other measured factors proven useful.

Do not contaminate the long-term baseline with invalid or neutralized laps.

## 7. Stint feasibility

A candidate stint of `n` laps is feasible only if both Fuel and VE constraints are satisfied with required reserve.

Conceptually:

~~~text
FuelRequired(n) =
    sum predicted Fuel consumption over n laps
  + fuel reserve

VERequired(n) =
    sum predicted VE consumption over n laps
  + VE reserve
~~~

Require:

~~~text
FuelRequired(n) <= C_fuel
VERequired(n)   <= C_ve
~~~

Do not calculate a single universal "max stint" once and assume conditions never change. Weather, saving mode, Fuel Ratio and live consumption can change feasible stint length.

A helper may still compute an approximate upper bound:

~~~text
n_fuel_max = floor((C_fuel - reserve_fuel) / r_fuel)
n_ve_max   = floor((C_ve   - reserve_ve)   / r_ve)

n_max_approx = min(n_fuel_max, n_ve_max)
~~~

but final feasibility must use the actual stint prediction.

## 8. Start Fuel and VE optimization

Neither of these rules is valid universally:

~~~text
Start VE = 100%
Start VE = minimum required for first stint
~~~

Race-start Fuel and VE are decision variables.

For each candidate first stint, evaluate feasible start-load candidates using the same total-race-time score.

The optimizer should account for:

~~~text
benefit of carrying more before the race:
    less Fuel/VE service later

cost of carrying more physical fuel:
    increased lap time from mass
~~~

The physical fuel-mass penalty must come from a verified/personal model when available.

If that relationship is unavailable, do not fabricate a precise optimum. Use a conservative documented fallback and lower confidence.

Function boundary:

~~~csharp
StartLoadResult OptimizeStartLoad(
    Stint firstStint,
    StrategyContext context);
~~~

## 9. Fuel Ratio

Fuel Ratio must be modeled as a first-class strategy variable, not a UI-only number.

The exact relationship between:

- VE target,
- Fuel Ratio,
- physical fuel carried,
- pit-service time,

must be obtained from verified LMU behavior.

Therefore the core should call an abstraction:

~~~csharp
EnergyLoad ResolveEnergyLoad(
    double targetVePct,
    double fuelRatio,
    VehicleEnergyModel model);
~~~

Do not embed an unverified LMU Fuel Ratio formula in the strategy engine.

## 10. Flat Out candidate generation

Flat Out uses normal modeled consumption.

The generator must search arbitrary-length stint sequences.

Recommended initial approach:

1. determine `N_strategy`,
2. determine legal compounds and dry tyre inventory,
3. enumerate/construct feasible stint lengths,
4. generate pit actions between stints,
5. optimize start load and refill targets,
6. evaluate tyre actions,
7. score complete plans,
8. retain the lowest predicted total-time feasible plan.

A simple dynamic-programming or bounded best-first/beam search is preferable to deeply nested special cases.

State can include:

~~~text
lap index
current compound
tyre state
dry tyre inventory
current Fuel/VE state
number of stops
~~~

Transitions include:

~~~text
continue stint
pit + no tyres
pit + fronts
pit + all four
change compound when legal/appropriate
~~~

Do not create logic that assumes exactly one or two stops.

Function boundary:

~~~csharp
RacePlan GenerateFlatOut(StrategyInput input);
~~~

## 11. Fuel Save candidate generation

Fuel Save is shown only when it is a **distinct feasible strategy**.

Internally generate several save layouts, for example:

- extend first stint,
- extend one later stint,
- distribute saving evenly,
- concentrate saving where traffic/neutralization makes it cheaper,
- remove a splash,
- remove a complete stop.

### Required save level

For a candidate stint of `n` laps:

~~~text
r_fuel_required <= usable_fuel_capacity / n
r_ve_required   <= usable_ve_capacity / n
~~~

Relative saving requirements:

~~~text
s_fuel = max(0, 1 - r_fuel_required / r_fuel_flat)
s_ve   = max(0, 1 - r_ve_required   / r_ve_flat)

s_required = max(s_fuel, s_ve)
~~~

This is an initial abstraction; production must respect the verified Fuel Ratio / physical-energy relationship rather than assuming Fuel and VE scale identically.

### Saving time penalty

Desired personal model:

~~~text
Delta t_save_per_lap = g(s, context)
~~~

A simple initial fit may be:

~~~text
g(s) = a*s + b*s^2
~~~

where coefficients are learned from corrected personal data.

Total saving cost:

~~~text
SavingPenalty =
    sum over laps/stints of g(s_j, context_j)
~~~

Before personal coverage is adequate, a documented empirical prior may be used with lower confidence.

### Net value

~~~text
NetFuelSaveBenefit =
    PitLaneTimeAvoided
  + ServiceTimeAvoided
  - SavingPenalty
  - AdditionalTyreCost
  - OtherConsequences
~~~

The Fuel Save candidate can be displayed even if slightly slower when product requirements choose to expose an alternative, but the UI must clearly show its predicted delta. The recommended strategy remains the lowest expected race-time candidate subject to confidence/risk rules.

Function boundary:

~~~csharp
RacePlan? GenerateFuelSave(
    StrategyInput input,
    RacePlan flatOut);
~~~

Return null when no distinct feasible Fuel Save plan exists.

## 12. Pit service model

Do not hard-code community anecdotes as authoritative pit timing.

Use an abstraction:

~~~csharp
PitEstimate EstimatePit(PitRequest request);
~~~

Preferred provenance:

1. verified current LMU pit estimate,
2. app-maintained model validated against the current build,
3. explicit low-confidence fallback.

For an extra stop:

~~~text
PitCost =
    PitLaneLoss
  + StationaryService
  + ColdOutlapEffects
~~~

If tyres are changed during an already-required Fuel/VE stop:

~~~text
IncrementalTyreCost =
    additional stationary tyre-service effect
  + cold/outlap effect
~~~

Do not charge pit-lane loss twice.

## 13. Tyre wear model

Keep FL/FR/RL/RR separately.

For corner `k`:

~~~text
q_k(end) =
    max(0,
        q_k(start)
      - w_k * n)
~~~

This simple projection is allowed with minimal personal data.

UI may display average tyre wear:

~~~text
w_avg = (w_FL + w_FR + w_RL + w_RR) / 4
~~~

but backend decisions use per-corner values.

## 14. Tyre pace-loss model

Wear and pace loss are separate models.

For personal degradation:

~~~text
x = (100 - q) / 100

Delta t_tyre(q) =
    a*x + b*x^2
~~~

For four tyres, initial implementation may use the limiting tyre or a fitted aggregate model; choose based on validation rather than an arbitrary average.

Cumulative tyre cost over a stint:

~~~text
TyreLoss =
    sum over laps of predicted tyre-related pace loss
~~~

Extrapolation beyond personal coverage is permitted only with increasing uncertainty.

Function boundaries:

~~~csharp
TyreProjection PredictTyres(StintInput input);
double PredictTyrePaceLoss(TyreState state, TyreModel model);
~~~

## 15. Tyre-change decision

At a pit opportunity compare legal actions:

~~~text
No tyres
Fronts only
All four
Other actions only if LMU actually supports them
~~~

For each action:

~~~text
TotalTyreDecisionCost =
    tyre service contribution
  + future tyre pace loss
  + cold/outlap effect
  + extra pit-lane cost if this action creates an additional stop
  + inventory opportunity cost
~~~

Choose the action that minimizes the complete remaining-race score.

Dry tyre inventory is a whole-race resource.

Wet tyres never decrement dry tyre allocation.

## 16. Compound selection

Do not encode:

~~~text
Soft < Medium < Hard lap time
~~~

as a universal truth.

For Auto:

1. read legal/available compounds from verified LMU data,
2. use verified LMU compound-condition recommendation metadata when available,
3. apply personal wear/degradation economics,
4. evaluate complete race time.

Lack of personal data for the LMU-recommended dry compound must not block Fuel/VE strategy generation.

## 17. Weather model

Dry and Wet personal models are separate.

### Source priority for Wet pace

~~~text
1. personal Wet model
2. normalized same-car field fallback
3. normalized same-class field fallback
4. unavailable / low-confidence Wet prediction
~~~

### Field normalization

For opponent `i` with comparable valid Dry and Wet samples:

~~~text
wet_factor_i =
    representative_wet_pace_i
  / representative_dry_pace_i
~~~

Combine robustly:

~~~text
field_wet_factor =
    robust_median(wet_factor_i)
~~~

Temporary user Wet prediction:

~~~text
predicted_user_wet_pace =
    user_dry_baseline
  * field_wet_factor
~~~

Never do:

~~~text
predicted_user_wet_pace = fastest_opponent_wet_lap
~~~

Filter opponent samples for:

- pit laps,
- yellow/Slow Zone/Safety Car,
- incidents,
- unstable/outlier samples,
- incompatible class/car,
- mismatched road-wetness region where possible.

If tyre compound is not reliably observable, do not pretend every rainy opponent lap is a verified Wet-tyre sample.

### Transition to personal Wet model

As valid personal Wet laps accumulate, blend away from the field fallback.

Conceptually:

~~~text
WetPrediction =
    alpha * PersonalWet
  + (1 - alpha) * FieldFallback
~~~

with `alpha` increasing with personal sample coverage/confidence.

Do not hard-code the confidence schedule until validated.

### Slick/Wet crossover

Compare the future horizon:

~~~text
CostStaySlick =
    sum predicted slick lap times

CostSwitchWet =
    pit/service cost
  + sum predicted Wet lap times
~~~

Switch when:

~~~text
CostSwitchWet + hysteresis_margin < CostStaySlick
~~~

If already stopping, do not charge pit-lane loss again.

Use hysteresis/minimum hold logic to prevent repeated Slick/Wet oscillation near crossover.

## 18. Pace model and total plan score

Predicted lap time should be decomposable:

~~~text
PredictedLapTime =
    BaselinePace
  + FuelMassEffect
  + SavingPenalty
  + TyrePaceLoss
  + RoadWeatherEffect
  + ColdTyreEffect
  + other modeled effects
~~~

Traffic, incidents and race-control events are stochastic/live context and should not be baked into the driver's clean baseline.

For a complete plan:

~~~text
DrivingTime =
    sum predicted lap times

PitTime =
    sum pit costs

TotalPlanTime =
    DrivingTime + PitTime
~~~

Candidate selection minimizes expected `TotalPlanTime`, subject to feasibility and confidence/risk constraints.

## 19. Confidence and provenance

Every derived model should expose:

~~~text
source
sample count
condition coverage
extrapolation distance
confidence
~~~

Examples of provenance:

~~~text
PersonalMeasured
PersonalFitted
SameCarFieldFallback
SameClassFieldFallback
VerifiedLmu
ValidatedBuildFallback
SyntheticSimulationFixture
Unavailable
~~~

Unsupported data must not be presented as high-confidence.

Confidence can influence:

- whether Fuel Save is shown,
- whether a Wet crossover is actionable,
- whether an Updated Strategy is surfaced,
- tie-breaking between nearly equal plans.

## 20. User-edited strategy evaluation

The strategy editor must use the **same evaluator** as recommended strategies.

Do not maintain separate UI math.

Function:

~~~csharp
PlanEvaluation EvaluatePlan(
    StrategyInput input,
    RacePlan plan);
~~~

Validate:

- pit laps are ordered and legal,
- no stint is energy-infeasible,
- no capacity exceeded,
- dry tyre inventory valid,
- compounds legal,
- Fuel/VE nonnegative,
- finish buffer satisfied.

Return:

~~~text
Valid
Violations[]
PredictedTotalTime_s
DeltaVsRecommended_s
Warnings[]
Confidence
~~~

## 21. Live replanning

On each meaningful telemetry/model update:

~~~text
1. build current RaceState
2. discard completed portion of loaded plan
3. update rolling race-distance projection
4. update short-term consumption/pace estimates
5. solve remaining Flat Out
6. solve optional Fuel Save
7. compare best new plan against loaded/current plan
8. classify whether change is material
9. surface Updated Strategy only when material
~~~

Function boundary:

~~~csharp
ReplanResult ReplanRemaining(
    StrategyInput currentInput,
    RacePlan loadedPlan);
~~~

## 22. Fast adaptation versus long-term learning

Keep two concepts separate.

### Long-term personal model

Built from historical valid laps and changes slowly.

### Current-race adaptation

Use a short clean-lap window to detect:

- Fuel drift,
- VE drift,
- pace change,
- tyre wear deviation.

An initial implementation may use the latest ~3 valid current-race laps for fast deviation detection, but the exact window belongs in configuration/tests.

Do not permanently overwrite the long-term model from one anomalous lap.

## 23. Material-change classifier

The engine may re-solve often. The overlay should not notify on every small numerical change.

A material change includes cases such as:

- different next pit lap,
- different stop count,
- tyre action changed,
- compound changed,
- Slick/Wet crossover,
- meaningful Fuel/VE target change,
- meaningful predicted race-lap change,
- incident/missed-pit requiring new immediate action.

Function:

~~~csharp
MaterialChange ComparePlans(
    RacePlan current,
    RacePlan candidate,
    MaterialChangePolicy policy);
~~~

Exact numerical thresholds are configuration/product-validation items, not hidden constants.

## 24. Recommended backend services

Suggested pure interfaces/classes:

~~~text
IRaceProjector
IDriverModelProvider
IConsumptionModel
IFuelMassModel
IFuelSaveModel
ITyreWearModel
ITyrePaceModel
IWeatherPaceModel
IPitServiceModel
ICompoundSelector
IPlanEvaluator
IFlatOutGenerator
IFuelSaveGenerator
IStrategyOptimizer
IMaterialChangeClassifier
~~~

Suggested top-level API:

~~~csharp
public interface IStrategyOptimizer
{
    StrategyResult Solve(StrategyInput input);

    PlanEvaluation EvaluateEditedPlan(
        StrategyInput input,
        RacePlan editedPlan);

    ReplanResult ReplanRemaining(
        StrategyInput input,
        RacePlan loadedPlan);
}
~~~

Keep these deterministic for the same input.

## 25. Failure behavior

When required inputs disappear:

### Telemetry loss

- freeze the last valid live state,
- do not learn from missing data,
- retain the last valid strategy,
- do not manufacture a new strategy from zeros.

### Pit estimate unavailable

- use validated fallback only if one exists,
- lower confidence / warn,
- otherwise avoid pretending precise time deltas are known.

### Tyre inventory unavailable

- do not silently assume unlimited dry tyres,
- retain known cached session-start/current inventory only when provenance is valid,
- otherwise restrict strategy claims.

### Schema/build change

- adapter capability detection must fail closed,
- unknown fields become unavailable,
- strategy engine receives explicit capability state.

## 26. Invariants

Every generated/evaluated plan must satisfy:

~~~text
Fuel >= 0
VE >= 0
Fuel <= verified capacity
VE <= verified capacity
dry tyres used <= available dry inventory
Wet tyres do not decrement dry inventory
all stints energy-feasible
legal compounds only
ordered/non-overlapping stints
finish buffer satisfied when requested
unsupported prediction != high confidence
~~~

These invariants belong in production validation **and** automated simulation tests.

## 27. Implementation sequence

Implement the backend in this order:

1. domain types and units,
2. plan evaluator + invariants,
3. timed-race projector,
4. basic Flat Out generator,
5. pit-service abstraction,
6. Fuel/VE feasibility,
7. dry tyre ledger,
8. tyre wear projection,
9. editable-plan evaluation,
10. tyre service economics,
11. compound selection,
12. Fuel Save generator,
13. weather/Wet fallback,
14. live replanning,
15. confidence/provenance,
16. personal fuel-save and tyre-pace model refinement.

The reusable simulator in `docs/TESTING.md` should exercise each layer as it becomes available.
