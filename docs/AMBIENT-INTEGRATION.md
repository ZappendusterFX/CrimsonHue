# Ambient and mapping controls — CrimsonHue 0.3.1

CrimsonHue consumes CDT's independent `/v1/ambient/stream` and local-light feed.
CDT owns capture and the neutral contracts. CrimsonHue shows the received values
separately from its calculated lamp output and exposes the visual adjustments in
the app. The producer contract is
`C:\DEV\CrimsonDesertTelemetry\docs\AMBIENT_STREAM.md`.

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
**Freeze readout** holds the raw display for inspection and copying. It does not
pause incoming telemetry, the mapper or the lamp stream.

Working RGB is AP1-like and is not calibrated to display RGB or local-light RGB.
CrimsonHue uses the mean of its nonnegative channels as relative level `W`.
The raw channels are displayed for inspection; they are not copied into lamp RGB.
No clock-time or daylight flag is inferred. Bright sky with low camera openness
can produce a dark local product. Ambient has no invented direction; sun disk,
material response and indoor global illumination are not modeled.

## Exposed Ambient response

All constants in the visual response below are visible settings with sliders and
numeric inputs. Their defaults and ranges are listed in the README controls table.
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
confirmed-clear visibility checks reach the mapper. Color controls apply to all
local-light colors: multiply linear RGB by Red balance, Green balance and Blue
balance, then apply Local hue shift and Local saturation. The default values
preserve source ratios.
There is no fire classification or automatic orange-to-yellow conversion.

Light sensitivity bounds each source's HDR peak with
`1 - exp(-peak × sensitivity)`, retaining a common RGB scale before spatial fade.
Player-relative distance remains full strength through Fade starts and reaches
zero at Off beyond. With normalized interval `t`, the weight is
`(1 - t² × (3 - 2t)) ^ falloff`. The **Distance falloff** setting supplies the
exponent; the default is 2. The curve preview plots remaining contribution in
percent against game-unit distance and updates with all three distance controls.

**Room direction offset** rotates the source direction in camera-local coordinates.
**Strict left / right** and **Strict front / rear** default to On, including when
loading older settings. They reject channels whose X or Y sign, respectively,
opposes the source's sign. These hard boundaries act before normalization.
An exact zero coordinate belongs to both adjoining halves. Height is weighted
without an additional top/bottom boundary. No matching channel means no local
contribution, with no fallback to another side. The preview draws the active
center lines and labels both switches' state.

Each source is expanded into a bounded **2D circular footprint** on the plane
normal to its camera-relative direction at unit depth. A channel ray with angle
`theta` to that direction intersects this plane at radius `tan(theta)`. Divide
that radius by `tan(Source disc radius)` to obtain normalized radius `r`.
Rays with nonpositive dot product or `r >= 1` contribute zero. The default radius
is 60 degrees, adjustable from 1 to 89 degrees; this is apparent angular size,
not a game-world sphere. All directions, including off-screen and rear sources,
use the same rule. Distance fade remains separate.

**Source disc soft edge** defaults to 25% of the disc's radius. Its inner circle
(`r <= 1 - softness`) has coverage one; the outer ring uses smoothstep down to
zero at the edge. Zero softness gives a hard edge. Both strict boundaries clip
this footprint regardless of its size or softness. This footprint is a mapping
choice, not a reflection estimate or a claim about the renderer's light volume.

Within the surviving footprint, imported channel positions supply angular weight
`exp(Directional focus × (dot - 1))`. **Strongest-channel compensation** blends
this absolute weight with the weight divided by the strongest allowed angular
weight. Its range is 0–100%. Multiply by disc coverage *after* compensation so
the soft edge cannot be boosted back to full intensity, even with a single lamp.
No blocked side or outside-disc channel participates in normalization.

**Local light mix**, divided by 100, scales the summed contributions before
overflow normalization.
Ambient fill and the Ambient-adjusted local contribution are added in linear
output space, and overflow is normalized again. The result is sRGB-encoded, then
**Output gamma** applies `encoded ^ (1 / gamma)`. Gamma one is neutral;
larger values brighten intermediate output and smaller values darken it. The
**Maximum brightness** and imported channel balance cap the final output.
Color controls, gain and gamma all preserve zero input as zero output.

Legacy smoothing remains an exposed control for older renderer-only input with
both strict boundaries off. It is bypassed for confirmed-clear input or either
enabled boundary so sources cannot leave residual color after becoming hidden
or crossing a side. This restriction is labelled in the UI. Ambient retains its
separate transition control and remains non-directional; disabling local spill
does not suppress genuine Ambient input or an explicitly configured background.

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
