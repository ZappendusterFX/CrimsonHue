# Current checkpoint — CrimsonHue 0.1.1, 2026-09-07, Codex

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
