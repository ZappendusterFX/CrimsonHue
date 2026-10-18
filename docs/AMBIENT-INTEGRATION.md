# Ambient and mapping controls — CrimsonHue 1.0.0

CrimsonHue consumes CDT's independent `/v1/ambient/stream` and local-light feed.
CDT owns capture and the neutral contracts. CrimsonHue shows the received values
separately from its calculated lamp output and exposes the visual adjustments in
the app. The producer contract is
[CDT Ambient stream](https://github.com/ZappendusterFX/CrimsonDesertTelemetry/blob/v2.2.5/docs/AMBIENT_STREAM.md).

The owner rejected 0.2.2's built-in dark-scene fill and orange-to-amber correction.
Version 0.3.0 removes those special rules. **Extra background light** defaults to
0%; color balance defaults to 1×, **Local saturation** to 100%, and **Local hue
shift** to 0°. No color family receives a special adjustment. Saved brightness
is retained without an automatic upgrade.

## Raw input

Require Ambient schema 1.0, `precompute-ambient-sky`, global upper-hemisphere
scope, relative shader units, an available sky sample, measured camera visibility
and an available, non-stale `localEnvironmentAmbientEstimateWorking.rgbWorking`.
Validate the local product against sky mean × visibility. Sky and visibility
have separate 1500 ms age limits, including client time and silence. A missing or
stale product is labelled as a local-only fallback and removes Ambient fill
immediately, including any configured Extra background light.

The UI shows global sky RGB, camera sky visibility, local Ambient RGB and its
exact working level. It also lists the confirmed-clear local sources with raw
positions and linear RGB. These values remain unmodified by the controls.
Mapped channel RGB is shown separately and is not a physical lamp measurement.
During sync, the Ambient status also reports whether the actual stream mapper
included Ambient. The raw tab shows its last input level, linear fill before local
mix/gamma/master, and sent-frame counters for local clearing and missing Ambient.
These diagnostics are published after a transport send; they do not prove physical
lamp output. They distinguish fresh input reception from use by the stream mapper.
**Freeze readout** holds the raw display for inspection and copying. It does not
pause incoming telemetry, the mapper or the lamp stream.

Working RGB is AP1-like and is not calibrated to display RGB or local-light RGB.
CrimsonHue uses the mean of its nonnegative channels as relative level `W`.
The raw channels are displayed for inspection; they are not copied into lamp RGB.
No clock-time or daylight flag is inferred. Bright sky with low camera openness
can produce a dark local product. Ambient has no invented direction; sun disk,
material response and indoor global illumination are not modeled.

## Recommended and custom response

New installations use a versioned recommended profile with three independent
amounts and one response choice. It resolves to the mapping below: threshold/floor
zero, exposure/reference 1/4, neutral color/gamma, preserved room alignment.
Quick uses 100/400 ms local/background transitions, Balanced 250/600 ms, and Calm
500/1000 ms. Custom tuning remains a separate saved record; old installations
stay Custom until the owner explicitly selects Recommended. Profile resolution
does not inspect scene brightness or overwrite room calibration.

Exposure and reference are mathematically redundant as a pair: only their ratio
affects the response (except exposure zero, which disables the layer). Recommended
therefore fixes that ratio. Amount, source compression, angular shape and distance
fade do different jobs; the profile supplies a consistent baseline rather than
claiming that Hue positions determine all of them. No lamp cutoff is inferred
from the normal-dimming min_dim_level metadata.

## Advanced Ambient response

The constants below remain visible in Advanced and editable in Custom mode.
Their custom defaults and ranges are listed in the README controls table.
Percentage controls are divided by 100 for the formulas below: `mix` is Ambient
mix, `background` is Extra background light, and `localMix` is Local mix in bright
Ambient. `exposure`, `reference` and `threshold` denote Ambient exposure, Ambient
reference level and Ambient black threshold respectively.

1. Subtract Ambient black threshold from `W`, clamping the result at zero.
2. Compute `target A = 1 - exp(-max(0, W - threshold) × exposure / reference)`.
3. Smooth `A` with Ambient transition in milliseconds. Zero transition updates
   immediately. Missing/stale Ambient or Ambient exposure zero clears the Ambient
   state immediately.
4. Set fill strength to `background + mix × A`. Apply it only while valid Ambient
   is available and Ambient exposure is greater than zero.
5. Form the fill color from Ambient tint hue and Ambient tint saturation, in the
   Color tab, with peak one. Saturation 0% gives neutral RGB. Multiply it by fill
   strength.
6. Multiply local-light contribution by
   `1 + (localMix - 1) × A`.

Defaults are Ambient exposure 1×, Ambient reference level 4, Ambient mix 40%,
Extra background light 0%, Ambient black threshold 0, Ambient transition 400 ms,
Local mix in bright Ambient 100% and Ambient tint saturation 0%.
Thus, bright Ambient does not automatically suppress local lights. Zero Ambient
plus no local lights gives black; a transition from a previously bright Ambient
sample follows Ambient transition. A tiny positive `W` can still produce tiny
positive output. Ambient black threshold lets the user choose where that becomes
black without changing the raw readout. Setting Extra background light above 0%
deliberately keeps a baseline while valid Ambient is available.

## Exposed local-light and output response

Only sources accepted by the existing availability, paired-capture and fresh
confirmed-clear visibility checks reach the mapper. This is collision-based
visibility: glass or enclosing fixture collision can reject a visibly illuminated
source. No optical material or screen-space contribution test is implemented.
Color controls apply to all
local-light colors: multiply linear RGB by Red balance, Green balance and Blue
balance, then apply Local hue shift and Local saturation. The default values
preserve source ratios.
There is no fire classification or automatic orange-to-yellow conversion.

Light sensitivity bounds each source's HDR peak with
`1 - exp(-peak × sensitivity)`, retaining a common RGB scale before spatial fade.
The resulting bounded source is multiplied by its reported kind weight: Point
lights 0–200%, Spotlights 0–1000%, both initially 100%. A conservatively matched
own-lantern pair additionally receives the 0–100% Own lantern multiplier
(Recommended 10%, neutral legacy/Custom default 100%). Recognition uses the
observed complete-pair signature on build 25477059; incomplete, ambiguous or
unsupported input keeps ordinary type weighting. Grouping is independent of
point/spot kind and does not add a second member-count multiplier. Untyped
sources retain unit weight. Raw source values remain unchanged.
Player-relative distance remains full strength through Fade starts and reaches
zero at Off beyond. With normalized interval `t`, the weight is
`(1 - t² × (3 - 2t)) ^ falloff`. The **Distance falloff** setting supplies the
exponent; the default is 2. The curve preview plots remaining contribution in
percent against game-unit distance and updates with all three distance controls.

**Direction origin** chooses the positional reference for the room:
`origin = player + blend × (pairedCameraPosition - player)`.
The visible percentage ranges from 0% (raw player position) to 100% (camera lens),
defaulting to 50%, including old configurations that lack this setting. All three
coordinates interpolate along the same line, without a height offset or inferred
eye position. The paired camera's right/forward/up axes still orient the source
vector. Thus third-person sources behind the character but in front of the lens
can map to different room sides depending on this explicit choice. Distance
attenuation remains measured from the player. Preview source dots and the mapper
use the same shared direction function; the origin percentage is displayed below
the room preview. CDT's visibility measurement remains producer-owned.

**Room direction offset** rotates that source direction in camera-local coordinates.
All directions use one spherical weighting rule. There are no left/right,
front/rear or above/below cuts. Legacy `SeparateLeftRight` and `SeparateFrontRear`
values remain in saved settings and version-1 presets for compatibility, but
have no mapping effect and are no longer controls in the HUD.

Each source covers a spherical cap around its direction. For unit source and
channel directions, `theta = acos(clamp(dot, -1, 1))` is their great-circle angle.
Divide by **Source influence radius** to obtain normalized radius `r`; `r >= 1`
contributes zero. Recommended/reset radius is 30 degrees, adjustable from 1 to
180; saved Custom values and historical omitted-field defaults are preserved.
The same angle works across every axis, diagonal, rear direction and pole.
This is angular coverage around the viewer; world-distance fade stays separate.
A point lamp at the exact room center has no direction and remains omnidirectional.

**Spherical edge softness** uses 20% of the angular radius for Recommended/reset.
Saved Custom values and historical omitted-field defaults are preserved. Its inner cap
(`r <= 1 - softness`) has coverage one; the outer ring uses smoothstep down to
zero at the edge. Zero softness explicitly selects a sharp circular edge, so use
a positive softness for continuous movement. This footprint is a mapping
choice, not a reflection estimate or a claim about the renderer's light volume.

Imported channel positions supply geometric angular affinity
`exp(Directional focus × (dot - 1))`. **Strongest-channel compensation** blends
the absolute contribution with the contribution divided by the strongest
geometric affinity. Its range is 0–100%. The denominator uses all room directions,
independent of cap membership, preventing jumps as a channel enters the footprint.
Cap coverage remains outside compensation and cannot be boosted back to full.
Wall channels integrate coverage times affinity over their calibrated regions;
their denominator uses full-region average affinity, not only the covered patch.
This changes wall intensity compared with the previous covered-patch normalization.
The cap integral is numerically bounded by the full-region affinity. Neither this
control nor spatial weighting claims constant total physical room brightness.

**Local light mix**, divided by 100, scales the summed contributions before
overflow normalization.
Ambient fill and the Ambient-adjusted local contribution are added in linear
output space, and overflow is normalized again. The result is sRGB-encoded, then
**Output gamma** applies `encoded ^ (1 / gamma)`. Gamma one is neutral;
larger values brighten intermediate output and smaller values darken it. The
**Maximum brightness** and imported channel balance cap the final output.
Color controls, gain and gamma all preserve zero input as zero output.

**Local light transition** applies in all input modes, including confirmed-clear
input. After local summation/overflow normalization,
encode the local RGB to sRGB and use the exponential response
`alpha = 1 - exp(-elapsedSeconds * 1000 / transitionMs)`. Decode the smoothed local
RGB back to linear before combining Ambient and applying output gamma/brightness.
Zero transition is immediate. Streaming uses elapsed monotonic time between mapped
frames; the first frame uses the nominal 1/30 second step.

Spatial rules apply to each new target. The previous output fades briefly after
a source becomes hidden or crosses a side; this avoids abrupt flashes from fast
Entertainment lamps. Ambient retains its separate transition and clears immediately
when unavailable. Master brightness applies after smoothing, so lowering its limit
takes effect immediately. Full unavailable local telemetry resets both layers and
clears output; smoothing never keeps an old frame alive through a data disconnect.

These steps are an adjustable visual response, not photometry or a calibrated
conversion between CDT's two RGB domains. Raw values and output values have
different purposes and are labelled accordingly.

## Verification and physical acceptance

Use `docs/HANDOVER.md` for the actual test results and exact package hash.
Synthetic tests can establish black preservation, control effects, spatial
response, bounds, persistence and freshness behavior. Read-only live input can
establish that CDT data arrive and produce finite calculated channel values.
Neither establishes the physical lamp color or brightness seen by the owner.

Physical acceptance needs the exact release package at a dark scene with no
local sources, a visible local light, a blocked light and a bright outdoor scene.
Compare raw input with mapped channel output, test camera rotation and change
one control at a time. Check Extra background light 0%, Ambient black threshold
and Ambient transition explicitly.
Record the chosen settings alongside actual lamp observations. Do not claim
pixel matching, physical brightness or complete environment illumination.
