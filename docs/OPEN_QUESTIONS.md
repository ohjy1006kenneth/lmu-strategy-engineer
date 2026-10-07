# Open Questions

These items should be resolved by implementation-time measurement or explicit product decision. Do not silently guess.

## LMU API / data

- Which fuel-capacity field is most reliable in the current LMU build: shared memory, REST, or both?
- What are the exact semantics of tyre inventory: remaining tyres, sets, corner-specific inventory, and session reset behavior?
- Is the available-compound list complete and stable across event types?
- What exactly does compound-condition recommendation metadata expose in the current build?
- What pit-stop-estimate fields are available now, and do they include lane loss, service breakdown, or only stationary time?
- What changes while the car is on track versus only while in garage/menu?
- Which opponent fields are available online for lap time, compound, sectors and pit state?
- How are Safety Car / Slow Zone / FCY states represented?

## Strategy calibration

- What is the actual LMU relationship between requested VE/NRG service and stationary time by class/build?
- How should physical fuel mass affect predicted lap time for each car/class?
- How strongly does Fuel Ratio change physical fuel mass for a fixed VE/NRG target?
- What empirical prior should be used for fuel-saving lap-time penalty before a personal model exists?
- How much confidence penalty should be applied when extrapolating tyre pace loss beyond observed tyre-life coverage?
- Which pit services are concurrent versus sequential in the current rules/build?

## Weather

- Which road-wetness / RealRoad values are reliably available live?
- What data is available for forecast timing and intensity?
- What hysteresis is needed to prevent repeated Wet/Slick recommendation flicker?
- If no personal Wet model exists, which field samples are reliable enough for a normalized same-class fallback?

## UI implementation

- Choose the production Windows UI framework.
- Verify transparent, click-through, always-on-top overlay behavior over LMU fullscreen/borderless modes.
- Verify Raw Input / DirectInput / HID behavior for common wheels while LMU owns focus.
- Determine a configurable long-press threshold; prototype currently uses about 650 ms.

## Product validation

- Define the threshold for a "material" strategy improvement before surfacing Updated Strategy.
- Decide how much uncertainty/risk can be traded for a theoretically faster strategy.
- Decide whether a user can pin a strategy constraint, for example "do not change tyres" or "must use Wet at next stop."

Resolve these through measurements, tests and user validation, then record accepted outcomes in docs/DECISIONS.md.
