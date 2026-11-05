# Nexus listing copy — CrimsonHue 2.0.0

Prepared for manual Nexus publication. Use the same CrimsonHue-2.0.0-asi-win-x64.zip as GitHub and pair it with CDT 2.2.10.

## Short fields

Counts include spaces and punctuation, exclude the code-block delimiters and
trailing newline. Version/file-name limits of 50 characters are documented in
CDT's existing Nexus publishing guide. The summary below fits a 350-character
budget; file description and short changelog each fit 255. These conservative
budgets are not a claim that every Nexus page field has the same limit.

| Field | Characters | Budget |
| --- | ---: | ---: |
| Title | 34 | — |
| Summary | 292 | 350 |
| File name | 16 | 50 |
| Version | 5 | 50 |
| File description | 220 | 255 |
| Short changelog | 252 | 255 |

### Title

```text
CrimsonHue - Spatial Room Lighting
```

### Summary

```text
Spatial room lighting from Crimson Desert engine data through independent Hue Entertainment, OpenRGB and compatible Tuya Wi-Fi 3.5 lamps. Includes in-game setup, device placement, menu lighting and ambient/local controls. Requires CrimsonDesertTelemetry 2.2.10 and .NET 8 Desktop Runtime x64.
```

### File name

```text
CrimsonHue - ASI
```

### Version

```text
2.0.0
```

### File description

```text
CrimsonHue 2.0.0 with independent Hue, OpenRGB and Tuya Wi-Fi 3.5 outputs. Requires CDT 2.2.10 and .NET 8 Desktop Runtime x64. Hue hardware is optional for OpenRGB or Tuya. Install the matching pair with the game closed.
```

### Short changelog

```text
Adds independent Hue/OpenRGB/Tuya selection, Tuya 3.5 spatial and menu lighting, protected local setup and bounded per-lamp output. OpenRGB-only needs no Hue bridge. Prior-state restoration is conditional. The controller Skills-menu issue remains open.
```

## Main description — BBCode

```bbcode
[size=5][b]CrimsonHue - Spatial Room Lighting[/b][/size]

[b]CrimsonHue 2.0.0[/b] supports three independent lighting outputs. Use any combination of Philips Hue Entertainment, OpenRGB and compatible Tuya Wi-Fi 3.5 RGB lamps.

Turn Crimson Desert's live engine-light telemetry into spatial room lighting through independently selected Hue Entertainment, OpenRGB and compatible Tuya Wi-Fi 3.5 RGB devices. Nearby sources follow their direction around your setup, including sources outside the game image, with a separate adjustable background layer.

CrimsonHue runs as an ASI inside the game. CDT hosts its mixer in the existing renderer; no separate CrimsonHue process is needed. Tuya credentials are entered in a separate Windows dialog. CDT is a required separate package and supplies telemetry, pause detection and the shared panel, while CrimsonHue controls the lights.

[size=4][b]Features[/b][/size]
[list]
[*]Independent output selection in Setup: Hue, OpenRGB, Tuya or any combination. No output is selected on a fresh setup; a saved Hue pairing preserves Hue opt-in. One failed output does not cancel healthy outputs.
[*]Guided bridge pairing and import of your existing Hue Entertainment area, channel positions and segmented lights.
[*]Recommended lighting with Overall brightness, Local light effects, Background light and Quick/Balanced/Calm response.
[*]Separate Point lights (0-200%), Spotlights (0-1000%) and Own lantern (0-100%) controls. Recommended starts the recognized own lantern at 10%.
[*]Continuous spherical source mapping, configurable distance fade and sharper Recommended zones: 30-degree source radius, 20% edge softness and directional focus 4.
[*]Optional Room calibration for individual wall-washer segments, including horizontal arrangements, plus an explicit two-second channel identification test.
[*]OpenRGB output with SDK device discovery, individual 3D positions, light coverage/background controls and an independent output limit. Works without Hue hardware. Reset saved settings or remove disconnected devices while stopped.
[*]Direct Tuya Wi-Fi 3.5 RGB output with individual room positions, protected local configuration, read-only TCP reachability check, and per-lamp workers that retain only the latest desired color.
[*]Configurable Menu lighting with color picker, hex input, brightness and Save in the HUD. An optional Gold preset is available; the default remains warm white at 20%, and existing settings are preserved.
[*]Steady menu output while CDT confirms paused simulation and acquisition, followed by fresh-data blending over 500 ms on resume. Overall brightness and the OpenRGB limit still apply.
[*]31 numeric Advanced controls, raw telemetry beside calculated lamp output, and versioned presets with selective Output/Ambient/Color/Space import.
[*]Protected current-user bridge credentials, stream ownership checks, stale-input handling and restoration of captured static light states when cleanup succeeds.
[/list]

[size=4][b]Requirements[/b][/size]
[list]
[*]Windows 10/11 x64 and a Crimson Desert build supported by CDT. CDT owns game compatibility and build validation; CrimsonHue consumes its neutral API without a game-build gate.
[*]CDT 2.2.10, installed separately with .NET 8 ASP.NET Core Runtime x64.
[*][url=https://dotnet.microsoft.com/en-us/download/dotnet/8.0].NET 8 Desktop Runtime x64[/url] for CrimsonHue. Plain .NET/ASP.NET runtimes and .NET 9/10 alone do not replace it.
[*]One compatible x64 ASI loader.
[*]For Hue only: a Hue-compatible HTTPS API v2 bridge with Entertainment support, color lights and an existing Entertainment area configured in the Hue app.
[*]For OpenRGB: its local SDK server and compatible RGB devices. Hue hardware is not required.
[*]For Tuya: compatible Wi-Fi protocol 3.5 RGB lamps, their local host/IP, device IDs and local keys. Protocol 3.3/3.4, Zigbee and Bluetooth are not supported by this backend.
[/list]

[size=4][b]Install and start[/b][/size]
1. Close the game and stop any old desktop CrimsonHue sync. Install the current CDT and CrimsonHue packages together through DMM, or follow each package's manual ASI-loader instructions.
2. CrimsonHue deploys one CrimsonHue.asi; its application dependencies are embedded. Keep all companions supplied by CDT. The CrimsonHue ZIP includes no loader or runtime installer.
3. Start the game. Ctrl+Shift+H shows or hides the Hue mixer while the game has focus. The shortcut can be changed in Setup.
4. In Setup, select your outputs. Configure Hue pairing/Entertainment area only if Hue is enabled. Use OpenRGB's Read devices or Tuya LAN's Add device for the other outputs.
5. Start with Recommended, stop other sync apps, then click Start lighting sync. Loading the mod never starts physical output automatically. Demo preview never drives lamps.
6. X, Hide settings and the shortcut hide the panel while sync continues. Release any held game buttons before interacting with the HUD. Before leaving the game, press Stop and wait for the stopped status.

Existing settings and protected pairing stay under LocalAppData/CrimsonHue. Existing Custom tuning is preserved; Use Recommended keeps a separate copy of it. Tuya layout and keys are atomically saved together in the Windows current-user DPAPI-protected tuya.secrets bundle; keys never enter the shared HUD. Mixer presets exclude output connection data, credentials and personal Room calibration. Do not share local settings or secrets files.

[size=4][b]Menu lighting[/b][/size]
Open Lighting > Menu lighting to choose a color and brightness, then Save menu lighting. Reset restores warm white at 20%. Gold preset explicitly selects RGB 1/0.65/0.08 at 80%; it never changes saved values automatically. Overall brightness, channel balance and the OpenRGB output limit still apply.

CDT detects stopped simulation progress after approximately 500 ms and suspends new local-light, ambient and visibility acquisition. CrimsonHue keeps a steady color only while that pause confirmation is fresh. On resume, old capture data are discarded and fresh gameplay lighting blends back in over 500 ms. The output connections stay open. Missing/stale activity data uses the existing stale-input safety behavior; Demo never drives hardware.

[b]Menu lighting does not fix the reported Skills-menu stutter or repeated controller hover sounds.[/b] Those persisted in the owner's earlier menu-mode test. Pause detection does not identify individual menu screens. The quiet pause/resume notifications introduced in CDT 2.2.9 remain included.

[size=4][b]OpenRGB setup[/b][/size]
OpenRGB is disabled by default for new users. Start its local SDK server, normally localhost:6742, then enable OpenRGB in its mixer tab and choose Read devices. This reads the SDK inventory; it does not rescan hardware. Select devices and drag them in the 3D room, adjust Height, and right-drag to rotate the view. Exit other software writing the same RGB devices. Common Start/Stop controls the configured selected outputs.

Each selected device receives one color for its configured position; keys and LEDs do not have separate room positions. Eligible RGB zones are used, keyboard matrix holes are skipped, gamepads use their Lightbar, and empty headers are not resized. Output limit starts at 25%, Light coverage at 90 degrees and Background light at 25% of the shared background amount. Selections and placements save locally by stable hardware identity, outside shareable mixer presets.

Reset OpenRGB settings disables OpenRGB, restores these defaults and current-device front positions, and removes saved disconnected entries. It preserves the SDK port and Hue setup. Remove disconnected devices only removes saved entries absent from a successful Read devices result on the current port, preserving current placement and response settings. Both require stopped lighting. Reset view changes only the viewing angle.

[size=4][b]Tuya LAN setup and limits[/b][/size]
In Tuya LAN, Add/Edit opens a Windows dialog with a password input for the local key. Saving does not change the lamp. Default profile: TCP 6668; power DPS 20, mode 21, HSV color 24, white brightness 22, temperature 23 and scene 25. RGB uses color mode and a 12-hex-digit HSV value without white-brightness DPS. These defaults do not establish compatibility with every Tuya product. No cloud login is used.

Check connection tests TCP reachability only; it does not authenticate the key or prove lamp support. Place each device in the 3D room. Tuya's output cap starts at 25%, coverage at 90 degrees and background at 25% of shared fill. Stop before changing connections or device selections. Position and tuning remain adjustable while running.

Each lamp retains one replaceable desired color, sends changed colors no faster than every 300 ms, and backs off 2.5 seconds after failure. This does not measure physical refresh rate. Stop restores a supported prior state only when a fresh query confirms the lamp still matches this output's commands. If prior state or ownership cannot be confirmed, or the device is unreachable, the last state may remain. Read the per-device status rather than assuming every Stop restores or turns off a lamp.

[size=4][b]Known limits[/b][/size]
This is an artistic lighting estimate from engine data, not screen sampling, pixel matching, lux or complete scene illumination. Spotlight cones, reflections and every emissive material are not simulated. Ambient uses CDT's relative sky/openness estimate and does not measure all indoor lighting.

Source visibility currently uses physics collision rays, not optical transparency. Glass or a collision shell around a visible cage lamp can exclude the source even when you can see the lamp or its light. This happens before the Point/Spot controls, so raising strength cannot restore a rejected source. Renderer-based optical visibility is not included in this release.

Point/spot are geometric kinds, not fire/blue-lamp categories, and grouping is independent of kind. Own-lantern recognition uses a conservative complete-pair signature in CDT metadata, not an engine ownership ID or a game-build gate. Incomplete or ambiguous input keeps ordinary spotlight strength.

Earlier owner use with a real Hue Bridge, Play bars, wall washers, OpenRGB and diyHue/LEDVANCE is separate from this new direct Tuya backend. No live Tuya lamp acceptance is claimed. Synthetic controller, transport and GPU tests do not establish installed-game or physical-device acceptance. The Hue 30-update-per-second target is not a measured lamp refresh rate and does not apply to Tuya's 300-ms minimum interval.

A failed output leaves healthy outputs running. Stop, resolve its connection/configuration issue and Start to retry a stopped output. Previous modes and colors are restored only where ownership and connectivity permit; another lighting app, unsupported state query or unreachable hardware can prevent restoration.

The controller ends with the game's process. Crashes, forced termination or an unreachable bridge can prevent release/restoration. Stop before quitting. Hot unload/reload is unsupported; restart the game after changing installed modules.

[url=https://github.com/ZappendusterFX/CrimsonHue/blob/main/docs/USER-GUIDE.md]Setup and device limits[/url] | [url=https://github.com/ZappendusterFX/CrimsonHue/releases]Published downloads[/url] | [url=https://github.com/ZappendusterFX/CrimsonHue]Source and issues[/url]

Independent project, not affiliated with Signify, Philips Hue or Pearl Abyss. Third-party notices are included in the download.
```

## Full changelog

See the [CrimsonHue 2.0.0 release notes](releases/v2.0.0.md).
