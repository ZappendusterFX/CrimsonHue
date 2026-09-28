# CrimsonHue

A standalone Windows companion that turns Crimson Desert's live local-light
telemetry into spatial room lighting through Hue Entertainment.

**0.2.2 — early preview.** This is a source-driven lighting estimate. With the
current CDT API, CrimsonHue uses the all-around ManyLights input, emits only
lights with a fresh confirmed-clear physics visibility result, and uses CDT's
separate camera-local Ambient estimate for neutral room fill. Bright surroundings
still soften local colors, but nearby lights retain a visible effect. Orange
engine lights receive an artistic amber correction because the game's displayed
fire can look yellower than its source RGB. Neither feed is calibrated to display
color; the mapping remains adjustable.

## Start

1. Install **CrimsonDesertTelemetry 2.2.1** with its server, rendered and upstream
   ManyLights capture, and source visibility enabled. Start the game.
2. Extract the complete CrimsonHue ZIP anywhere and run `CrimsonHue.exe`.
   The package includes its .NET runtime. No administrator rights or installation
   into the game directory are needed.
3. Enter your bridge's local IPv4 address and click **Find bridge**. The preset
   `192.168.2.109` is the development setup; change it to your own address.
4. Check the displayed bridge identity. Press the bridge's link button (for diyHue,
   in its web UI), then click **Trust bridge & pair** within 30 seconds. Repeat the
   link-button press if the pairing window expires. Both the application key and
   Entertainment client key are requested automatically.
5. Select an existing **Entertainment area**. Create and position lights in the
   Hue app first, then refresh. CrimsonHue preserves the existing layout/channels.
6. Adjust brightness, light sensitivity, Ambient influence and the two distance
   fade controls described below. Stop other sync apps, then click **Start lighting sync**.
7. **Stop**, or close the window, to release the stream and restore previously read
   on/off, brightness and color states of the selected lights, if CrimsonHue still
   owns the stream. Running effects/dynamic scenes are not resumed.

**Demo preview only** animates synthetic data and disables physical output. Normal
preview uses live game data and the imported layout without claiming a stream.
Streaming never starts automatically on application launch.

## Requirements and scope

- Windows 10/11 x64.
- CrimsonDesertTelemetry 2.2.1, schema 1.6, on the same PC for all-around,
  confirmed-visible lights. Older schema 1.4/1.5 installations still use the
  original rendered-only feed without physics visibility filtering.
- A Hue-compatible bridge with HTTPS API v2, Entertainment DTLS 1.2/PSK on UDP 2100,
  color lights and an existing Entertainment area.
- Development target: diyHue. Physical Philips Hue bridges remain untested.
  Backend/lamp performance varies; 30 outgoing updates/s is a target, not a
  measured physical lamp refresh rate.

CrimsonHue consumes the telemetry service over WebSocket/JSON. It has no memory
reader, ASI, injection, screen capture or bundled game files. Users need the
running services, not the telemetry or diyHue source repositories.

## Mapping and availability

For schema 1.6, `lights.upstream` provides the all-around input, including sources
behind the camera. CrimsonHue requires its capture sequence, frame and timestamp
to match `lights.rendered`, then uses the rendered capture's paired camera. Only
sources with `sourceVisibility.status=clear`, method `physics-ray-fan` and a fresh
measurement reach the mapper. Blocked, unknown, missing or stale verdicts do not
light the room. If CDT marks the upstream feed unavailable, CrimsonHue clears
output instead of switching silently to view-filtered sources. If upstream capture
is disabled, schema 1.6 can use the rendered feed with the same clear-only rule.
Older schema 1.4/1.5 uses the original rendered-only behavior.

Player distance supplies a configurable fade, and camera direction supplies a
smooth weight for each Hue channel. Each source is normalized against its
strongest imported channel, sharpening camera rotation in sparse room layouts.
Hue X points right, Y towards the screen and
Z up. The preview is top-down; cards show channel height, which participates in
the direction calculation. A clear physics ray is a sampled geometry verdict,
not a guarantee of optical visibility through every material or at every instant.

Each source's HDR intensity is compressed with a common RGB scale before applying
distance and direction weights, so high source intensity cannot undo that source's
fade. Orange-family source RGB is shifted toward amber; pure red and other hues
retain their source ratios. Weighted contributions are combined in linear RGB; only sums above the
output range are scaled down, preserving RGB ratios. Output is then sRGB-encoded
and brightness-limited. Imported Hue brightness balance is applied where provided.
Short configurable smoothing reduces jitter on the legacy rendered feed. The
confirmed-visible mode updates channel colors immediately so a previous clear
light cannot linger after becoming blocked or unknown. This is an artistic estimate, not
game pixels, physical lux, measured room distances or full scene lighting.
Spotlight cones are not simulated. CDT supplies the separate source visibility
decision; CrimsonHue does not perform its own occlusion test.

### Distance fade

- **Fade starts:** full distance strength up to this distance from the player.
- **Off beyond:** no new contribution at or beyond this distance.
- Between both values, strength falls smoothly to zero. Approaching a source uses
  the same curve in reverse. Smoothing can briefly fade out the previous output
  after crossing the cutoff.

Distances are **game units, not confirmed metres**. For example, start 5 / end 15
means full distance strength through 5, then a fade ending at 15; these example
values are not a calibration for the game. The controls update both preview and
active sync immediately, keep start below end, and are saved on Start or normal
window close. The preview hides out-of-range source dots and reports an in-range
contribution count for live input.

Existing 0.1.0 settings retain their radius as **Off beyond**, with **Fade starts**
at zero. New setups default to 0 / 35. Mapping is intentionally different from
0.1.0: very bright distant sources now fade reliably instead of saturating the
post-falloff HDR compressor. Multiple overlapping sources still add together.

### Ambient influence

CrimsonHue reads CDT's separate `/v1/ambient/stream` on the same loopback port.
It requires a fresh global sky sample, fresh measured camera sky visibility and
the available local product estimate. The three working RGB channels provide a
**relative brightness proxy**, not display color. CrimsonHue turns that proxy
into a neutral room baseline and moderately reduces local-light contrast as the measured
surroundings brighten. A valid dark Ambient estimate also adds a small neutral
floor, keeping the room visible without inventing sky color. The default **Ambient influence** is
1×; 0 disables this mapping, while higher values react more strongly. Ambient
changes fade over about 0.4 seconds. This is a tunable visual model, not a claim
that CDT exposes physical lux or pixel-accurate room illumination.

Sky and local visibility expire independently after 1500 ms, including client
time. On missing/stale Ambient, the neutral baseline fades away and the app falls
back to the existing local-light mapping; the status line names that fallback.
Invalid or missing local-light telemetry still clears output and releases the
Entertainment stream as before. See `docs/AMBIENT-INTEGRATION.md` for limits and
acceptance checks.

The new default maximum brightness is 85%. An untouched 0.2.1 preset at 60%
is upgraded once; custom values, including a deliberate 60% choice in 0.2.2,
remain unchanged. The brightness slider still caps output at its displayed value.

`sampleIndex` is never a persistent ID. Authored and rendered feeds are not added.
Gradient channel membership is preserved. The transport supports up to 160 channels
in one datagram; actual bridges and the Hue app may impose lower limits. There is
no additional hardcoded ten-lamp restriction in CrimsonHue.

Missing, malformed, loading, stopped, disconnected or stale data clears calculated
output. Light captures and clear visibility measurements each expire after 500 ms,
including transport/client time; duplicate envelopes never refresh freshness.
After one second without valid
input, the app releases the stream and restores normal states. While still armed,
it can resume when data returns and the bridge is free. A bridge error or another
app's takeover ends the session and requires a new Start. Hard crashes or an
unreachable bridge can prevent restoration; detected cleanup failures appear in
the UI. Renderer culling limits rear coverage; missing sources do not prove OFF.

## Settings

`%LOCALAPPDATA%\CrimsonHue\settings.json` stores preferences. `bridge.secrets`
stores the keys and trusted certificate fingerprint, encrypted with Windows DPAPI
for the current account. Neither file ships in releases. Authenticated requests
pin the certificate accepted during pairing; Find and pair again if it changes.
The initial identity probe sends no credentials. Bridges must use private IPv4
and HTTPS. HTTP is allowed only on loopback for automated tests.

Telemetry defaults to `ws://127.0.0.1:27311/v1/stream`; Advanced settings allow
another local port. Only loopback addresses are accepted. Host restarts reconnect
automatically and reset sequence tracking. Additive schema 1.x fields are tolerated.

## Build and verify

Use a .NET 8 or 9 SDK on Windows:

```powershell
dotnet build src/CrimsonHue.App -c Release
dotnet run --project tests/CrimsonHue.Tests -c Release
powershell -ExecutionPolicy Bypass -File scripts/Publish.ps1
```

The tests need OpenSSL for real DTLS interoperability. By default they use Git for
Windows' `C:\Program Files\Git\usr\bin\openssl.exe`; pass `--openssl <path>` after
the `dotnet run` argument separator to override. `--live-telemetry` adds a read-only
CDT light WebSocket control, `--live-bridge` probes the development bridge, and
`--live` runs those plus the Ambient WebSocket control. None changes lamps.

`--live-ambient` adds a read-only Ambient WebSocket control. `--live-mapping`
reads the protected saved area and fresh CDT streams, then prints calculated
channel RGB for the actual camera, a simulated 180° turn and no local lights;
it never starts an Entertainment stream. `--live-lamps` is a
separate, explicitly requested physical test. It reads the
paired current-user credentials and saved area, refuses to take over any active
Entertainment stream, sends a bounded five-second session at 25% maximum
brightness using fresh Ambient and local lights, then stops and restores the
previously read light states. It does not save settings. Use it only while the
game and paired bridge are available.

`--inspect-layout` instead reads the paired user's protected credentials and prints
only area names, channel names/IDs and raw Hue XYZ positions. Run it as the same
Windows user who paired the app. It performs GET requests only, does not start a
stream or change settings, and does not print keys or full API responses. Use this
to compare the bridge's saved layout with the Hue app before changing mapping axes.

The executable supports `--ui-smoke <absolute-png-path>` to render its actual WPF
window with synthetic data, exercise the distance-control bounds, without loading
credentials or connecting to devices, then exit. The publish script creates a
standalone x64 executable and ZIP in
`dist/`, and refuses to overwrite existing versions.

## Distribution

Intended Nexus category: Crimson Desert / Utilities, as an external companion
requiring CrimsonDesertTelemetry. Extract the portable app separately from the
game; do not deploy its ZIP as game files with a mod manager. See `docs/NEXUS.md`
for the draft listing and `docs/HANDOVER.md` for measured verification status.

Independent project, not affiliated with Signify or Philips Hue. Third-party
license notices accompany the release in `THIRD-PARTY-NOTICES.md` and runtime files.
