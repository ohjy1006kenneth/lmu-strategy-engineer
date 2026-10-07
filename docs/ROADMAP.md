# Roadmap

## Phase 0 — Product prototype
Current / mostly complete.

- [x] Pre-race interactive prototype
- [x] Compact and expanded overlay prototype
- [x] Arbitrary-length stint representation
- [x] Editable strategy concept
- [x] Flat Out / Fuel Save concept
- [x] Strategy graph zoom/pan
- [x] Per-stint compound representation
- [x] Updated Strategy / Keep Current / Replace overlay
- [x] Synthetic 24h and 1h45 stress tests

## Phase 1 — Production solution skeleton

- [ ] Create .NET solution / project structure
- [ ] Domain objects with explicit units
- [ ] Pure strategy-core library
- [ ] Reusable deterministic simulation engine
- [ ] 17 canonical timed-race scenarios
- [ ] Fast / Average / Casual driver profiles
- [ ] Fixed simulation-results overwrite workflow
- [ ] SQLite persistence project
- [ ] Structured logging
- [ ] Capability/version model for LMU adapter

## Phase 2 — LMU read-only adapter

- [ ] Shared-memory integration
- [ ] REST/Swagger capability detection
- [ ] Session identification
- [ ] Fuel capacity
- [ ] Live Fuel / VE
- [ ] weather / RealRoad inputs
- [ ] tyre inventory
- [ ] available compounds
- [ ] compound-condition recommendation
- [ ] pit-service estimate
- [ ] scoring / opponent lap timing

## Phase 3 — Persistent personal learning

- [ ] clean-lap filtering
- [ ] Fuel/lap model
- [ ] VE/lap model
- [ ] average + per-corner tyre-wear model
- [ ] tyre pace-loss model
- [ ] fuel-saving penalty model
- [ ] confidence / coverage tracking

## Phase 4 — Strategy optimizer

- [ ] Flat Out candidate generation
- [ ] start Fuel/VE optimization
- [ ] Fuel Ratio optimization
- [ ] tyre-service optimization
- [ ] Auto compound integration
- [ ] Fuel Save candidate generation
- [ ] weather crossover
- [ ] rolling timed-race lap prediction
- [ ] material-change classifier

## Phase 5 — Windows UI

- [ ] Pre-race desktop UI
- [ ] strategy graph
- [ ] zoom/pan
- [ ] strategy editor
- [ ] time delta
- [ ] transparent always-on-top overlay
- [ ] global input binding
- [ ] short/long press state machine

## Phase 6 — Live validation

- [ ] compare predictions with recorded LMU sessions
- [ ] verify pit-estimate semantics
- [ ] verify compound-recommendation fields
- [ ] verify tyre inventory semantics
- [ ] verify online opponent-data availability
- [ ] regression suite across LMU updates
