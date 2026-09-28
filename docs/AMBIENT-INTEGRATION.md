# Ambient integration requirements — calibration pending

User requirement, 2026-09-07: the room should reflect general environment light,
and distant fires/lamps must not dominate it. In bright daylight, ordinary local
lights should have little or no visible influence. In dark surroundings, nearby
lights should remain pronounced. The distance-fade controls ship in 0.1.1;
ambient input and ambient-relative mixing do not. CDT now documents the separate
`/v1/ambient` feed: camera-local sky visibility and a working RGB sky estimate.
The estimate uses relative shader units and AP1-like RGB, and is not calibrated
against local-light RGB or display color. Do not combine them as if the numbers
were directly comparable.

## Inputs and ownership

CrimsonDesertTelemetry owns capture and its neutral HTTP/WebSocket contract.
The producer documents availability, units, color space, timestamps and sampling
location in `C:\DEV\CrimsonDesertTelemetry\docs\AMBIENT_STREAM.md`. It does not
provide a calibrated conversion to local-light RGB or output brightness. The
existing rendered RGB values alone do not establish environment brightness.
Clock time is not a substitute.

## Intended mapping

1. Use fresh general environment light as a baseline, with direction only if the
   real API provides it. Do not manufacture directional ambient samples.
2. Derive local-light contrast from source strength relative to the actual
   environment, after bringing both into a documented comparable linear/exposure
   convention. Raw HDR numbers from unrelated scales cannot simply be divided.
   Calibrate the response with captured paired samples once available.
3. Apply the existing player-relative distance fade and camera/Hue direction
   weighting to local effects. Ambient suppression must never re-amplify a source
   past its distance weight or revive a source outside the configured cutoff.
4. Combine the ambient baseline and bounded local effects in linear RGB, then
   perform the final output conversion, brightness limits and smoothing. Revisit
   0.1.1's local-only tone mapping when the real input semantics are known; do not
   simply add ambient on top of already saturated output.

Ordinary fires/lamps should disappear perceptually in bright outdoor light without
a blanket "daytime means off" rule. Dark rooms, caves, shade and night can still
show local effects. An exceptionally bright source may remain visible even in
daylight. This remains a lighting estimate, not an occlusion or pixel-matching
model; those require additional evidence/inputs.

## Safety and acceptance gates

- Track ambient freshness independently. Stale or absent ambient must not become
  a permanently held background or be silently interpreted as a measured black
  environment. Define and label a safe local-only fallback once the real producer
  contract is known. Retain existing session ownership and shutdown guarantees.
- Avoid visible jumps when ambient becomes available, changes rapidly or drops
  out. Exercise indoor/outdoor transitions and exposure changes, not just noon
  and midnight.
- Test bright outdoors near a small fire, the same source in darkness, a dark
  daytime interior, many distant sources, cutoff crossing in both directions,
  source/ambient freshness mismatch and reconnects.
- Record synthetic math tests, read-only live telemetry observations and actual
  lamp acceptance separately. Do not claim ambient behavior before all required
  input semantics and the implemented mapping have been verified.
