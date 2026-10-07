# Testing

## Philosophy

Strategy changes must be validated with deterministic simulation before being considered production-safe.

Tests should cover:
- sprint races,
- medium races,
- endurance races,
- timed races,
- lap-count races if supported.

Do not test only perfectly repeatable drivers.

## Pace variation

Simulations should include:
- normal lap-to-lap noise,
- traffic,
- mistakes,
- off-tracks,
- spins,
- cold tyres,
- fuel-mass effects.

A realistic average sim racer should fluctuate materially more than a professional driver.

## Fuel / VE

Test:
- Fuel drift,
- VE drift,
- tank-capacity limits,
- VE limits,
- final-lap buffer,
- splash stop,
- save-to-eliminate-stop,
- partial final refill,
- start Fuel/VE optimization.

## Tyres

Test:
- no change,
- fronts only,
- all four,
- compound change,
- dry allocation exhaustion,
- Wet exemption from dry allocation,
- cold outlap,
- degradation outside observed personal coverage.

## Weather

Test:
- forecast correct,
- rain early,
- rain late,
- false shower,
- dry -> wet,
- wet -> dry,
- repeated crossover,
- no personal wet data,
- normalized same-car field fallback,
- same-class fallback when same-car samples are unavailable,
- fastest-opponent absolute pace is **not** copied directly,
- insufficient/dirty field samples disable or lower confidence of the fallback,
- field fallback transitions to personal Wet model after enough valid personal Wet laps,
- personal wet model becoming available mid-race,
- hysteresis prevents repeated Slick/Wet strategy flicker around crossover.

## Race control / failures

Test:
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

## Invariants

The solver must never:
- produce negative fuel,
- produce VE below zero,
- exceed verified fuel capacity,
- use unavailable dry tyres,
- count Wet tyres against dry allocation,
- produce a stint beyond its energy constraints,
- show an unsupported candidate as high-confidence.

## UI behavior

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

## Recommended test architecture

Keep deterministic scenario fixtures separate from production telemetry.

Each scenario should define:
- starting session state,
- lap stream,
- environmental changes,
- expected optimizer decisions.

Assertions should target behavior, not only exact floating-point values.
