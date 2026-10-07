# Strategy Engine

## Objective

Minimize predicted total race time while satisfying game and user constraints.

Conceptually:

~~~
TotalRaceTime =
    DrivingTime
  + SavingPenalty
  + TyreDegradationLoss
  + PitLaneLoss
  + FuelVEService
  + TyreService
  + ColdTyreEffects
  + OtherKnownServiceCosts
~~~

## RacePlan

A RacePlan contains arbitrary-length stint and pit lists.

### Stint

Every stint should carry at least:

- index,
- start lap,
- end lap,
- lap count,
- compound,
- starting fuel,
- starting VE,
- Fuel Ratio,
- target Fuel/lap,
- target VE/lap,
- saving intensity,
- predicted tyre state at start,
- predicted tyre state at end,
- predicted pace/degradation.

### Pit

Every pit should carry:

- pit lap,
- target fuel,
- target VE,
- Fuel Ratio,
- tyre action,
- next compound,
- pit-lane loss,
- estimated service time,
- incremental cost versus an already-required stop.

## Flat Out

Flat Out means normal modeled driving, not "always start with maximum energy."

Optimize:
- race-start Fuel,
- race-start VE,
- stop count,
- pit laps,
- refill amounts,
- Fuel Ratio,
- tyre changes,
- compounds.

Race-start Fuel/VE are optimization variables.

Do not force:
- start VE = 100%,
- or start VE = minimum required.

The solver must compare any mass/pace effect of carrying more physical fuel against later pit-service time saved.

## Fuel Save

Show Fuel Save only when a distinct feasible saving candidate exists.

Internally evaluate multiple patterns such as:
- extend first stint,
- extend later stint,
- eliminate splash stop,
- eliminate complete stop,
- uneven saving across stints.

The UI should only expose the best Fuel Save result.

### Fuel-save economics

~~~
NetFuelSaveBenefit =
    PitTimeAvoided
  + ServiceTimeAvoided
  - DrivingTimeLostWhileSaving
  - AdditionalTyreCost
~~~

The backend should test required target Fuel/lap and VE/lap for each candidate stint.

### Personal saving-cost model

The desired learned relationship is:

~~~
consumption reduction -> corrected lap-time penalty
~~~

A simple first personal model may be convex:

~~~
DeltaTimeSave = a*s + b*s^2
~~~

where s is saving intensity.

Before adequate personal coverage exists, use a documented empirical prior with lower confidence. Do not call that prior "personal."

## Fuel Ratio

Fuel Ratio affects physical fuel carried relative to energy/NRG strategy. It must be a first-class stint/pit variable rather than a display-only value.

The optimizer should consider how Fuel Ratio changes:
- physical mass,
- refuelling amount,
- pit-service time,
- ability to complete the energy target.

## Tyre wear

The UI shows average wear across all four tyres.

The backend keeps FL/FR/RL/RR separately.

A basic life projection can exist after very little clean data:

~~~
remaining_next =
    remaining_now
  - wear_per_lap * laps
~~~

## Tyre pace loss

Tyre-life projection and tyre-related lap-time loss are different problems.

A personal degradation fit may use:

~~~
x = fraction of tyre life used
DeltaTimeTyre = a*x + b*x^2
~~~

The engine may extrapolate outside observed coverage, but uncertainty must grow with extrapolation distance.

Do not require the user to deliberately drive tyres to an arbitrary 10% or 20% remaining before the model can be useful.

## Tyre-service decision

Compare:

~~~
Keep:
  cumulative predicted tyre-related pace loss

Change:
  tyre service
  + cold/outlap effect
  + pit-lane loss if this creates an extra stop
  + dry-tyre inventory consequence
~~~

If the car is already stopping for Fuel/VE, pit-lane loss is already paid and tyre changes should be evaluated on marginal service cost.

## Dry tyre allocation

Wet tyres do not consume the dry-tyre allocation ledger.

The plan must track dry inventory across the entire race, not independently at each stop.

## Compound selection

Do not model Soft / Medium / Hard as a universal pace ranking.

Auto should begin with:
- compounds actually available in the current LMU event,
- LMU compound-condition recommendation metadata when verified.

Personal history then refines:
- wear,
- degradation,
- stint economics.

The user should not need to drive every car/track/compound combination before Auto can function.

## Weather crossover

Dry and wet models remain separate.

### Personal wet data first

If the user has a usable personal Wet model for the current car/track/road-condition region, use it.

### No personal wet data: normalized field fallback

If rain arrives mid-race and the user has slick data but no usable personal Wet data, the engine may temporarily estimate Wet pace from other cars **without copying the fastest driver's absolute lap time**.

Preferred source order:

1. user's personal Wet model,
2. normalized same-car field data,
3. normalized same-class field data,
4. no Wet prediction if field evidence is insufficient.

The fallback should estimate how much slower the field becomes in Wet conditions relative to each driver's own comparable Dry baseline.

For opponent i:

~~~
wet_factor_i =
    representative_wet_lap_i
  / representative_dry_lap_i
~~~

Use only comparable, valid samples:
- same car when possible, otherwise same class,
- exclude pit in/out laps,
- exclude yellow / Slow Zone / Safety Car laps,
- exclude obvious incidents,
- require a minimum number of stable Wet samples,
- prefer laps close to the current road-wetness / track-condition region.

Combine opponent factors using a robust statistic such as a median or trimmed median:

~~~
field_wet_factor = robust_median(wet_factor_i)
~~~

Then project the user's Wet pace:

~~~
predicted_user_wet_pace =
    user_dry_baseline
  * field_wet_factor
~~~

At the same time, measure the user's **actual current slick pace** as the road gets wetter.

The decision is therefore based on:

~~~
predicted_user_wet_pace
vs
measured_or_predicted_user_slick_pace_at_current_wetness
~~~

Do not use:

~~~
fastest wet driver lap = user's predicted wet lap
~~~

because driver skill differences would bias the estimate.

The field fallback is temporary. As soon as the user completes enough valid personal Wet laps in comparable conditions, transition toward the personal model and stop relying on the field estimate.

### Wet switch economics

A Wet switch is worthwhile when:

~~~
future time saved on Wet tyres
>
wet tyre service
+ additional pit-lane loss if off-cycle
+ cold/outlap effects
~~~

If the car is already stopping for Fuel/VE, the pit-lane loss is already being paid and only the marginal Wet-tyre/service consequence should be charged.

The engine must also handle Wet -> Dry, not only Dry -> Wet, and should use hysteresis so forecast/road-wetness noise does not cause repeated Slick/Wet recommendation flicker.

## User-edited strategy

The user can edit:
- pit lap,
- Fuel Ratio,
- tyre action,
- next compound.

Every edit rebuilds the complete plan and shows predicted total time gained/lost versus the recommendation.

A Back to Recommended action restores the optimizer's plan.
