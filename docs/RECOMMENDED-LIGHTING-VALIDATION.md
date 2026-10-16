# Recommended lighting profile — 2026-10-16

## Product behavior

The normal CDT-hosted HUD exposes Overall brightness, Local light effects,
Background light, and Quick/Balanced/Calm response. Room and Setup remain separate;
the 28 detailed mapping controls, presets and raw inputs are in Advanced.

New installations start Recommended. Existing saved mappings remain Custom until
explicit opt-in. Recommended resolves a coherent neutral mapping: zero black
threshold/floor, gamma and RGB gains 1, neutral color, continuous spherical
weighting, coordinated transitions (local/background: 100/400, 250/600, 500/1000 ms).
It preserves the current brightness cap on opt-in and personal camera alignment.
The original custom mapping is stored separately and restored exactly on request.
Partial preset imports change selected custom groups only, then activate Custom.
Legacy advanced commands and tab resets also use the preserved custom base, so
older compatible renderers cannot replace unrelated settings with profile values.
Room layout, bridge credentials and Entertainment channel identities stay separate.

This is a deterministic starting profile, not scene or lamp calibration. It does
not invent a visible minimum from Hue's normal-dimming min_dim_level capability,
normalize darkness into daylight, recognize the owner's lantern, or add game time
to CDT's contract. Missing local telemetry still clears both layers; missing
Ambient alone removes its layer. The HUD explains known blocking conditions and
labels per-lamp output as calculated commands rather than measured illumination.

## Live context before this change

The owner reported noon (12:05) and clear weather. A bounded passive read of the
running Hue .asi.16 immutable UI snapshot is saved locally in
`artifacts/hue-live-state/noon-1205.json`. It reported Streaming, 20,941 frames sent,
zero local-cleared frames and zero Ambient-missing frames; working Ambient level
18.3828603, sky visibility 0.4274210, local RGB 15.7323/17.6368/21.7795, and fill
0.395092 of its configured maximum 0.4 (98.77%). All six calculated channel peaks
were about 66.15% encoded command range. The additional Ambient mix limits the
linear layer; this record does not establish a missing noon sample or a physical
lamp cutoff. Earlier dawn and saved-settings replays are different observations.

Clock time is optional context for plausibility, not a substitute for local
illumination. CDT's current sky-times-camera-visibility input does not measure all
indoor/global illumination or displayed pixel exposure. No physical measurement,
game input, screen capture, process write, or real settings change was made here.

## Validation and packages

- Core suite: **110/110 passed**, including eight new profile/diagnostic groups,
  existing spatial/segmented-light/freshness checks and loopback DTLS. The first
  sandbox run encountered temporary-file permissions and loopback OpenSSL errors;
  the unchanged suite passed outside that restriction. Evidence:
  `artifacts/recommended-profile-tests-final.log`.
- App and test project Release builds pass with zero warnings/errors.
- The exact packaged controller smoke checks simple commands, invalid-value
  rejection, complete Custom restoration, neutral recommended colors, brightness
  cap, no physical demo output and process-preserving shutdown. Isolated controller
  startup also checks partial preset import and legacy set/reset preservation.
- Shared native rendering/input checks and final package hashes are recorded below.

The installed game and its active configuration were not upgraded by this work.
Physical profile/brightness/cursor acceptance remains pending. The separate
intermittent stream-stop cause and reliable own-lantern identification remain open.
The earlier Hue .asi.17 intermediate package is preserved; use final .asi.18.

Hue **0.4.0-asi.18**, headless ASI with five embedded application files:

- ZIP: `dist/CrimsonHue-0.4.0-asi.18-asi-win-x64.zip`
- ZIP SHA-256: `022FB7504C5BF53AB336D0E1F3B602918AD89C8C630BB3F0B08AA86B149DDE06`
- ASI SHA-256: `96C651CEE778C17A0452132B1720E9C500F3B83B205B9F6F3AA6834C34275511`
- Maintained `Publish-Asi.ps1` and `Test-AsiPackage.ps1` passed on the exact
  expanded folder and ZIP: seven files under one `CrimsonHue/` root; no loose
  runtimes, executables, settings or credentials. The exact packaged ASI passed
  controller smoke. Log: `artifacts/recommended-profile-package-final.log`.

Matching CDT **2.2.5-huepanel.8**, production, research OFF, native build 25477059:

- ZIP: `artifacts/cdt-shared-overlay/artifacts/mod-manager/CrimsonDesertTelemetry-v2.2.5-huepanel.8-ModManagers.zip`
- ZIP SHA-256: `7CF3B41B43C48CE75058BEF88AF5F975EC55D65A79D84BE9C066F73F8FCB75F0`
- ASI SHA-256: `92B93E9D6D495AB05155FCD891F29B4DE95BFA3988A64BB8D24806632B6772B8`
- Maintained package builder/validator passed on production staging and ZIP.
- **13/13 focused native input/overlay checks passed**. Six hidden D3D12 WARP
  runs against the exact packaged Hue .asi.18 passed: Lighting, Custom, Advanced,
  Setup, Room and raw input. Each read back 748,434 visible pixels. Screenshots
  were inspected; normal controls fit without scrolling. These are synthetic
  renderer/input results, not evidence from the owner's game or lamps.
- Evidence in the CDT worktree: `docs/HUE_LIGHTING_UI.md`,
  `artifacts/simple-lighting-final/`, `artifacts/simple-lighting-package.log`.
- Maintained panel and CDT's copied source match SHA-256
  `3BB3124E230F33F0C1FCEE6282E00CA4B4C486B1BE92FC0AB0CA802D004B0471`.

Upgrade both packages with the game closed. Neither package was installed here.
