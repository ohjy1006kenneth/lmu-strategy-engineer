# LMU Integration

## Core rule

A value being visible in the LMU UI does **not** prove an external application can read it.

Every production field should be classified as:
- verified from current shared memory,
- verified from current REST / Swagger,
- derived,
- cached from session start,
- or unavailable.

Do not build behavior around a UI-only assumption.

## Adapter boundary

Expose a stable internal interface even if LMU changes external schemas.

Suggested capabilities:

- GetSession
- GetVehicle
- GetLiveTelemetry
- GetScoring
- GetForecast
- GetTyreInventory
- GetAvailableCompounds
- GetRecommendedCompoundConditions
- GetPitEstimate

Different capabilities may use different transports.

## Candidate LMU sources

Current and historical LMU integrations have exposed useful data through combinations of:
- native/shared memory,
- scoring shared memory,
- REST endpoints / local Swagger.

These interfaces can change between builds. Capability detection and version logging are mandatory.

## Fuel capacity

Use current-session / loaded-car fuel capacity only when actually exposed.

Do not maintain a car-name-to-capacity table as the primary source.

If capacity cannot be verified:
- do not silently invent one,
- degrade or disable fuel optimization that requires it.

## Tyres

When current LMU interfaces expose them, read:
- legal/available compounds,
- remaining dry tyre inventory,
- tyre-management options,
- compound-condition recommendation metadata.

Wet tyres do not decrement dry allocation.

## Auto compound

The intended Auto flow is:

1. ask LMU what compounds are legal/available,
2. use LMU condition/recommendation metadata when verified,
3. use personal data to refine wear and race-time economics,
4. never force the user to build a complete personal matrix for every car/track/compound combination before Auto can work.

## Opponent data

Opponent lap/sector timing may be useful for a temporary weather fallback.

Do not assume opponent fuel, VE, or tyre wear is externally observable online.

## Pit service estimate

Preferred order:

1. current LMU-provided service estimate,
2. app-maintained values validated against the current game build,
3. clearly lower-confidence fallback.

Do not require a user calibration pit stop as onboarding.

## Version resilience

Persist:
- game build,
- adapter/schema version,
- capability flags.

At startup:

1. detect build,
2. probe supported fields,
3. disable features dependent on missing fields,
4. never silently reinterpret a changed field.

## Fields requiring implementation-time verification

Third-party reverse engineering has reported useful fields/endpoints including concepts such as:
- fuel capacity,
- tyre inventory,
- garage tyre options,
- compound-condition recommendation metadata,
- pit-stop estimate.

Treat these as promising integration leads, not permanent API contracts. Verify against the current installed LMU Swagger/shared-memory definitions before production use.
