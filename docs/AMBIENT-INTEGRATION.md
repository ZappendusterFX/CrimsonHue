# Ambient integration — CrimsonHue 0.2.1

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
`A = 1 - exp(-W × S / 4)`, smoothed over 0.4 seconds. Each Hue channel receives
a directionless neutral baseline `0.8 × A`. Local contributions retain their
existing player-distance fade, camera direction weight and per-source HDR bound,
then are scaled by `max(0.05, (1 - A)^1.5)`. Baseline and local RGB are combined
in linear output space, overflow-normalized, sRGB-encoded and brightness-limited.
This is an empirical visual response, not photometry or a calibrated conversion
between CDT's two RGB domains. Ambient influence 0 restores local-only output.

No clock-time or daylight flag is inferred. Bright sky with low camera openness
produces a dark local product, so an interior during daytime can retain strong
local colors. Ambient has no invented direction. A small residual local contrast
remains at high `A`; real material response, sun disk and indoor GI are not modeled.

## Evidence and remaining acceptance

Synthetic tests cover daylight-neutral output near a red source, dark-interior
local color, independent sky/visibility expiry, fallback, schema rejection and
settings migration. A read-only live CDT Ambient stream had 40 fresh reads and
nine progressing sky captures over four seconds. A bounded source-built physical
session sent nonzero, near-neutral mapped colors, then released the area and
completed the restoration path. These are separate from visible lamp acceptance.

The owner must still assess the exact 0.2.1 package at bright outdoor, dark
interior and night scenes, including a visible light, a blocked light and a
source behind the camera. Check transitions and both freshness fallbacks.
Calibration can then be adjusted with paired game screenshots, CDT samples and
real-lamp observations. Do not claim pixel matching, physical brightness or
complete environment illumination from this feed.
