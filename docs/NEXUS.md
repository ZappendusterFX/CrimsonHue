# Nexus listing copy — CrimsonHue 1.0.7

Nexus publishing stays with the owner. Use the same immutable
`CrimsonHue-1.0.7-asi-win-x64.zip` as the GitHub release. CDT, the ASI loader,
.NET runtimes, game files, saved settings and credentials are not included.

- GitHub release: https://github.com/ZappendusterFX/CrimsonHue/releases/tag/v1.0.7
- Full guide: https://github.com/ZappendusterFX/CrimsonHue/blob/v1.0.7/docs/USER-GUIDE.md
- Required CDT: https://github.com/ZappendusterFX/CrimsonDesertTelemetry/releases/tag/v2.2.9
- Category: Crimson Desert / Utilities

## Short fields

Counts include spaces and punctuation, exclude the code-block delimiters and
trailing newline. Version/file-name limits of 50 characters are documented in
CDT's existing Nexus publishing guide. The summary below fits a 350-character
budget; file description and short changelog each fit 255. These conservative
budgets are not a claim that every Nexus page field has the same limit.

| Field | Characters | Budget |
| --- | ---: | ---: |
| Title | 34 | — |
| Summary | 325 | 350 |
| File name | 16 | 50 |
| Version | 5 | 50 |
| File description | 238 | 255 |
| Short changelog | 246 | 255 |

### Title

```text
CrimsonHue - Spatial Room Lighting
```

### Summary

```text
Bring Crimson Desert's live engine lights to Hue Entertainment and optional OpenRGB devices. Includes spatial placement, an in-game mixer, configurable menu lighting, wall-washer calibration and separate ambient, point, spotlight and own-lantern controls. Requires CrimsonDesertTelemetry 2.2.9 and .NET 8 Desktop Runtime x64.
```

### File name

```text
CrimsonHue - ASI
```

### Version

```text
1.0.7
```

### File description

```text
Single-ASI CrimsonHue with configurable menu lighting and OpenRGB reset controls. Requires CrimsonDesertTelemetry 2.2.9, .NET 8 Desktop Runtime x64 and a Hue Entertainment area. Install both mods with the game closed. OpenRGB is optional.
```

### Short changelog

```text
Adds menu color/brightness controls, optional Gold preset, fresh-data resume blending and OpenRGB reset/disconnected-device cleanup. Existing settings are preserved. Requires CDT 2.2.9 for menu mode. The reported Skills-menu stutter is not fixed.
```

## Main description — ready-to-paste BBCode

```bbcode
[size=5][b]CrimsonHue - Spatial Room Lighting[/b][/size]

Turn Crimson Desert's live engine-light telemetry into spatial room lighting through Hue Entertainment and optional OpenRGB devices. Nearby sources follow their direction around your setup, including sources outside the game image, with a separate adjustable background layer.

CrimsonHue 1.0.7 runs as an ASI inside the game. CrimsonDesertTelemetry (CDT) 2.2.9 hosts its optional mixer in the existing in-game renderer. There is no separate CrimsonHue EXE or settings window. CDT remains a separate required download; it supplies telemetry, pause detection and the shared panel, while CrimsonHue controls the lights.

[size=4][b]Features[/b][/size]
[list]
[*]Guided bridge pairing and import of your existing Hue Entertainment area, channel positions and segmented lights.
[*]Recommended lighting with Overall brightness, Local light effects, Background light and Quick/Balanced/Calm response.
[*]Separate Point lights (0-200%), Spotlights (0-1000%) and Own lantern (0-100%) controls. Recommended starts the recognized own lantern at 10%.
[*]Continuous spherical source mapping, configurable distance fade and sharper Recommended zones: 30-degree source radius, 20% edge softness and directional focus 4.
[*]Optional Room calibration for individual wall-washer segments, including horizontal arrangements, plus an explicit two-second channel identification test.
[*]Optional OpenRGB output alongside Hue, with SDK device discovery, individual 3D positions, light coverage/background controls and an independent output limit. Reset saved OpenRGB settings or remove disconnected devices while stopped.
[*]Configurable Menu lighting with color picker, hex input, brightness and Save in the HUD. An optional Gold preset is available; the default remains warm white at 20%, and existing settings are preserved.
[*]Steady menu output while CDT confirms paused simulation and acquisition, followed by fresh-data blending over 500 ms on resume. Overall brightness and the OpenRGB limit still apply.
[*]31 numeric Advanced controls, raw telemetry beside calculated lamp output, and versioned presets with selective Output/Ambient/Color/Space import.
[*]Protected current-user bridge credentials, stream ownership checks, stale-input handling and restoration of captured static light states when cleanup succeeds.
[/list]

[size=4][b]Requirements[/b][/size]
[list]
[*]Windows 10/11 x64 and a Crimson Desert build supported by CDT. CDT owns game compatibility and build validation; CrimsonHue consumes its neutral API without a game-build gate.
[*][url=https://github.com/ZappendusterFX/CrimsonDesertTelemetry/releases/tag/v2.2.9]CrimsonDesertTelemetry 2.2.9[/url], installed separately with its documented .NET 8 ASP.NET Core Runtime x64 requirement. Older CDT endpoints can retain ordinary lighting but do not provide menu mode.
[*][url=https://dotnet.microsoft.com/en-us/download/dotnet/8.0].NET 8 Desktop Runtime x64[/url] for CrimsonHue. Plain .NET/ASP.NET runtimes and .NET 9/10 alone do not replace it.
[*]One compatible x64 ASI loader.
[*]A Hue-compatible HTTPS API v2 bridge with Entertainment support, color lights and an existing Entertainment area configured in the Hue app.
[*]Optional: OpenRGB with its local SDK server and compatible RGB devices. The Hue bridge and Entertainment area remain required; OpenRGB-only operation is not offered by this release.
[/list]

[size=4][b]Install and start[/b][/size]
1. Close the game and stop any old desktop CrimsonHue sync. Install the current CDT and CrimsonHue packages together through DMM, or follow each package's manual ASI-loader instructions.
2. CrimsonHue deploys one CrimsonHue.asi; its application dependencies are embedded. Keep all companions supplied by CDT. The CrimsonHue ZIP includes no loader or runtime installer.
3. Start the game. Ctrl+Shift+H shows or hides the Hue mixer while the game has focus. The shortcut can be changed in Setup.
4. In Setup, enter your bridge's own local IPv4 address, click Find bridge, press the bridge's link button, then Trust bridge & pair. Wait for HUE BRIDGE: CONNECTED and select your Entertainment area.
5. Start with Recommended, stop other sync apps, then click Start lighting sync. Loading the mod never starts physical output automatically. Demo preview never drives lamps.
6. X, Hide settings and the shortcut hide the panel while sync continues. Release any held game buttons before interacting with the HUD. Before leaving the game, press Stop and wait for the stopped status.

Existing settings and protected pairing stay under LocalAppData/CrimsonHue. Existing Custom tuning is preserved; Use Recommended keeps a separate copy of it. Mixer presets exclude bridge keys, addresses, area IDs and personal Room calibration. Do not share the complete local settings or secrets files.

[size=4][b]Menu lighting[/b][/size]
Open Lighting > Menu lighting to choose a color and brightness, then Save menu lighting. Reset restores warm white at 20%. Gold preset explicitly selects RGB 1/0.65/0.08 at 80%; it never changes saved values automatically. Overall brightness, channel balance and the OpenRGB output limit still apply.

CDT detects stopped simulation progress after approximately 500 ms and suspends new local-light, ambient and visibility acquisition. CrimsonHue keeps a steady color only while that pause confirmation is fresh. On resume, old capture data are discarded and fresh gameplay lighting blends back in over 500 ms. The output connections stay open. Missing/stale activity data uses the existing stale-input safety behavior; Demo never drives hardware.

[b]Menu lighting does not fix the reported Skills-menu stutter or repeated controller hover sounds.[/b] Those persisted in the owner's menu-mode test. Pause detection does not identify individual menu screens. CDT 2.2.9 avoids treating intentional capture pauses as light failures or announcing readiness on every menu resume.

[size=4][b]OpenRGB setup[/b][/size]
OpenRGB is disabled by default for new users. Start its local SDK server, normally localhost:6742, then enable OpenRGB in its mixer tab and choose Read devices. This reads the SDK inventory; it does not rescan hardware. Select devices and drag them in the 3D room, adjust Height, and right-drag to rotate the view. Exit other software writing the same RGB devices. Common Start/Stop controls both Hue and OpenRGB.

Each selected device receives one color for its configured position; keys and LEDs do not have separate room positions. Eligible RGB zones are used, keyboard matrix holes are skipped, gamepads use their Lightbar, and empty headers are not resized. Output limit starts at 25%, Light coverage at 90 degrees and Background light at 25% of the shared background amount. Selections and placements save locally by stable hardware identity, outside shareable mixer presets.

Reset OpenRGB settings disables OpenRGB, restores these defaults and current-device front positions, and removes saved disconnected entries. It preserves the SDK port and Hue setup. Remove disconnected devices only removes saved entries absent from a successful Read devices result on the current port, preserving current placement and response settings. Both require stopped lighting. Reset view changes only the viewing angle.

[size=4][b]Known limits[/b][/size]
This is an artistic lighting estimate from engine data, not screen sampling, pixel matching, lux or complete scene illumination. Spotlight cones, reflections and every emissive material are not simulated. Ambient uses CDT's relative sky/openness estimate and does not measure all indoor lighting.

Source visibility currently uses physics collision rays, not optical transparency. Glass or a collision shell around a visible cage lamp can exclude the source even when you can see the lamp or its light. This happens before the Point/Spot controls, so raising strength cannot restore a rejected source. Renderer-based optical visibility is not included in this release.

Point/spot are geometric kinds, not fire/blue-lamp categories, and grouping is independent of kind. Own-lantern recognition uses a conservative complete-pair signature in CDT metadata, not an engine ownership ID or a game-build gate. Incomplete or ambiguous input keeps ordinary spotlight strength.

The owner has used private builds with a real Hue Bridge, Play bars, wall washers and parallel OpenRGB, including menu lighting, and previously with diyHue/LEDVANCE. Synthetic controller, transport and GPU tests are separate from exact public-package installation/game/input acceptance and other hardware testing. 30 outgoing updates per second is a target, not a measured lamp refresh rate. SDK RGB values do not measure the emitted color or brightness of a device.

An OpenRGB failure leaves a healthy Hue stream running. Stop, resolve the SDK/server conflict and Start to retry. Previous modes and colors are restored while ownership and connectivity permit; another lighting app or unreachable hardware can prevent restoration.

The controller ends with the game's process. Crashes, forced termination or an unreachable bridge can prevent release/restoration. Stop before quitting. Hot unload/reload is unsupported; restart the game after changing installed modules.

[url=https://github.com/ZappendusterFX/CrimsonHue/blob/v1.0.7/docs/USER-GUIDE.md]Complete setup and tuning guide[/url] | [url=https://github.com/ZappendusterFX/CrimsonHue/releases/tag/v1.0.7]GitHub release[/url] | [url=https://github.com/ZappendusterFX/CrimsonHue]Source and issues[/url]

Independent project, not affiliated with Signify, Philips Hue or Pearl Abyss. Third-party notices are included in the download.
```

## Full changelog

Use the release notes in [releases/v1.0.7.md](releases/v1.0.7.md) when the field
allows a longer entry. The exact package digest and release validation belong to
the versioned release validation record and GitHub asset metadata.
