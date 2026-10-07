# Research Notes

This file records useful public integration leads. Re-verify all technical fields against the current LMU build during implementation.

## Official LMU references

Virtual Energy / NRG:
https://guide.lemansultimate.com/hc/en-gb/articles/13152376674191-What-is-Virtual-Energy-NRG

Multi Function Display:
https://guide.lemansultimate.com/hc/en-gb/articles/13202210967055-Understanding-the-MFD-Multi-Function-Display

Limited tyres:
https://guide.lemansultimate.com/hc/en-gb/articles/13210731599119-What-are-limited-tyres

RealRoad:
https://guide.lemansultimate.com/hc/en-gb/articles/13210664727055-What-is-RealRoad-RR

## Third-party integration references

RaceSimTools shared-memory documentation:
https://racesimtools.com/lmu-director/documentation/shared-memory

LMU Pit Companion:
https://github.com/valenjimeno/lmu-pit-companion

TinyPedal:
https://github.com/TinyPedal/TinyPedal

Simulator Controller:
https://github.com/SeriousOldMan/Simulator-Controller

Race Engineer LMU integration research:
https://github.com/Alexander-Gro/race-engineer

## Strategy-reference product

Ultimate Setup Hub pit-stop calculator:
https://ultimatesetuphub.com/pitstop-calculator

Useful product behavior to study:
- Flat Out versus fuel-saving strategies,
- Fuel/VE targets,
- pit length,
- tyre strategy,
- strategy comparison.

Do not copy private/proprietary implementation. Public behavior can inform requirements.

## Important verification notes

Third-party reverse engineering has reported current-build concepts such as:
- vehicle fuel capacity,
- tyre inventory,
- tyre garage options,
- compound-condition metadata,
- pit-stop estimate endpoint.

These are not treated as permanent official contracts in this repository.

Before relying on any field:
1. verify it in the installed build,
2. document transport and semantics,
3. add a capability flag,
4. add regression coverage for missing/changed fields.
