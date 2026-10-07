# Data Model

## LapRecord

Persist enough context so later model selection is reproducible.

Suggested fields:

### Identity
- id
- timestamp
- driver id
- car id
- car class
- track id
- layout id
- session type
- session id

### Timing
- lap number
- lap time seconds
- sectors
- validity

### Fuel / energy
- fuel start L
- fuel end L
- fuel used L
- VE start %
- VE end %
- VE used %
- Fuel Ratio / engine-map context

### Tyres
- compound
- tyre age laps
- remaining FL / FR / RL / RR
- wear FL / FR / RL / RR
- temperatures if available

### Environment
- road-condition family
- road wetness
- track temperature
- air temperature
- RealRoad / grip state if exposed
- rain state / forecast snapshot

### Context flags
- pit in
- pit out
- formation
- yellow
- Slow Zone
- Safety Car
- off-track
- crash/spin
- traffic quality
- telemetry completeness

### Versioning
- game build
- physics version
- BOP identifier when available
- schema version

## Model keys

### Tyre model

At minimum:

~~~
driver
+ car
+ track/layout
+ road-condition family
+ compound
~~~

### Fuel / VE model

Changing between dry compounds should not automatically erase Fuel/VE knowledge.

Prefer a key like:

~~~
driver
+ car
+ track/layout
+ road-condition family
+ relevant environment
~~~

Compound can be a secondary feature rather than a hard partition unless testing proves it materially changes consumption.

## Road conditions

Never blindly average dry and wet.

Suggested families:
- Dry
- Damp / transitional
- Wet

The exact mapping should follow what the current LMU build reliably exposes.

## Lap filtering

Hard reject from normal learning:
- incomplete laps,
- pit in/out,
- formation,
- telemetry loss,
- severe incident/spin.

Exclude neutralized laps from normal pace learning:
- yellow,
- Slow Zone,
- Safety Car.

Traffic may still be useful for consumption while being downweighted or corrected for pace.

## Recent Clean Laps UI

The selector must not exceed actual matching data.

When N reaches the available matching count:
- show only N,
- disable/grey the plus control.

When Qualifying is selected:
- do not show the Recent Clean Laps stepper.

## Model outputs

Store both:
- point estimate,
- confidence / coverage metadata.

The UI should stay simple; confidence is mainly for solver risk handling and debugging.
