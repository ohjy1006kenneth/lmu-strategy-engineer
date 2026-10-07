# Engineering Decisions

These decisions are current product requirements unless deliberately revisited.

## D001 — LMU only for V1
Accepted.

Do not build a generic sim abstraction before the LMU product works.

## D002 — Personal baseline
Accepted.

The user's clean laps become the normal personal baseline.

Cold-start priors/fallbacks may exist, but must not masquerade as personal knowledge.

## D003 — Dry/wet separation
Accepted.

Do not merge fundamentally different road-condition models.

## D004 — Arbitrary-length stint planner
Accepted.

No two-stint-only solver.

## D005 — Two user-facing strategy concepts
Accepted.

Expose:
- Flat Out,
- Fuel Save when available.

Internal candidate generation can be more complex.

## D006 — Editable strategy
Accepted.

Users can edit a strategy and see predicted total time gain/loss versus recommendation.

## D007 — LMU-guided Auto compound
Accepted.

Do not require a complete user-generated Soft/Medium/Hard matrix before Auto works.

Use actual event availability and verified LMU recommendation metadata.

## D008 — Wet tyres excluded from dry allocation
Accepted.

Maintain dry inventory separately.

## D009 — Quiet live replanning
Accepted.

Recalculate often; notify only for materially different action.

## D010 — Overlay decision model
Accepted.

No Recalculate button.

When an update exists:
- tap = Keep Current,
- long press = Replace.

Visible buttons mirror the same actions.

## D011 — Race-start Fuel/VE are optimization variables
Accepted.

Do not force start VE = 100%.
Do not force minimum start VE.

Compare free pre-race loading / mass effects against future pit-service time.

## D012 — Rolling timed-race distance
Accepted.

Estimated laps update with actual pace, pits and neutralization.

## D013 — Pre-race driver model stays minimal
Accepted.

Display:
- Fuel/lap,
- VE/lap,
- average four-tyre wear/lap.

Do not display Tyre Pace as a separate card.

## D014 — Strategy is the primary visualization
Accepted.

Fuel, VE, Fuel Ratio, tyres, compound and pit action belong to stints/pits in one strategy timeline rather than unrelated widgets.
