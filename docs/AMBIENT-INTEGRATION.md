# Ambient integration — CrimsonHue 0.2.2

The owner observed all three lamps turning red in bright daytime because 0.1.3
mapped only local light sources. CrimsonHue now consumes CDT's independent
`/v1/ambient/stream` as a camera-local brightness signal. CDT owns capture and
the neutral contract; CrimsonHue contains no ambient capture implementation.
The producer contract is `C:\DEV\CrimsonDesertTelemetry\docs\AMBIENT_STREAM.md`.

## Input and visual mapping

Require schema 1.0, `precompute-ambient-sky`, global upper-hemisphere scope,
relative shader units, an available sky sample, measured camera visibility and
an available, non-stale `localEnvironmentAmbientEstimateWorking.rgbWorking`.
Validate the local product against sky mean × visibility. Sky and visibility
have separate 1500 ms age limits, including client time and silence. A missing
or stale product is labelled as a local-only fallback and fades out of output.

Working RGB is AP1-like and not calibrated to display RGB or local-light RGB.
CrimsonHue therefore uses only the **mean of its nonnegative channels** as a
relative level `W`; it does not copy those channels into lamp RGB. With the
adjustable Ambient influence `S` (default 1, range 0–3), the artistic level is
`A = 1 - exp(-W × S / 4)`, smoothed over 0.4 seconds. Fresh Ambient presence
`P` is smoothed separately over the same period. Each Hue channel receives
a directionless neutral baseline `0.08 × P + 0.4 × A`. Local contributions retain
their player-distance fade and per-source HDR bound, then are scaled by
`1 - 0.3 × A`. A very dark valid Ambient estimate thus still gives a small
neutral fill, and bright Ambient no longer removes nearly all local contrast.
When Ambient is stale, `P` and `A` fade out. Sensitivity 0 restores local-only
output. Baseline and local RGB are combined in linear output space, overflow-
normalized, sRGB-encoded and brightness-limited.
This is an empirical visual response, not photometry or a calibrated conversion
between CDT's two RGB domains. Ambient influence 0 restores local-only output.

No clock-time or daylight flag is inferred. Bright sky with low camera openness
produces a dark local product, so an interior during daytime can retain strong
local colors. Ambient has no invented direction; real material response, sun disk
and indoor GI are not modeled.

Local-light direction uses the imported Hue channel positions. For each source,
`exp(2 × Spread × (dot - 1))` is divided by the strongest weight among the area's
channels. This makes camera yaw more visible in sparse layouts while preserving
the player-distance fade. A warm-light heuristic raises green toward 0.8 of red
for orange-family source RGB. It leaves pure red, blue, green and near-white
sources unchanged. This responds to an observed fire whose engine-light RGB was
roughly `1 : 0.40 : 0.11`, while the visible flame looked yellow. The HUD and CDT
API agreed on the source RGB; this correction is not evidence of a CDT defect.
Its match to a physical lamp still needs owner observation.

## Evidence and remaining acceptance

Synthetic tests cover neutral Ambient fill, stronger daylight local contrast,
yellow fire, camera yaw, dark Ambient, independent sky/visibility expiry,
fallback, schema rejection and one-time brightness-preset migration. The previous
0.2.1 read-only CDT and bounded physical runs established input and transport
behavior, not the visible appearance of 0.2.2 on lamps.

The owner must assess the exact 0.2.2 package at bright outdoor, dark
interior and night scenes, including a visible light, a blocked light and a
source behind the camera. Check transitions and both freshness fallbacks.
Calibration can then be adjusted with paired game screenshots, CDT samples and
real-lamp observations. Do not claim pixel matching, physical brightness or
complete environment illumination from this feed.
