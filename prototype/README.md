# Prototype Reference

The interactive browser prototype was used to validate product behavior during product design. It is intentionally **not** the production implementation.

This folder documents the latest prototype behavior so implementation agents do not need access to the original chat artifact.

## Pre-race

### Race information
Show:
- track/layout,
- car,
- race length,
- estimated laps,
- dry tyre allocation,
- available compounds,
- forecast.

### Strategy data
Sources:
- Qualifying,
- Recent Clean Laps.

When Qualifying is selected, do not show a lap-count stepper.

When Recent Clean Laps is selected:
- show minus / N / plus,
- cap N to the number of matching recorded clean laps,
- disable plus at the cap.

Also expose:
- Buffer Laps,
- Start Compound with Auto shown simply as Auto.

### Driver model
Show only:
- average Fuel/lap,
- average VE/lap,
- average tyre wear/lap across all four tyres.

### Strategy selection
User-facing options:
- Flat Out,
- Fuel Save only when a distinct feasible plan exists.

### Strategy visualization
The Strategy timeline:
- supports Tyre Life and Pace Loss modes,
- shows translucent compound bands by stint,
- supports zoom in/out,
- supports horizontal pan/scroll,
- shows pit markers,
- treats tyre resets correctly after tyre changes.

Each stint can reveal:
- lap range,
- start fuel,
- start VE,
- Fuel Ratio,
- target Fuel/lap,
- target VE/lap,
- compound,
- predicted tyre end state,
- pit Fuel/VE targets,
- tyre action,
- next compound.

### Strategy editing
The user can modify:
- pit lap,
- Fuel Ratio,
- tyre action,
- next compound.

Every edit recalculates predicted total race time and shows gain/loss versus the recommendation.

Provide Back to Recommended.

## Compact overlay

For Fuel, VE and average tyre wear:

- target is large,
- actual is small,
- delta uses green/red,
- do not write Ahead/Behind.

## Expanded overlay

Top section always shows the current plan:

~~~
NEXT PIT STOP                        CURRENT
Lap 12
+41 L · VE 71% · FL + FR
~~~

If the live optimizer finds a materially better plan, show:

~~~
UPDATED STRATEGY                 FASTER PLAN
short reason

Lap 11
...

[ Keep Current ]    [ Replace ]
   Tap input         Hold input
~~~

There is no Recalculate button.

## Input behavior

One configurable input:
- tap compact -> expand,
- tap expanded with no update -> compact,
- tap with update -> Keep Current,
- long press with update -> Replace.

Visible buttons perform the same actions.

## Important

Do not reproduce prototype mock constants as production facts. The browser prototype used synthetic values for UX and algorithm stress testing.
