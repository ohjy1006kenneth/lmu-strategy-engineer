# Product Specification

## Product identity

LMU Strategy Engineer is a local Le Mans Ultimate race-strategy application.

The goal is:

> Use LMU's hard game/session constraints and the driver's own history to build a strategy before the race and continuously improve it during the race.

It should feel like a high-quality pit-stop calculator whose inputs are filled automatically, whose models become personalized, and whose strategy can react live.

## V1 scope

### In scope

- Le Mans Ultimate only
- timed races: sprint, medium and endurance
- pre-race strategy
- Flat Out and optional Fuel Save strategy
- Fuel / VE / Fuel Ratio planning
- tyre wear and degradation modeling
- compound selection
- tyre-change economics
- weather crossover
- editable strategy with predicted time delta
- compact and expanded in-race overlay
- configurable wheel / keyboard / gamepad input
- local persistent learning

### Out of scope

- setup recommendations
- generic telemetry dashboards
- spotter / radar
- racing-line coaching
- generic AI chat
- multi-sim support in V1
- team cloud/server features
- Stream Deck-specific workflows

## Core product principles

1. **Personal first.** Fuel, VE, tyre wear, tyre pace loss and fuel-saving cost should converge toward the user's own data.
2. **No fake certainty.** Missing data must not silently become high-confidence predictions.
3. **LMU owns hard constraints.** Use verified game/session data for capacities, availability, weather and rules.
4. **Optimize total race time.** Fuel, VE, saving, tyres, weather and pit costs belong in one strategy.
5. **Live but quiet.** Replan often, notify only when action materially changes.
6. **Minimal UI.** Show race decisions, not a generic dashboard.

## Pre-race screen

### Race information

Show:

- track/layout
- car
- race duration
- estimated laps
- dry tyre allocation / remaining inventory
- available compounds
- weather forecast
- LMU connection status

Estimated laps for a timed race are a strategy estimate, not an official fixed lap count.

### Strategy data

Source:

- **Qualifying**
- **Recent Clean Laps**

Qualifying:
- do not show the recent-lap count stepper.

Recent Clean Laps:
- show minus / N / plus,
- N cannot exceed matching recorded clean laps,
- disable/grey plus at the limit.

Also show:

- Buffer Laps
- Start Compound

Start Compound displays simply **Auto** when Auto is selected. Do not expose awkward text such as "LMU recommends Medium" in the selector.

### Driver model

Show only:

- average Fuel/lap
- average VE/lap
- average tyre wear/lap across all four tyres

Do not show Tyre Pace as a separate pre-race card. Tyre pace modeling remains internal.

## Strategy choices

Expose at most:

### Flat Out

Uses normal modeled consumption and pace.

### Fuel Save

Only appears when the backend can construct a distinct feasible saving plan.

The backend may evaluate several saving layouts internally, but the user sees only the best Fuel Save candidate.

Each option should summarize:

- stint lengths
- stop count
- predicted time delta

## Strategy visualization

Section name: **Strategy**.

Requirements:

- Tyre Life mode
- Pace Loss mode
- arbitrary number of stints
- pit markers
- tyre reset after tyre service
- translucent compound band per stint
- zoom in/out
- horizontal pan/scroll

Each stint should be inspectable by hover/focus and reveal:

- lap range
- start Fuel
- start VE
- Fuel Ratio
- target Fuel/lap
- target VE/lap
- compound
- predicted tyre state at stint end
- following pit Fuel/VE targets
- tyre action
- next compound

## Strategy editing

The user can modify:

- pit lap
- Fuel Ratio
- tyre action
- next compound

Every edit rebuilds/evaluates the plan and immediately shows predicted total race-time gain/loss versus the recommended strategy.

Provide **Back to Recommended**.

## Compact overlay

For Fuel, VE and average tyre wear:

~~~text
FUEL TARGET
3.57 L/lap      <- large

Actual 3.52     <- small
+0.05 L/lap     <- green
~~~

Do not write Ahead / Behind. The color and signed delta are enough.

## Expanded overlay

Always show the current strategy first:

~~~text
NEXT PIT STOP                        CURRENT
Lap 12
+41 L · VE 71% · FL + FR
~~~

When a materially better plan exists:

~~~text
UPDATED STRATEGY                 FASTER PLAN
short reason

Lap 11
...

[ Keep Current ]    [ Replace ]
   Tap input         Hold input
~~~

There is **no Recalculate button**.

## Input behavior

One configurable action input:

- compact + tap -> expand,
- expanded with no update + tap -> compact,
- update visible + tap -> Keep Current,
- update visible + long press -> Replace.

Visible buttons mirror the same actions.

The production Windows app should use global Windows input so this works while LMU owns focus.

## Accepted strategy decisions

- No generic permanent tyre baseline; personal clean laps become the normal baseline.
- A temporary prior/fallback is allowed for cold-start decisions, but with lower confidence.
- Dry and Wet models stay separate.
- The solver supports arbitrary-length stint plans.
- Wet tyres do not consume dry tyre allocation.
- Soft / Medium / Hard are not modeled as a universal pace ranking.
- Auto compound should use verified LMU event availability and condition recommendation when available.
- The user should not need to drive every compound combination before Auto can work.
- Fuel/VE knowledge should not disappear just because the driver changes dry compound.
- Race-start Fuel and VE are optimization variables; do not force max or minimum universally.
- Timed-race lap projection updates during the race.
- Strategy recalculation is continuous; UI notifications are only for meaningful changes.
