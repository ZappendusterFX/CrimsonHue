# Release publication — 2026-09-28

GitHub repository: https://github.com/ZappendusterFX/CrimsonHue (public).
GitHub pre-release: https://github.com/ZappendusterFX/CrimsonHue/releases/tag/v0.3.2.
Tag `v0.3.2` points to `c34245c287d3d7301f991c885d3d56c0db0b3225`,
which includes `docs/USER-GUIDE.md` and README links. The immutable ZIP was
uploaded with 70,326,922 bytes; GitHub reports SHA-256
`6A1C4C5D39BB90136925A112439AA245B59348A71B5A63A85D7BAE3F6EE01DC6`,
matching the local file. GitHub release notes state prerequisites and link the
complete guide. CDT is separate and not packaged. Nexus publication belongs to
the owner; `docs/NEXUS.md` is the prepared listing copy. No Nexus upload by Codex.

# Current checkpoint — CrimsonHue 0.3.2, 2026-09-28, Codex

Owner found nearby fires behind the character lighting the front Hues while CH0
rear stayed dark. A read-only CDT capture confirmed the geometry: player
(-10606.647, 607.5385, -4421.96), camera
(-10612.477, 610.5437, -4423.1084), sources about 6.06–6.10 GU from the player.
Those sources are behind the player but in front of the third-person camera lens.
The owner explicitly chose an adjustable percentage between those two reference
points instead of declaring either one universally correct.

Space now exposes Direction origin, 0% raw player position to 100% paired camera
position, with 50% as the visible default. It interpolates all three coordinates
along their connecting line; there is no additional eye-height offset. Camera
right/forward/up still supplies orientation at every percentage. Distance fade
remains player-relative, and the strict boundaries/disc use the selected origin.
Mapper and RoomView share SourceDirection; preview status labels the percentage.
The origin has slider/numeric/reset/save support and defaults to 50% for older
settings that do not contain it. No existing numeric settings were rewritten.

CH0 is at imported room XYZ (+0.3129, -0.9982, +0.5053): rear-right, about 26 degrees
above horizontal. It is not a sky-only channel. Its height participates in the
finite disc test. In the recorded scene, origin 0% reaches CH0 with the rear-right
fire and leaves both front channels at exact local zero. At 100%, the same nearby
fires reach the fronts. At 50%, their combined side/height angle can miss the
60-degree disc around CH0, so the midpoint is not promised as a universal fix.
Origin 25% also reaches the rear in the recorded regression. Strict left/right
still excludes the rear-left fire from the only rear-right lamp.

Owner reports an active Off beyond of 15 GU; disk settings still said 20 GU at
inspection, so in-app unsaved settings must not be equated with saved ones. The
recorded scene regression explicitly uses 15 GU. Normal close should save the
owner's current choices before switching packages. LEDVANCE minimum brightness
was also suspected by the owner for tiny nonzero output; the existing Ambient
black threshold was recommended. No hardware dimming limit was measured and no
new output threshold was added in this release.

Verification: Release build zero warnings/errors; 71/71 non-live tests passed,
including exact 0/25/50/100% origin geometry, camera axes, endpoint persistence,
the recorded 15-GU scene, and camera translation independence at 0%. Read-only
live saved-layout mapping passed 72/72. With the saved 20-GU cutoff, origin 0%
gave CH0 local RGB approx (0.6000, 0.3541, 0.1832) and front local RGB zero.
At 100%, both front lamps had red output 0.6000. At 50%, fronts were zero and
CH0 had only a small contribution from farther sources (~0.00383 red). The
separate Ambient baseline was ~0.000823 per output channel. These are calculated
outputs, not physical lamp measurements. No physical stream was started.

Source and packaged WPF smoke runs exited 0, with six panels rendered; Space was
visually inspected. UI coverage includes all 28 numeric and two boolean settings,
and origin endpoint changes update mapping and preview text. All five ZIP entries
match package files. AGENTS.md and CLAUDE.md are byte-identical.

Package: `dist/CrimsonHue-0.3.2-win-x64/CrimsonHue.exe` (166969627 bytes),
`dist/CrimsonHue-0.3.2-win-x64.zip`, SHA-256
`6A1C4C5D39BB90136925A112439AA245B59348A71B5A63A85D7BAE3F6EE01DC6`.
Prior immutable packages and pre-existing codex_check_hdr.obj are preserved.
No credentials/settings/game files are packaged and no upload occurred.

Next: owner closes 0.3.1 normally, opens 0.3.2, and compares origin percentages
in the same scene. Camera position, player position and imported lamp height
are distinct factors; do not change Hue positions or invent a hardware limit.

# Previous checkpoint — CrimsonHue 0.3.1, 2026-09-28, Codex

Owner requires strict spatial separation: a source on one side must never light
lamps on the opposite side. Off-screen sources remain relevant to the room. The
owner clarified that source enlargement must be a 2D circle, not a world-space
sphere. The old exponential distribution had an infinite tail: at the reported
left brazier (roughly 22 degrees left), focus 2 assigned ~66% angular weight to
the front-right channel. That was broad directional mixing, not a reflection.

The mapper now applies visible Strict left / right and Strict front / rear
switches, both On by default (including old settings). Opposite-sign camera/room
X or Y contributions are exactly zero before normalization. A source with no
matching lamp is dropped, never redirected across a boundary. Positions exactly
on a center line belong to both adjacent halves. Room direction offset rotates
the source first. Height still participates in matching, with no height boundary.

Each source has a finite circular footprint in the plane normal to its camera
bearing. Channel rays intersect that 2D plane; Source disc radius controls its
angular radius (60 degrees default, 1–89 range), and Source disc soft edge controls
the outer fading fraction (25% default, 0–100%). The footprint is clipped at
enabled side boundaries. Outside the circle is zero; its edge is applied after
angular compensation so compensation cannot revive it. Off-screen and rear
sources use the same mapping without a screen-frustum filter. All four new
controls are visible in Space with reset/persistence. Preview shows boundaries
and their states. Raw CDT input and the independently controlled Ambient layer
keep their meanings; local spill does not stand in for reflected illumination.

Strict boundaries also bypass the old channel EMA, including the legacy feed,
so a camera turn cannot retain color on the wrong side. The immediate path now
assigns the target RGB directly, avoiding floating-point residue from an EMA
formula with alpha one. This restriction is labelled beside Legacy smoothing.

Verification: Release build zero warnings/errors; 69/69 non-live tests passed.
Tests cover the recorded brazier/three-lamp layout, exact black across quadrants
through camera rotations, unmatched layouts, center lines, independent switches,
offset, finite circular footprint in both dimensions, radius/edge adjustment,
off-screen coverage, Ambient independence and persistence of all new settings.
Read-only live saved-layout mapping passed 70/70. At the owner's forest scene,
40 clear sources and raw Ambient W=0.000968932153 produced local-only RGB zero
for CH0 rear and CH1 front-right; CH2 front-left was approximately
(0.6000, 0.3548, 0.1834). Combined right/rear output equalled Ambient alone,
approximately (0.000742932, 0.000742932, 0.000742932), at the saved 60% cap.
A simulated 180-degree camera turn moved the fire contribution to CH0 rear.
These are computed outputs, not observations of physical lamp appearance.

Source and packaged WPF smoke runs exited 0. All 27 numeric controls and both
boolean controls are checked for UI coverage and live state changes; six panels
were rendered. All five ZIP entries match package files. AGENTS.md and CLAUDE.md
are byte-identical. No physical output session was started or taken over.

Package: `dist/CrimsonHue-0.3.1-win-x64/CrimsonHue.exe` (166969115 bytes),
`dist/CrimsonHue-0.3.1-win-x64.zip`, SHA-256
`F74658AC6316E947ECF8689069D28080840E00AF73F11098E91493543D16FAFC`.
All previous packages and pre-existing untracked codex_check_hdr.obj are preserved.
No credentials/settings/game files are packaged and no upload occurred.

Next: owner switches from the still-running 0.3.0 to this exact 0.3.1 package,
then starts sync and checks the left brazier and camera turns. Strict switches
start enabled; source radius and edge can be tuned in Space. Tiny genuine Ambient
remains independently adjustable with the existing Ambient black threshold.

# Previous checkpoint — CrimsonHue 0.3.0, 2026-09-28, Codex

Owner rejected 0.2.2's artificial night fill: live CDT had zero confirmed-clear
sources and practically zero Ambient, yet that version generated ~0.188 neutral
RGB at their saved 60% cap. The owner explicitly requires visible controls and
raw input, rather than hardcoded tuning changes. An initial 0.2.3 fix was never
packaged; this feature release supersedes that work.

The WPF UI now has Output, Ambient, Color, Space and Setup tabs. All 25 mapping
parameters have a slider, numeric entry, explanation and individual reset. Each
tab can be reset; Save settings stores the current choices. The Space tab has
Fade starts, Off beyond and the requested Distance falloff exponent, with a
curve preview drawn from the mapper's actual distance function. Camera offset
also rotates source dots consistently with lamp assignment.

The fixed night floor, special orange/fire recoloring, doubled direction
exponent and silent saved-brightness migration are removed. Extra background
light and Ambient black threshold default to zero. Hue shift, saturation, RGB
balance, gamma, Ambient exposure/reference/mix/transition/tint, local contrast,
distance curve and angular compensation are exposed. Technical conversion and
bounded-output formulas are documented in docs/AMBIENT-INTEGRATION.md.

CDT raw input shows unmodified local and sky working RGB, sky visibility and
the local mean, plus the raw confirmed-clear source RGB/positions and counts.
Freeze readout holds only the display for inspection/copying; incoming telemetry
and physical output continue normally. Channel cards show mapped output RGB.
Demo preview stays synthetic and no longer mixes live Ambient into its preview.
No CDT or diyHue implementation was changed.

Verification: Release build zero warnings/errors; 62/62 non-live tests pass,
including neutral preservation of orange source ratios, exact black at zero,
all controls, distance falloff boundaries, persistence and ownership cleanup.
Read-only saved-layout live mapping passed 63/63 with zero clear sources:
Ambient W=0.000247726451 produced RGB≈0.000189962018 on every channel at the
preserved 60% limit (~0.019% output component). Tiny raw input is retained by
default; the visible Ambient black threshold can suppress it if desired. This
is computed output, not physical lamp evidence. No physical session was started.

Source and packaged WPF smoke runs exited 0. The UI gate verifies every
MappingSettings double has a visible control, each control changes mapping,
numeric entry rejects invalid values, and distance limits remain consistent.
Output/Ambient/Color/Space/Setup/raw panels were rendered; raw and mixer views
were visually inspected. All five ZIP entries match package files.

Package: `dist/CrimsonHue-0.3.0-win-x64/CrimsonHue.exe` (166958875 bytes),
`dist/CrimsonHue-0.3.0-win-x64.zip`, SHA-256
`71F4AD244421AE97F2D7C140778AC1AB540DEF80D40E23BBA10FD6EF272DD18C`.
Earlier packages and the pre-existing untracked codex_check_hdr.obj are preserved.
No credentials/settings/game files are packaged. AGENTS.md and CLAUDE.md are
byte-identical. No public upload occurred.

Next: owner tunes 0.3.0 in-app, especially Distance falloff and Ambient black
threshold, while comparing raw values and mapped output. Actual physical lamp
appearance remains owner-observed evidence and must not be inferred from tests.

# Previous checkpoint — CrimsonHue 0.2.2, 2026-09-28, Codex

Owner tested 0.2.1 in game and found the lamps too dark, with weak camera-turn
response and an orangered lamp beside a visibly yellow fire. Ingame HUD and
read-only CDT API agreed on nearby linear engine-light RGB around
`(1.582, 0.636, 0.173)` and `(1.122, 0.340, 0.085)`: the source data are
orange/red even though the rendered flame looks yellower. No CDT code or API
was changed. The correction here is artistic, not evidence of a telemetry bug.

0.2.2 keeps the clear-only upstream filter. Each source now focuses on the
strongest imported Hue channel and uses twice the previous angular exponent;
this makes camera yaw materially change a sparse room layout. Orange-family
source RGB raises green smoothly toward 0.8 of red; pure red, blue, green and
near-white stay unchanged. The valid Ambient signal now supplies
`0.08 × presence + 0.4 × level` neutral linear fill, while local contribution
retains `1 - 0.3 × level` contrast. Missing/stale Ambient fades back to the
local-only mapper. The untouched old 60% brightness preset migrates once to
85%; custom brightness and later deliberate 60% settings are preserved.

Verification: Release app build zero warnings/errors; 57/57 non-live tests pass,
including fire hue, three-channel camera yaw, dark Ambient fill, old preset
migration and preservation of deliberate brightness choices. Read-only live
saved-layout mapping passed 58/58: 50 clear sources, Ambient W≈0.001 and
computed RGB for actual/yaw+180°/no-local cases. At that moment rear channel
red moved 0.43→0.85 on the simulated turn; one front channel moved 0.85→0.37;
no-local neutral output was about 0.27 per component. These are calculated
colors, not physical lamp observations. The packaged WPF demo smoke exited 0,
its preview rendered, and all five ZIP entries match package files. The real
area was active during the read-only inspection, so no 0.2.2 physical run was
attempted and no active stream was interrupted.

Package: `dist/CrimsonHue-0.2.2-win-x64/CrimsonHue.exe` (166917915 bytes),
`dist/CrimsonHue-0.2.2-win-x64.zip`, SHA-256
`812F6FA6E59F762E86AF22A998DE3C1CA11330CB90F0712331BFD387C19EB07C`.
Prior packages are preserved. No credentials/settings/game files are in the ZIP
and no public upload occurred. AGENTS.md and CLAUDE.md remain byte-identical.

Next: owner stops the 0.2.1 stream, opens the exact 0.2.2 package and checks
the left fire lamp's hue, brightness, and response to camera turning at the same
game point. Also compare a bright outdoor scene and blocked source. Record
actual visible lamp behavior separately from the computed values.

# Current checkpoint — CrimsonHue 0.2.1, 2026-09-28, Codex

The owner observed all three lamps turning red together outdoors in bright
daylight with 0.1.3, matching that version's local-only mapping. Screenshot/CDT
diagnostics showed available sky and camera visibility; live read-only Ambient
values gave a local working estimate around 7–11. CrimsonHue now subscribes to
CDT's separate `/v1/ambient/stream` on the configured local port. It requires
fresh sky, measured camera sky visibility and a valid local product, expiring
sky and visibility independently after 1500 ms including client time. Missing
Ambient is explicitly labelled and fades to the local-only fallback.

The working RGB is AP1-like and not calibrated to local-light or display RGB.
0.2.1 therefore uses it only as a relative brightness signal: a smoothed neutral
room baseline increases and confirmed local-light contrast decreases as camera-
local Ambient grows. The new Ambient influence control (default 1×, 0 disables)
adjusts this artistic response. No time-of-day or invented directional Ambient
was added. The exact model and limits are in `docs/AMBIENT-INTEGRATION.md`.

Verification: Release build zero warnings/errors; 55/55 non-live tests pass,
including bright-day/dark-interior mapping, immediate blocked-light removal,
independent sky/visibility expiry, fallback and legacy-settings migration.
Read-only live Bridge, all-around lights and Ambient WebSockets passed 58/58:
40 progressing light captures and nine progressing sky captures in four seconds.
Before packaging, a bounded source-built physical run started the free area,
sent nonzero near-neutral mapped colors, stopped and restored states; it did not
establish what the owner saw on the lamps. Source and packaged UI demo smoke
runs exited 0 with no physical output. All five ZIP entries match package files.

Package: `dist/CrimsonHue-0.2.1-win-x64/CrimsonHue.exe` (166916379 bytes),
`dist/CrimsonHue-0.2.1-win-x64.zip`, SHA-256
`FDEE30A69E2F60DA38264815A9803665539D7AD5BEE0097FFC8190B2090B71D5`.
The earlier local 0.2.0 package remains preserved but is superseded because its
normal missing-visibility status was misleading. No credentials/settings/game
files are in the ZIP and no public upload occurred.

Next: owner tests the exact 0.2.1 package in the same bright outdoor scene and
reports lamp colors/brightness. Then compare a dark interior and night, including
a visible and blocked source; adjust the response based on real-lamp evidence.

# Previous checkpoint — CrimsonHue 0.1.3, 2026-09-28, Codex

CrimsonHue now consumes CDT's schema 1.6 all-around `lights.upstream` input from
the neutral `/v1/stream` API. It requires the input's capture sequence, frame and
timestamp to match the rendered sample, then uses that sample's paired camera.
Each input source must have `sourceVisibility.status=clear`, method
`physics-ray-fan`, and a measurement no older than 500 ms including client time.
Blocked, unknown, missing and stale verdicts produce no Hue contribution.
When upstream is advertised but unavailable, output clears instead of switching
to a partial rendered view. With upstream disabled, schema 1.6 applies the same
clear-only rule to rendered sources. Schema 1.4/1.5 retains the old rendered path.
The confirmed-visible mapper bypasses the old channel-level EMA, which otherwise
retained color after a light became blocked or unknown; legacy smoothing remains.
Demo preview remains synthetic and never sends physical output. No telemetry code
was added here; CDT and its packages were unchanged.

Verification: Release app build had zero warnings/errors; 52/52 non-live tests
passed, including paired-capture, behind-camera, blocked/unknown, unavailable and
visibility-expiry checks and immediate removal of hidden color. A read-only CDT
WebSocket run during live gameplay passed 53/53 with 40 progressing all-around
captures and up to six confirmed-clear
sources. This proves input parsing and finite mapping, not real-lamp appearance or
visibility accuracy. After diyHue was restarted, a read-only bridge plus CDT run
passed 54/54 and confirmed the saved area layout. The area was initially active;
the owner stopped the other stream before physical output was attempted.
An explicit, bounded `--live-lamps` run then started Entertainment, observed
nonzero mapped color, stopped after five seconds, released the area and completed
the previous-light-state restoration path (53/53 tests passed). This proves the
CrimsonHue DTLS/bridge control path sent data; actual lamp appearance and
visible/blocked behavior still need the owner's observation.
Source and packaged WPF demo smoke modes exited 0 with no bridge connection or
lamp output. The ZIP's five entries match the on-disk package hashes. Existing
0.1.0 and 0.1.1 packages remain untouched. An earlier local 0.1.2 package from
this work is retained but superseded because it still smoothed hidden color.

Package: `dist/CrimsonHue-0.1.3-win-x64/CrimsonHue.exe` (166902555 bytes),
`dist/CrimsonHue-0.1.3-win-x64.zip`, SHA-256
`9109E559B445980E1F9619F84CD3AF6BD43A2239A7F31490CBE0C55D310BBF02`.
No credentials, settings, ASI or game files are included; no public upload.
CDT's separate Ambient API exists, but its working RGB has no calibrated relation
to local-light RGB or Hue output, so ambient mixing remains pending.

Next: ask the owner what the three lamps visibly did during the bounded stream.
Then check visible and blocked lights, including a source behind the camera, in
the packaged UI before claiming physical acceptance.

# Previous checkpoint — CrimsonHue 0.1.1, 2026-09-07, Codex

The new distance-fade release is built, tested, packaged and open for user testing.
Product lives entirely in this repository. Telemetry remains unchanged; its ambient
feed is still being developed. Historical 0.1.0 and diyHue work is retained below.

## Distance fade — 0.1.1

User reports real lamps reacting to distant in-game light colors and asks for
adjustable fade distances. There was already a radius falloff, but its attenuation
preceded HDR compression. Extremely bright sources could therefore saturate output
despite a tiny distance weight. The new mapping bounds each source's HDR intensity
before distance/direction weights. Weighted linear sources are summed and only
overflow is scaled down; there is no post-fade amplification. Multiple overlapping
sources still add together. This is an artistic model, not physical photometry.

- **Fade starts / Off beyond** sliders control a player-relative full-strength
  zone and a smooth fade to zero contribution. The same squared-smoothstep curve
  applies approaching/leaving. Existing smoothing releases residual old output.
- Both controls affect preview and active sync immediately. WPF events keep the
  interval valid; settings are saved on Start/normal close. Stored `Radius` remains
  the end distance for backward compatibility; new `FadeStart` defaults to zero.
  Existing user cutoff 35 was retained. Distances are game units, not known metres.
- Preview dots use the same distance envelope; live capture status reports the
  number of contributions inside the cutoff.
- Future ambient-relative contrast requirements are in
  `docs/AMBIENT-INTEGRATION.md`. Actual environment brightness should suppress
  ordinary local effects in bright daylight but retain them in dark daytime
  interiors. No ambient field, fake time-of-day rule or upstream modification was
  introduced. Ambient mixing is not implemented until its contract is available.

Verification for 0.1.1:

- Release build: zero warnings/errors. **46/46 non-live tests pass**, including ten
  new distance/HDR/boundary/player-vs-camera/smoothing/settings regressions and the
  existing real loopback OpenSSL DTLS, mock HTTPS/WS and DPAPI checks.
- Actual WPF slider event self-tests cover both crossed bounds and control limits
  in isolated smoke mode. Source-build and packaged EXE smoke runs exit 0. Render
  inspected at `artifacts/ui-distance-fade-packaged.png`; both fade controls and
  their explanation are visible at the default window size. Smoke uses synthetic
  data and does not load credentials or contact devices.
- Optional `--live` run: **46/47 passed**. Bridge HTTPS probe succeeded; zero live
  frames arrived. A read-only OS check found no listener on port 27311 at that time,
  so current live-input and real-lamp appearance acceptance remain outstanding.
  Do not mistake earlier 0.1.0 live evidence below for 0.1.1 lamp acceptance.
- Old package process 28084 was closed normally. New package opened as PID 31784;
  path, window handle/title and responsiveness verified. Protected credential file
  hash stayed unchanged and saved area selection was retained. No new physical
  sync was started; the user must press Start when telemetry is available.

Package: `dist/CrimsonHue-0.1.1-win-x64/CrimsonHue.exe`, 166899483 bytes.
ZIP `dist/CrimsonHue-0.1.1-win-x64.zip`, 68180244 bytes.
SHA256 `6FDBD7D93A3AFA25AC8591F333E051509DF3524203BFFE827FD07311FC92073C`.
All five ZIP entries match on-disk package hashes; no settings, secrets or game
files are included. The old 0.1.0 ZIP hash remains unchanged. No public upload.

Next: user adjusts both distances and walks towards/away from local sources in the
game. Record real-lamp results separately; calibration and future ambient-relative
mixing still require live evidence. Preserve both immutable packages.

## Resolved — diyHue placement save/recovery (2026-09-07)

User authorized fixing the neighboring diyHue project. The running container's
two affected Python files matched the old checkout byte-for-byte. Its log showed
the exact ignored Hue-app PUT, confirming the cause (no longer only an inference).
The repair now applies validated v2 placements, saves groups synchronously and
publishes the updated layout. Twelve isolated placement tests pass; the key
regression fails against the original container image.

The development container was gracefully restarted with the two fixed files.
Its original, still-inactive layout was checked before replaying the exact discarded
user request. Corrected positions were verified through API and disk, then verified
again after a second graceful restart. The paired CrimsonHue diagnostic independently
read these values over pinned HTTPS:

| Channel | Light | X | Y | Z |
| --- | --- | ---: | ---: | ---: |
| 0 | LEDVANCE lamp_001 | 0.31291532926158294 | -0.9982087856556487 | 0.5052717571578222 |
| 1 | LEDVANCE lamp_002 | 0.6081610035675666 | 1 | -0.3940484143872305 |
| 2 | LEDVANCE lamp_003 | -0.9011899756747147 | 1 | -0.8866089323712685 |

This gives rear, front-right and front-left respectively, matching the screenshot's
floor arrangement. Pairings, client keys, certificate and lamp backend settings
were checked unchanged against the ignored local backup. No physical output was
sent. The open CrimsonHue window needs **Refresh areas** to replace its cached
old layout; no CrimsonHue code or package change was required.

Implementation/deployment details and recreate-image caveat are in
`C:\DEV\DiyHue\docs\ENTERTAINMENT-PLACEMENT-FIX.md`.
The local fixed image is `diyhue-ledvance-tuya:placement-fix-20260907`; the existing
container was patched in place. Old images/packages are unchanged. User asked
whether upstream contribution is possible; answered yes via their existing fork
and a focused PR. The user subsequently authorized submission:
[diyHue PR #1119](https://github.com/diyhue/diyHue/pull/1119) is open against `dev`.
Fork branch `fabianviol:fix/entertainment-placement-v2`, commit
`e631fd767518e5f89b7079e2d353e87535527d8b`, contains one focused commit/four files.
It was prepared in `C:\DEV\DiyHue-pr-entertainment-placement` from upstream dev
`daf917799850dfee1bf94719756f80dfface83a0`, without unrelated fork changes.

The dev port uses that branch's existing save API and includes a device-ID lookup
fix in the Entertainment configuration reload path. Twelve tests pass against the
PR source; independent PUT and real Config.load_config regressions both fail on
the unmodified dev base. This submission did not replace the local working bridge
with dev code. Merge/maintainer review remains pending; no monitoring was scheduled.

## Follow-up — Hue app placement mismatch (2026-09-07)

User's Hue screenshot shows lamp_003 left of the monitor, lamp_002 right of it,
and lamp_001 behind the seat. User confirmed the arrangement was saved with Done.
An authenticated, certificate-pinned **GET-only** query of the paired bridge's
area "Spieltisch" instead returned the following raw channel positions:

| Channel | Light | X | Y | Z |
| --- | --- | ---: | ---: | ---: |
| 0 | LEDVANCE lamp_001 | 0.5404710514918292 | -1 | 0.5052717571578196 |
| 1 | LEDVANCE lamp_002 | 0.22516372352075176 | 1 | -0.3940484143872304 |
| 2 | LEDVANCE lamp_003 | -0.3793345270304169 | -1 | 0.7054737741448802 |

The area was inactive. These agree with diyHue's on-disk group configuration, but
put lamp_003 behind the viewer instead of next to the screen. CrimsonHue imports
them verbatim. Its convention (+X right, +Y front, +Z up) agrees with the primary
[HueApi coordinate implementation](https://github.com/michielpost/Q42.HueApi/blob/master/src/HueApi/Models/HuePosition.cs).
No axes were flipped and no physical output was sent during this investigation.

Concrete defect in the neighboring source:
`C:\DEV\DiyHue\BridgeEmulator\flaskUI\v2restapi.py`,
`ClipV2ResourceId.put`, entertainment_configuration branch (line 634), handles
only action start/stop. A PUT with locations/service_locations does nothing but
still returns the resource as a successful response. The POST branch does import
positions. This is a strong explanation for edits appearing saved in the Hue app
while the bridge retains the area's original positions. No request capture from
the phone or deployed-source comparison was performed, so the exact phone request
path remains unverified. A fix belongs in diyHue, not a compensating mirror here;
request user authorization before editing/deploying that neighboring project.

Added `--inspect-layout` to the test executable for repeatable, sanitized, read-only
diagnostics using the paired account's DPAPI store, plus a six-cardinal-direction
regression covering front/back and height independently. **36/36 non-live tests
pass** after these additions (the earlier 36-test run below included `--live`).
No new app package was needed; immutable 0.1.0 remains unchanged. Physical-lamp
acceptance and the upstream ambient-light feed remain pending.

## Result

- WPF desktop UI: HTTPS bridge identity probe, link-button pairing requesting both
  keys, explicit certificate trust, current-user DPAPI storage, area discovery,
  imported positions/channels/names, top-down room preview and synthetic demo.
- Native managed DTLS 1.2 PSK with BouncyCastle 2.7.0, AES-128-GCM-SHA256, HueStream
  v2 RGB packets. No external OpenSSL executable is needed by the application.
- WebSocket reader: fragmented/full messages, 16 MiB bound, schema 1.4/additive 1.x,
  capabilities/axes/basis/state checks, monotonic and source-timestamp freshness,
  duplicate rejection, reconnect/reset and no historical light identities.
- Renderer contributions only, paired-camera directions, player-relative radius,
  soft spatial weighting, ratio-preserving HDR compression, sRGB output, brightness
  cap and smoothing. Hue channel IDs/segments and available brightness balance kept.
- Start/Stop session ownership checks, bounded handshake, stale output clearing,
  release/normal-state restoration after one second without fresh telemetry,
  automatic resumption while armed, explicit restart on bridge failure/takeover.
  Selected lights' on/off, brightness, color/temperature and gradient points are
  restored where available. Running scenes/effects are not resumed.

## Evidence

- Release builds completed with zero warnings/errors.
- **36/36 tests pass**, including real OpenSSL DTLS interoperability, HTTPS
  certificate pinning and link-button pairing against a loopback mock, Windows
  DPAPI, partial WebSocket messages over 64 KiB, cleanup/failure/takeover and Stop
  during bridge activation. Tests use synthetic credentials only.
- Real read-only check: development bridge `192.168.2.109` reports DiyHue Bridge and
  responds over HTTPS. Native application probe succeeds with a certificate.
- Real game WebSocket control: 40 distinct envelopes and 40 distinct rendered
  captures during four seconds. First run up to 44 contributions; later run up to
  19. All parsed/mapped without invalid output. This proves a progressing input
  path, not complete source coverage or accepted real-lamp appearance.
- Actual WPF render inspected at `artifacts/ui-packaged.png` (synthetic demo).
  Packaged executable smoke mode exits 0. Five ZIP files match disk hashes; no
  secrets, settings, ASI or game files included.
- Interactive package started as PID 28084; window title/handle and responsiveness
  verified. Local settings/DPAPI files were subsequently created at 21:30 CEST,
  consistent with the user pairing through the UI. Their contents were not read.
  User explicitly confirmed: "Gekoppelt, Bereich wird angezeigt". Actual bridge
  pairing and Entertainment area selection therefore pass user acceptance.
  Physical lamp output/Start-Stop appearance acceptance remains pending.

## Package

`dist/CrimsonHue-0.1.0-win-x64/CrimsonHue.exe` (self-contained, 166898459 bytes).
ZIP `dist/CrimsonHue-0.1.0-win-x64.zip`, 70292385 bytes.
SHA256 `A7C759FBB47A58127F801AB753A9C70EA2EB3DA9BAA8BC24EB6F540C61F2396E`.
Keep versioned packages immutable. `scripts/Publish.ps1` refuses existing outputs.
No remote repository, GitHub release or Nexus upload was created. Nexus listing
draft is `docs/NEXUS.md`. Source-license/branding choices remain publication work.

## Known limits / next step

User is adding **general ambient lighting upstream in CrimsonDesertTelemetry**.
Do not invent its contract. Consume it separately once its fields, freshness and
color semantics are defined. Current output has only local light contributions.

No depth/occlusion, spotlight attenuation, physical falloff/units, persistent IDs,
sun/sky/emissive completeness or exposure normalization. Camera direction determines
room mapping; player position determines proximity. Hue placement is normalized,
not measured room metres. Renderer culling limits rear visibility. The consumer's
160-channel datagram cap is not a claim the Hue app or physical bridge supports it.
Real Hue hardware and large areas remain untested. Physical backend refresh rate
is not established by the 30/s outbound target. Crash/unreachable bridge restoration
cannot be guaranteed; detected cleanup failures are reported instead of hidden.

Next: user verifies the paired Entertainment area and runs Start/Stop while moving
near game lights. Record visual acceptance and any actionable mismatch; preserve
the current package and issue a new version if code changes are needed.
