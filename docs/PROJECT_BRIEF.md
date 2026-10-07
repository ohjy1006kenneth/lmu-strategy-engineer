# Project Brief

## Problem

Le Mans Ultimate exposes race information, energy management and pit controls, but deciding the fastest complete race strategy still requires the driver to reason about fuel, Virtual Energy (VE / NRG), tyre degradation, tyre allocation, compound choice, pit-service time, weather and changing race conditions.

Static pit calculators can solve a snapshot. This product should go further:

- learn the driver's own behavior,
- use LMU session/game constraints automatically,
- create a useful pre-race plan quickly,
- continuously re-evaluate that plan during the race,
- and only interrupt the driver when a materially better plan exists.

## Target user

A sim racer who wants reliable strategy help without becoming a race engineer and without manually entering large amounts of data.

The system should work for:
- short daily races,
- medium-length races,
- long endurance races,
- timed races such as 45 minutes, 1h45, 6h and 24h.

## V1 scope

V1 is **Le Mans Ultimate only**.

In scope:
- pre-race strategy,
- Flat Out / Fuel Save comparison,
- Fuel / VE / tyre usage modeling,
- tyre compound and tyre-change strategy,
- weather-aware replanning,
- editable strategy,
- race-time gain/loss comparison,
- compact + expanded overlay,
- wheel / keyboard / gamepad action binding,
- local persistent learning.

Out of scope for V1:
- car setup recommendations,
- telemetry-analysis dashboards,
- spotter / radar,
- racing line coaching,
- generic AI chat,
- multi-sim abstraction,
- team cloud/server features,
- Stream Deck-specific workflow.

## Product identity

The intended identity is:

> Works after minimal clean data and becomes more personal as you race.

Hard game constraints come from LMU when available. Driver behavior comes from the user's own history. Generic priors may be used only for cold-start estimates where necessary and must be lower-confidence than personal data.

## Main surfaces

### Pre-race

The user should be able to see:
- race/session facts,
- forecast,
- tyre allocation and available compounds,
- strategy data source,
- buffer laps,
- average Fuel/lap,
- average VE/lap,
- average tyre wear/lap,
- Flat Out recommendation,
- optional Fuel Save recommendation,
- editable stint plan,
- total predicted gain/loss versus the recommendation.

### In-race

Compact:
- Fuel target large, actual small, red/green delta.
- VE target large, actual small, red/green delta.
- tyre-wear target large, actual small, red/green delta.

Expanded:
- current Next Pit Stop,
- Updated Strategy if one exists,
- Keep Current,
- Replace.

The live engine recalculates automatically. There is no manual Recalculate button.

## Differentiator

A useful mental comparison is a high-quality pit-stop calculator whose inputs are filled automatically from LMU and the driver's own history, with the additional ability to react mid-race.

The UI should feel like a race-strategy tool, not a generic SaaS dashboard.
