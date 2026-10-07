# UI / UX Specification

## Visual direction

Dark graphite, restrained accent, motorsport timing hierarchy.

Avoid:
- generic SaaS dashboard styling,
- excessive cards,
- giant sidebars,
- developer explanations in normal driver-facing UI.

## Pre-race header

Show:
- track/layout,
- car,
- race duration/type,
- LMU connection state.

## Race data

Show compactly:
- race length,
- estimated race laps,
- dry tyre allocation / remaining inventory,
- available compounds,
- forecast.

Wet tyres are not part of the dry allocation number.

## Strategy data

Sources:
- Qualifying
- Recent Clean Laps

Qualifying:
- no clean-lap-number modifier.

Recent Clean Laps:
- minus / N / plus,
- plus disabled at actual available count.

Also show:
- Buffer Laps,
- Start Compound with Auto displayed simply as "Auto".

## Driver model

Show only:
- average Fuel/lap,
- average VE/lap,
- average tyre wear/lap across all four tyres.

Do not show Tyre Pace as a separate pre-race card.

Tyre-pace modeling still exists internally.

## Strategy choices

Show at most:
- Flat Out,
- Fuel Save.

Only show Fuel Save when a distinct feasible candidate exists.

Each strategy should summarize:
- stint lengths,
- stop count,
- predicted time delta.

Selecting one updates the Strategy visualization.

## Strategy visualization

Section name: **Strategy**.

Requirements:
- Tyre Life and Pace Loss modes,
- translucent compound band per stint,
- pit markers,
- tyre reset when changed,
- zoom in/out,
- horizontal pan/scroll.

Represent each stint as a first-class block.

Hover/focus on a stint should expose:
- lap range,
- starting fuel,
- starting VE,
- Fuel Ratio,
- target Fuel/lap,
- target VE/lap,
- compound,
- predicted tyre state at stint end,
- following pit Fuel/VE targets,
- tyre action,
- next compound.

## Strategy editing

Users can edit the recommended strategy.

Editable:
- pit lap,
- Fuel Ratio,
- tyre service,
- next compound.

After every edit:
- rebuild the plan,
- recalculate predicted total race time,
- show gain/loss relative to recommendation.

Provide:
- Back to Recommended.

## Compact overlay

For each metric:

~~~
FUEL TARGET
3.57 L/lap       <- large

Actual 3.52      <- small
+0.05 L/lap      <- green
~~~

Use:
- Fuel,
- VE,
- average tyre wear.

Do not write Ahead / Behind.

## Expanded overlay

Always show the current plan first:

~~~
NEXT PIT STOP                        CURRENT
Lap 12
+41 L · VE 71% · FL + FR
~~~

When the optimizer finds a materially better plan:

~~~
UPDATED STRATEGY                 FASTER PLAN
short reason

Lap 11
...

[ Keep Current ]    [ Replace ]
   Tap input         Hold input
~~~

No Recalculate button.

## Input behavior

One configurable wheel / keyboard / gamepad action:

- compact + tap -> expand,
- expanded without update + tap -> compact,
- update visible + tap -> Keep Current,
- update visible + long press -> Replace.

Visible buttons mirror the same actions.

The production Windows app should use global Raw Input / DirectInput / HID rather than depend on browser Gamepad behavior.

## Notification philosophy

Do not show a new strategy for every small model movement.

A strategy update should appear only when the recommended action materially changes.
