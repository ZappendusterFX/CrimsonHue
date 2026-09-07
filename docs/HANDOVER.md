# Current checkpoint — CrimsonHue 0.1.0, 2026-09-07, Codex

First standalone Windows application implemented, packaged and opened for the
user's real-lamp acceptance. Product lives entirely in this repository. Neither
CrimsonDesertTelemetry nor diyHue sources/configuration were modified by this work.

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
