# Quiet-corridor movement response — 2026-11-03

## Decision and scope

Change Hue's local output smoothing, plus the independently demonstrated CDT
cache-admission defect found in the later lightning recording. The owner describes walking forward,
turning 180 degrees, returning and turning again in a quiet corridor, without
combat or moving scene lights. The existing five-second capture is sufficient
for an offline comparison. The owner then explicitly requested additional
five-second and 30-second recordings of their lightning-ball effect; those
passive recordings are separate evidence below. No repeated AN/AUS comparison,
settings change, installation or assistant-triggered lamp output was performed.

Do not identify a physical moving light or the owner's lantern from a list index.
The earlier asserted moving-source/cache root cause remains withdrawn. Physics
visibility changes are present, but the available recording does not prove that
individual collision results are wrong. Hue's old smoothing was active; this is
a stronger output response, not a repair of a bypassed smoothing setting.

## CDT regression audit

The initial corridor audit used CDT local commit `7e53f98` (public 2.2.6 plus
diagnostic documentation). Comparing 2.2.5 and 2.2.6 shows no change to the managed
visibility association, nine-ray classification, 500 ms freshness limit, query
cadence/budget or geometry handling. Today's native port changes the exact-build
addresses/profile and bounded diagnostics, not those rules.

Two historical cases are distinct:

- September's mass false-clear batches used the wrong natural query family
  `0x40004027`. Fix `2964014` restricted the query; the foreign profile is still
  rejected before capture and replay. Build 26361232 accepts its measured
  `0x40000017`; the previous build retains `0x40000016`.
- The moving-torch unknown/clear case was handled by the fail-closed HUD/visible
  feed in `4e8fe82`. It did not establish persistent source identity or remove all
  waiting-for-physics phases. Approximate source tracking must not be reintroduced
  as if it were a reliable visibility measurement.

Preserved integrated.3 evidence has 241 accepted natural templates using the
current profile and 8,751 completed nine-ray targets: 7,986 fully blocked,
652 fully clear and 113 partial. No mass-clear batch matching the old failure
appears. The corridor's 286 raw frames have 7–44 clear and 198–235 blocked records
globally; known results have consistent ages, counts and measurement provenance.
The existing targeted `--physics-visibility` regression passes. This is evidence
against that regression, not proof of material-aware optical accuracy. Cage/glass
limitations remain documented upstream.

The optional smoothed recording contains 13 `repeated-source-changed` rejections.
All coincide with sources entering/leaving CDT's 100 GU published radius as the
player moves during the same GPU capture: 25 added/removed records, all explained
by the radius crossing, with no common light-payload mutation. The old
visibility-metadata equality fix `087da44` is still present. These conservative
optional-feed rejections are separate from Hue's always-available raw input and
outside its 35 GU range. Switching Hue to that feed is not this solution. The
longer effect recording later revealed the separate admission defect below;
absence of a new-build regression does not mean there are no older defects.

## Implemented response

Keep raw `/v1/stream`, fresh-clear source selection, weights and spatial mapping.
Replace only the single exponential local output stage with two cascaded stages
in encoded RGB. Each stage's time constant is the displayed transition divided
by `2.146193220620583`; this preserves the old 63.2% step-response time.

The exact held-target update uses nonnegative common RGB weights. It introduces
no threshold, brightness hold, per-component attack/release bias, guessed source
identity or visibility override. It preserves target colors, converges without
overshoot and is independent of how a constant-input interval is split between
frames. Both states reset on feed loss; 0 ms updates both immediately. Tiny
positive durations avoid an infinity-times-zero calculation.

Ambient keeps its existing independent response/removal. Master and channel
limits still apply immediately after smoothing. Settings, presets, room geometry,
game-build handling and the shared panel protocol remain unchanged. The control
description now explains the displayed 63% response time.

## Recorded comparison

Evidence comes from CDT's preserved
`artifacts/light-research/hue-flicker-motion-20261103-go/` and Hue's six captured
UI states in `artifacts/hue-live-state/flicker-motion-20261103-go/`.
Replay reconstructs effective controls and wall/segment calibration from state 3:
Balanced 250/600 ms, range 35 GU, Point 200%, Spot 102%, Own 10%, Ambient 40%.
All 286 raw frames parse; 78 distinct native captures are present.

The offline harness under `artifacts/corridor-replay-20261103/` reuses the existing
source-balance replay's reconstruction and actual Core mapper. Baseline Core is
the immutable 1.0.1 binary (its smoothing is identical to installed 1.0.0).
It starts from black, excludes the first second, resamples latest published
envelopes at nominal 30 Hz, and uses the separate Ambient feed. Captured UI does
not expose imported channel brightness, so replay assumes 1 per channel. Ambient
publication time is approximated from its timestamp/age. This is not a recording
of actual sent packets or measured lamp brightness.

| Mean over eight calculated channels | Previous | Two-stage | Change |
|---|---:|---:|---:|
| Rapid-change measure: sum of RGB second-difference norms | 1.25435 | 0.42750 | −65.9% |
| Total variation: sum of RGB step norms | 1.67348 | 1.34321 | −19.7% |
| Hann-windowed RGB-vector RMS above 3 Hz | 0.009457 | 0.004605 | −51.3% |
| Same RMS, 7–15 Hz | 0.006070 | 0.001226 | −79.8% |
| Same RMS, 1–3 Hz | 0.025379 | 0.028068 | +10.6% |

Second difference is `rgb[n] - 2*rgb[n-1] + rgb[n-2]`; it emphasizes rapid
reversals. It is a computed comparison, not a perceptual flicker certification.
Four 30 Hz sampling phases give 64.6–70.2% lower second-difference totals. At
15/60 Hz the reductions are 36.6/67.1%; excluding 1.5 or 2 seconds instead gives
64.4/67.7% at 30 Hz. The improvement is not just a cold-start or sampling-phase
artifact. Slow turns can produce equally large or larger changes; the new filter
does not attempt to remove those scene transitions.

Within 208 adjacent pairs sharing a native capture, 43 change the actual mapped
local target when only visibility selection is exchanged under the same pose.
Local-only replay has much more rapid variation than Ambient-only replay. These
checks establish an input path for the fluctuations; counterfactual sums are not
an additive causal partition or evidence that the corresponding verdict is false.

The actual new compiled Core reproduces the analytical candidate for 150 frames
and eight channels: maximum component differences below `8e-16`, also checked
local-only. The exact packaged Core reproduces that compiled candidate with zero
component difference for the same combined/local-only runs. Reports, phase
results, frozen baseline/candidate DLLs and comparison receipts stay in the
ignored artifact folder; raw data is not committed.

## Short effects and tests

Synthetic Balanced/250 ms tests compare with the previous single-stage response:

| Visible pulse duration | New/old peak | Extra peak delay |
|---|---:|---:|
| 50 ms | about 86% | about 93 ms |
| 100 ms | about 93% | about 74 ms |
| 200 ms | about 102% | about 44 ms |

These synthetic pulses have a confirmed visible source. Owner-triggered live
effect recordings are evaluated separately below.
The new filter does soften/delay the shortest effects. It retains useful peaks
without globally increasing the selected response time, but cannot infer a
spell, fire or lightning identity from the output signal. Real combat/physical
lamp acceptance is not claimed.

All 131 Core tests pass: the 125 existing regressions plus six focused checks
covering matched response time/settling, 5/10 Hz visibility patterns and mean
preservation, 50/100/200 ms pulses, bounded color transitions, reset/zero/tiny
durations and regular/irregular timing. Existing mocked-stream checks retain
freshness clearing, independent Ambient removal and ownership/cleanup behavior.
The new 5/10 Hz tests compare with the old analytical response; their thresholds
reject the previous single-stage response.

## Owner-triggered lightning recordings

The owner reports several lightning balls spreading around the player and
explicitly triggers two additional captures through the existing bounded
`Capture-LightStreams.ps1` script. These are new live input observations, followed
by offline output calculation; no new package was installed or physically tested.

- Five seconds, 15:07:53.646–15:07:58.634 CET: 298 raw envelopes. Bright cool point
  groups are already present at the beginning and last about 1.9 seconds, followed
  by the end of the effect. The first onset is missing, so this capture cannot
  establish onset latency or the entire intended distribution.
- Thirty seconds, approximately 15:12:48–15:13:18 CET: 1,784 raw envelopes,
  456 native captures, 1,783 smoothed envelopes. Strong bursts occur near the
  beginning, 5 seconds and 9 seconds; effect tails end around 20 seconds, followed
  by roughly 10 seconds of quiet input. The first burst is already underway, but
  the later two have recorded preceding input and decay. No further timing retry
  is needed for these comparisons. The raw file is close to the recorder's
  128 MiB bound; do not assume a longer/more crowded capture will fit.

Despite the requested quiet pose, the player and camera move substantially in
the 30-second recording (about 21 and 29 GU maximum displacement, respectively;
camera forward spans about 137 degrees). Bright confirmed contributions occur
in all twelve camera-local 30-degree sectors over the recording. This is useful
spatial effect input, not proof that a stationary simultaneous 360-degree flash
was fully measured. Group centres do not locate each visible effect particle.

The existing credential-free UI reader confirms the same Balanced 250/600 ms
controls and captured room geometry after the five-second run. It reports sync
stopped at that later read; no physical output during either recording is inferred.

Actual packaged Core replay of the thirty seconds agrees with the analytical
candidate to below `8e-16` over 900 frames, eight channels and combined,
effect-only and without-effect comparisons. For the two later burst windows,
all channels with an old isolated blue-light pulse retain one with the new filter:

| Recorded burst | Per-channel new/old isolated blue peak | Encoded blue time integral |
|---|---:|---:|
| Around 5 s | 100.8–107.4% | 99.97–100.31% |
| Around 9 s | 101.3–106.5% | 99.37–101.55% |

Most peak times are unchanged at the simulated 30 Hz cadence; one channel per
burst peaks one update later. Across four 30 Hz sampling phases, peak ratios
range from 100.77–107.37% and finite-window integral ratios from 99.12–101.92%.
One near-saturated plateau's maximum moves earlier; a peak timestamp is not an
onset-latency measurement. This is a diagnostic isolation of strong point
contributions, not semantic spell detection or physical energy. Mixing and output
normalization are nonlinear: the full-mix excess differs, including one channel
with about 4.5% lower peak excess in the second burst. Short calculated local
target pulses can still be weaker: a 133 ms pulse on channel 6 loses about 13%
of peak, and a 33 ms pulse on channel 0 about 16%. These target pulses combine
source changes, visibility and spatial motion; they are not identified individual
lightning balls. Do not claim all short effects are unchanged.

The recorded large bursts remain pronounced without an added spell detector.
Raw intensity rises distinguish these particular bursts from the corridor, but
walking into range can also raise raw intensity. No fixed intensity threshold,
blue-color rule or game-specific effect label is introduced into production.

Private evidence: `artifacts/lightning-360-20261103-go/`,
`artifacts/lightning-360-20261103-30s-go/` and
`artifacts/lightning-replay-20261103/`. The thirty-second analysis and actual
package comparison are kept there. Replay uses the recorded CDT 2.2.6 verdicts;
it cannot manufacture rays that a corrected scheduler might have measured.

### Separate CDT admission defect

The longer run exposes current lights marked `outside-physics-budget` even when
the exact selected target set has at most 112 entries, below the 256-target cap.
All 1,303 such upstream observations correspond to selected targets; 1,206 are
within Hue's 35 GU range. They are not legitimate nearest-256 exclusions.

The managed client retains up to 512 positional entries for five seconds, then
refuses to allocate an entry for a new current target while those historical
entries fill the cache. Rapidly changing effect positions can therefore prevent
new measurements despite spare request capacity. This is an older bookkeeping
defect, not evidence that today's native query profile or collision verdict is
incorrect. The targeted upstream correction gives current targets cache space
and keeps all visibility/freshness/position checks; it does not turn unknown into
clear. A late response is now tied to its original cache entry and discarded if
that entry has been removed, rather than attaching to a replacement entry.
The actual managed client reproduces admission failure with 17 current targets
after 32 rounds of positional churn in under two seconds. The corrected code
passes that case, retained-anchor/measurement-expiry/budget-skip checks and the
late-response test. Restoring the old response lookup makes the late-response
test fail. All 79 CDT managed tests pass; native query behavior is unchanged.

Even after fixing admission, genuinely unmeasured short-lived light, native time
budget skips, blocked rays and expired results must remain unknown/blocked.
The strongest observed blue peak (about 38,667 raw units) does become clear after
about 31 ms in this recording, while other very short large peaks never receive a
fresh result. Raw HDR amplitude is not a direct Hue output amplitude because the
mapper bounds each source before spatial mixing. Filter attenuation and missing
input remain distinct.

## Final Hue package

The maintained `scripts/Publish-Asi.ps1 -Version 1.0.3` build passes, including
the exact packaged hidden-controller smoke and expanded/ZIP validators. The
package contains seven expected files under one root, with five application
files embedded in the ASI and no loose runtime, credentials or settings. The
31-control/shared-panel ABI is unchanged; no CDT renderer change or new renderer
acceptance is claimed. Build log: `artifacts/motion-response-1.0.3-package.log`.

Immutable package: `dist/CrimsonHue-1.0.3-asi-win-x64.zip`, 3,013,047 bytes.

- ZIP SHA256: `918EE899AACD444266F578DA80502EAFF73D66A81D5B5210985826589C97AB5F`
- ASI SHA256: `248044DFD035DFC954B034F2906B4BA3E48A0CD8722FDDE243DD95360DD2332F`
- Embedded Core SHA256: `FF39CE8077FA2DB7DB4E6D32E65D0F8D814F5CA406675CE6B71C7F2B5E7A0EAB`

The exact final Core was replayed against both recordings: 150 corridor frames
and 900 lightning frames, including combined/local-only and cold/settled-first
effect-isolation variants. Every compared output component is identical to the
already verified 1.0.2 filter. Receipt:
`artifacts/motion-response-1.0.3-replay/verification.json`.

The initial local 1.0.2 ZIP remains immutable, unpublished and superseded. Its
SHA256 is `B01DE30C059D314EF4CACAD287EC0CB422E7BDAC1943123D5E6AA324B0F47C7C`.
Version 1.0.3 retains the same smoothing code and updates packaged guidance to
the subsequently demonstrated CDT 2.2.7 correction; it does not change the
measured filter after the comparison.

Built and validated locally, not installed or published. Installation remains
exclusively the owner's responsibility. Use the new CDT 2.2.7 package for the
complete correction; old packages and the running installation are preserved.
AGENTS.md and CLAUDE.md remain byte-identical.
