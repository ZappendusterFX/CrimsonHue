# CrimsonHue

A standalone Windows companion that turns Crimson Desert's live local-light
telemetry into spatial room lighting through Hue Entertainment.

**0.1.0 — early preview.** This is a source-driven lighting estimate. General
ambient light is planned upstream and will be integrated separately when its API
contract is available. This version consumes the existing local-light feed.

## Start

1. Install **CrimsonDesertTelemetry 2.0.0** with its server and rendered light
   capture enabled, following its own installation instructions. Start the game.
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
6. Adjust brightness, sensitivity and radius around the player. Stop other sync
   apps, then click **Start lighting sync**.
7. **Stop**, or close the window, to release the stream and restore previously read
   on/off, brightness and color states of the selected lights, if CrimsonHue still
   owns the stream. Running effects/dynamic scenes are not resumed.

**Demo preview only** animates synthetic data and disables physical output. Normal
preview uses live game data and the imported layout without claiming a stream.
Streaming never starts automatically on application launch.

## Requirements and scope

- Windows 10/11 x64.
- CrimsonDesertTelemetry 2.0.0, schema 1.4 rendered lights, on the same PC.
- A Hue-compatible bridge with HTTPS API v2, Entertainment DTLS 1.2/PSK on UDP 2100,
  color lights and an existing Entertainment area.
- Development target: diyHue. Physical Philips Hue bridges remain untested.
  Backend/lamp performance varies; 30 outgoing updates/s is a target, not a
  measured physical lamp refresh rate.

CrimsonHue consumes the telemetry service over WebSocket/JSON. It has no memory
reader, ASI, injection, screen capture or bundled game files. Users need the
running services, not the telemetry or diyHue source repositories.

## Mapping and availability

Only `lights.rendered` is used. Positions are transformed with the camera paired
to the light capture. Player distance supplies a soft radius cutoff, and camera
direction supplies a smooth weight for each Hue channel. Hue X points right, Y
towards the screen and Z up. The preview is top-down; cards show channel height,
which participates in the direction calculation.

Contributions are combined in linear RGB, compressed with a common HDR scale to
preserve channel ratios, then sRGB-encoded and brightness-limited. Imported Hue
brightness balance is applied where provided. Short configurable smoothing reduces
jitter. This is an artistic estimate, not game pixels, physical lux, measured room
distances or full scene lighting. Spotlight cone/occlusion is not simulated yet.

`sampleIndex` is never a persistent ID. Authored and rendered feeds are not added.
Gradient channel membership is preserved. The transport supports up to 160 channels
in one datagram; actual bridges and the Hue app may impose lower limits. There is
no additional hardcoded ten-lamp restriction in CrimsonHue.

Missing, malformed, loading, stopped, disconnected or stale data clears calculated
output. Rendered data must be no older than 500 ms, including transport/client
time; duplicate envelopes never refresh freshness. After one second without valid
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
the `dotnet run` argument separator to override. `--live` adds read-only checks
against the local telemetry and the development bridge; it never changes lamps.

The executable supports `--ui-smoke <absolute-png-path>` to render its actual WPF
window with synthetic data, without loading credentials or connecting to devices,
then exit. The publish script creates a standalone x64 executable and ZIP in
`dist/`, and refuses to overwrite existing versions.

## Distribution

Intended Nexus category: Crimson Desert / Utilities, as an external companion
requiring CrimsonDesertTelemetry. Extract the portable app separately from the
game; do not deploy its ZIP as game files with a mod manager. See `docs/NEXUS.md`
for the draft listing and `docs/HANDOVER.md` for measured verification status.

Independent project, not affiliated with Signify or Philips Hue. Third-party
license notices accompany the release in `THIRD-PARTY-NOTICES.md` and runtime files.
