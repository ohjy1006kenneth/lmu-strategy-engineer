# Research and Open Questions

This file is for implementation-time verification, public references and unresolved questions.

Do not treat third-party reverse engineering as a permanent LMU API contract.

## Official LMU references

Virtual Energy / NRG:
https://guide.lemansultimate.com/hc/en-gb/articles/13152376674191-What-is-Virtual-Energy-NRG

Multi Function Display:
https://guide.lemansultimate.com/hc/en-gb/articles/13202210967055-Understanding-the-MFD-Multi-Function-Display

Limited tyres:
https://guide.lemansultimate.com/hc/en-gb/articles/13210731599119-What-are-limited-tyres

RealRoad:
https://guide.lemansultimate.com/hc/en-gb/articles/13210664727055-What-is-RealRoad-RR

## Third-party integration leads

RaceSimTools shared memory:
https://racesimtools.com/lmu-director/documentation/shared-memory

LMU Pit Companion:
https://github.com/valenjimeno/lmu-pit-companion

TinyPedal:
https://github.com/TinyPedal/TinyPedal

Simulator Controller:
https://github.com/SeriousOldMan/Simulator-Controller

Race Engineer integration research:
https://github.com/Alexander-Gro/race-engineer

Strategy-reference product:
https://ultimatesetuphub.com/pitstop-calculator

Study public behavior and requirements; do not copy proprietary/private implementation.

## LMU questions to verify

- Which fuel-capacity field is most reliable in the current build?
- Exact tyre-inventory semantics: tyres, sets, corners, session reset?
- Exact available-compound representation?
- What does current compound-condition recommendation metadata expose?
- What does current pit-stop estimate expose: stationary time, lane loss, breakdown?
- Which garage/REST values freeze or remain live while driving?
- Which opponent fields are available online: lap, sectors, compound, pit state?
- How are yellow / Slow Zone / Safety Car states represented?
- Does opponent compound data reliably support Wet-field normalization?

## Strategy calibration questions

- Current relationship between requested VE/NRG service and stop time?
- Fuel-mass effect on lap time by car/class?
- Fuel Ratio's exact effect on physical fuel for a given VE plan?
- Best cold-start prior for fuel-saving time penalty?
- Confidence penalty for tyre degradation extrapolation?
- Which pit services are concurrent versus sequential in current LMU rules/build?

## Weather questions

- Which road-wetness / RealRoad fields are reliable live?
- What forecast timing/intensity data is available?
- What hysteresis threshold avoids crossover flicker?
- How many clean opponent Wet samples are enough for a field fallback?

## UI/platform questions

- Verify WPF transparent/click-through/always-on-top behavior over LMU fullscreen/borderless modes.
- Verify Raw Input/HID across common wheelbases while LMU owns focus.
- Tune configurable long-press threshold; prototype uses roughly 650 ms.

## Public account, synchronization and hosting questions

These require explicit product/security decisions before implementing public sync:

- Which supported account-login, verification, password recovery and token revocation scheme is appropriate for a self-hosted public service without unplanned recurring costs?
- What UX links offline-local history to an account? Should local-only use remain available without registration, and how are shared-PC sign-outs or account switches isolated?
- What is the approved merge policy for simultaneous edits to preferences, models and annotations across two signed-in PCs? Which objects can merge automatically and which must prompt for a conflict decision?
- What record identity, revision, idempotency key, tombstone retention and cursor protocol guarantees convergence without duplicate laps or data loss?
- What upload frequency, batch sizes, offline queue ceiling and server disk quotas work for the Pi 5 and its 1 TB USB HDD?
- Which retention windows apply to personal data, deleted records, revoked accounts and off-host encrypted backups?
- How are account export/delete requests verified, processed, and transparently reported when old backups expire later?
- How is the sync API isolated from Hermes, public Docker apps, and NAS users, and what are the measured HDD throughput and concurrent-user limits?
- Which Cloudflare Tunnel hostname, authentication boundary, abuse/rate limiting and incident response policies will be used for public access?
- What happens to registered users if the Pi/HDD is offline, loses data, or the operator eventually moves to a paid hosting provider?
- Does the server need an account/control-plane registry on NVMe, and how will that registry be securely backed up and kept consistent with the separate per-user HDD databases?

Do not treat these unanswered implementation choices as permission to skip mandatory isolation, backup, or data-loss protections.

## Product questions

- Material-strategy-improvement threshold before showing Updated Strategy.
- How much uncertainty/risk can be accepted for a theoretically faster plan?
- Whether users can pin hard constraints such as "do not change tyres."

When an answer becomes verified and accepted, move the resulting rule into `docs/PRODUCT.md` or `docs/ENGINEERING.md`.
