# CrimsonHue

A Windows ASI plugin that turns Crimson Desert's live local-light
telemetry into spatial room lighting through Hue Entertainment.

**Branch preview: 0.4.0-asi.12.** CDT hosts the interactive Hue mixer in its own
D3D12 renderer. `CrimsonHue.asi` is the headless lamp controller and installs no
graphics hooks. There is no separate settings window or process. The ZIP contains
one ASI and documentation; application dependencies
are embedded, and users install **.NET 8 Desktop Runtime x64** separately.
See [ASI guide](docs/ASI.md) and [verification checkpoint](docs/HANDOVER.md).
Use the matching **CDT 2.2.5-huepanel.5** preview; public CDT 2.2.4 lacks the panel.
The Hue panel uses CDT's compact body-text size and blue/cyan palette. Setup
guides the link-button pairing and confirms CONNECTED only after authenticated
area loading; a matching saved pairing is reused. Physical output remains explicit.
This preview has synthetic native/GPU verification; real game/input and DMM
acceptance remain pending. The linked
0.3.3 release is the earlier desktop EXE.

**Downloads and guides:** [CrimsonHue releases](https://github.com/ZappendusterFX/CrimsonHue/releases) ·
[complete setup and tuning guide](docs/USER-GUIDE.md) ·
[required CrimsonDesertTelemetry releases](https://github.com/ZappendusterFX/CrimsonDesertTelemetry/releases).
CrimsonDesertTelemetry (CDT) is a separate game mod and is **not included** in
the CrimsonHue ZIP. Install CDT separately alongside CrimsonHue using your
existing ASI loader; CrimsonHue still consumes only CDT's neutral local API.

**0.3.3 — early preview.** **Export preset** writes a versioned, readable JSON
file containing only the 30 mixer controls. **Import preset** previews the
differences and lets users apply Output, Ambient, Color and Space independently.
Space is off by default because it depends on the room. Import saves the chosen
values; bridge address, Entertainment area, telemetry address and protected
credentials stay local. See [Sharing presets](docs/USER-GUIDE.md#sharing-presets).

Since 0.3.2, **Direction origin** blends the room's game-space
reference point between the player (0%) and camera lens (100%), starting at 50%.
Viewing axes always follow the paired camera. This lets you choose where nearby
sources belong around a third-person view. Local sources use a bounded 2D disc
around their direction from that origin. Strict left/right and front/rear boundaries are on by
default: an enlarged source cannot light the opposite side. Sources outside the
image still reach matching room lamps. Disc radius, soft edge and both boundaries
are visible in the Space tab. CrimsonHue shows the raw CDT light and Ambient values
beside the mapped lamp output. Sliders and numeric inputs expose the brightness,
Ambient response, color and spatial adjustments. **Extra background light**
defaults to 0% and color adjustments are neutral: zero Ambient plus no confirmed-clear
lights produces black. The previous automatic orange-to-amber correction and
saved-brightness upgrade have been removed. The result is a source-driven
lighting estimate; neither CDT feed is calibrated to displayed game pixels or
physical lamp brightness.

## Start

1. Install [**CrimsonDesertTelemetry 2.2.1**](https://github.com/ZappendusterFX/CrimsonDesertTelemetry/releases/tag/v2.2.1) with its server, rendered and upstream
   ManyLights capture, and source visibility enabled.
2. Install [**.NET 8 Desktop Runtime x64**](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
   With the game closed, import this ZIP through DMM, or put its
   `CrimsonHue/CrimsonHue.asi` in the same ASI loading directory as CDT.
   Only the ASI needs deploying. Start the game; the in-game mixer opens.
   The ZIP contains no runtime installer or ASI loader.
3. In **Setup**, enter your bridge's local IPv4 address, click **Apply addresses**
   and **Find bridge**. The preset
   `192.168.2.109` is the development setup; change it to your own address.
4. Check the displayed bridge identity. Press the bridge's link button (for diyHue,
   in its web UI), then click **Trust bridge & pair** within 30 seconds. Repeat the
   link-button press if the pairing window expires. Both the application key and
   Entertainment client key are requested automatically.
5. Select an existing **Entertainment area**. Create and position lights in the
   Hue app first, then refresh. CrimsonHue preserves the existing layout/channels.
6. Use the **Output**, **Ambient**, **Color** and **Space** tabs to adjust the
   preview. Compare the raw CDT values with the channel output while tuning.
   The [tuning guide](docs/USER-GUIDE.md) explains each control, interactions
   between controls, shareable presets and a practical setup sequence.
   Stop other sync apps, then click **Start lighting sync**.
7. **Stop**, or close the overlay, to release the stream and restore previously read
   on/off, brightness and color states of the selected lights, if CrimsonHue still
   owns the stream. Running effects/dynamic scenes are not resumed.
   Close then hides the overlay; **Ctrl + Shift + H** reopens it while the game
   has focus. **Hide settings** and the shortcut hide without stopping
   sync. Press **Stop** and wait for the stopped status before leaving the game.

In **Setup → Overlay shortcut**, choose the key and Ctrl/Shift/Alt modifiers,
then click **Save shortcut**. The binding applies immediately and persists for
the next launch, independently of mixer presets. CDT uses F8–F11 even with
modifiers, so avoid those keys when running both overlays.

**Demo preview only** animates synthetic data and disables physical output. Normal
preview uses live game data and the imported layout without claiming a stream.
Demo uses the selected area when available. **Room** adds optional wall-washer
calibration: visible wall direction/width per lamp, with independent segment
offset, elevation and height span. Imported Hue positions and wire channels stay
intact. A movable single-source preview helps tune the output areas. Room setup
stays local and is excluded from mixer presets; existing setups keep point mapping.
An explicit **Test CH** action identifies one independently addressable segment
with white at 10% for two seconds, then clears, releases and restores when still
owned. It works without game telemetry, is disabled in demo, and never starts
automatically. See [wall-washer setup](docs/USER-GUIDE.md#wall-washer-room-calibration).
Streaming never starts automatically when the ASI loads.

## Requirements and scope

- Windows 10/11 x64.
- .NET 8 **Desktop Runtime x64**, including its WindowsDesktop/WPF framework.
  The plain .NET Runtime or ASP.NET Runtime alone does not supply WPF. .NET 9/10
  alone does not fulfill the .NET 8 requirement.
- CrimsonDesertTelemetry 2.2.1, schema 1.6, on the same PC for all-around,
  confirmed-visible lights. Older schema 1.4/1.5 installations still use the
  original rendered-only feed without physics visibility filtering.
- A Hue-compatible bridge with HTTPS API v2, Entertainment DTLS 1.2/PSK on UDP 2100,
  color lights and an existing Entertainment area.
- Development target: diyHue. A physical Hue Bridge BSB002 with two Hue Play wall
  washers has passed pairing and read-only area/light-state import. Physical
  Entertainment output on that bridge remains untested.
  Backend/lamp performance varies; 30 outgoing updates/s is a target, not a
  measured physical lamp refresh rate.

CrimsonHue consumes the telemetry service over WebSocket/JSON. Its own ASI hosts
the consumer; it has no game memory reader, screen capture or
bundled game files. Its UI hooks DXGI presentation, resizing and color-space
changes. Users need the running CDT service, not its source repository.
An ASI depends on the game's process lifetime: process termination can prevent
bridge cleanup. See [ASI limits](docs/ASI.md#lifetime-and-limits).

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

Player distance supplies a configurable fade. **Direction origin** selects the
point used to calculate source directions: `player + blend × (camera - player)`.
The percentage interpolates all three coordinates with no additional height
offset. At 100%, a source between the character and the third-person camera can
be classified as in front; at 0% it can be behind. At 50%, the origin is halfway.
The camera's right/up/forward axes supply orientation at every percentage.
Changing the origin does not change the player-relative distance cutoff.

Each source has a
2D circular footprint whose size is **Source disc radius** in degrees. A lamp's
direction must intersect this disc and remain on the source's side of every
enabled boundary. **Strict left / right** and **Strict front / rear** are on by
default and clip the disc at the room center lines, before any weighting.
Opposite-side contributions are exactly zero, even at Directional focus 0.
If no lamp matches, that source contributes nothing; it is never reassigned to a
forbidden side. A source or lamp exactly on a center line can use either adjoining
half. Height participates in the circular footprint and angular weighting.

**Source disc soft edge** controls the outer fraction that fades to zero; all
directions outside the disc are zero. This is a planar angular footprint, not a
volume or a simulated reflection. It works around all camera directions,
including outside the game image, without a screen-frustum filter.
**Directional focus** weights the allowed channels within the disc.
**Strongest-channel compensation** controls normalization against the strongest
allowed channel; it does not undo the disc's soft edge or either boundary.
Both boundaries, the disc defaults and the 50% origin are loaded for older saved configurations;
existing numeric choices are retained. Ambient is a separate, non-directional
layer controlled in the Ambient tab; it does not represent reflected local light.
Hue X points right, Y towards the screen and
Z up. The preview is top-down; cards show channel height, which participates in
the direction calculation. A clear physics ray is a sampled geometry verdict,
not a guarantee of optical visibility through every material or at every instant.

Each source's HDR intensity is compressed with a common RGB scale before applying
distance and direction weights, so high source intensity cannot undo that source's
fade. Local colors retain their source ratios with the default color controls.
RGB gains, hue shift and saturation apply the same rule to all local-light
colors; no fire color is detected or specially recolored. Weighted contributions
are combined in linear RGB and overflow is scaled down. Output is sRGB-encoded,
corrected by the displayed gamma control and brightness-limited. Imported Hue
brightness balance is applied where provided.
Short configurable smoothing reduces jitter on the legacy rendered feed when
both strict boundaries are off. Confirmed-visible mode and either strict boundary
update channel colors immediately so a previous source cannot linger after
becoming hidden or moving to another side. This is an artistic estimate, not
game pixels, physical lux, measured room distances or full scene lighting.
Spotlight cones are not simulated. CDT supplies the separate source visibility
decision; CrimsonHue does not perform its own occlusion test.

### Controls and raw values

Numeric tuning controls have a slider and numeric input; the two strict boundaries
have on/off switches. Adjustments affect preview
and active sync immediately. **Reset this tab** restores that tab's defaults;
**Save settings** stores the current configuration. Settings are also saved on
Start and normal window close. Existing saved brightness is retained as entered; the 85% default
applies to new or reset settings only.

**Export preset** writes only mixer values, never bridge setup or credentials.
In **Presets**, enter an absolute `.json` path. **Preview import** validates the file,
previews changed values and lets you
choose which tabs to apply. Output, Ambient and Color are selected initially;
Space is unselected because it is room-dependent. Applying a preset saves it and
updates any active lighting sync. The format has a version number; unsupported,
incomplete or out-of-range files are rejected without changing settings.

| Tab | Controls (default; range) |
| --- | --- |
| Output | Maximum brightness (85%; 0–100%), Light sensitivity (1.5×; 0–10×), Local light mix (100%; 0–400%), Output gamma (1; 0.1–4), Legacy smoothing (100 ms; 0–1000 ms) |
| Ambient | Ambient exposure (1×; 0–10×), Ambient reference level (4; 0.01–100 raw units), Ambient mix (40%; 0–100%), Extra background light (0%; 0–100%), Ambient black threshold (0; 0–100 raw units), Local mix in bright Ambient (100%; 0–200%), Ambient transition (400 ms; 0–5000 ms) |
| Color | Local hue shift (0°; −180–180°), Local saturation (100%; 0–200%), Red balance / Green balance / Blue balance (each 1×; 0–4×), Ambient tint hue (0°; 0–360°), Ambient tint saturation (0%; 0–100%) |
| Space | Strict left / right (On), Strict front / rear (On), Direction origin (50%; 0% player–100% camera), Fade starts (0; below Off beyond, up to just under 1000 game units), Off beyond (35; 1–1000 game units), Distance falloff (2; 0.1–8), Source disc radius (60°; 1–89°), Source disc soft edge (25%; 0–100%), Directional focus (2; 0–16), Strongest-channel compensation (100%; 0–100%), Room direction offset (0°; −180–180°) |
| Setup | Bridge pairing, imported Entertainment area and local telemetry address |

The raw readout shows CDT's global sky RGB, camera sky visibility, camera-local
Ambient RGB and exact working level, plus confirmed-clear source positions/RGB.
Those values are shown before any tuning. Calculated channel RGB describes the
mapped output after tuning. A display swatch is an illustration; the numeric
working values retain their original meaning and may exceed the display range.
These diagnostics do not measure the physical lamps. **Freeze readout** holds
the raw display for inspection and copying; live mapping and streaming continue.

### Distance fade

- **Fade starts:** full distance strength up to this distance from the player.
- **Off beyond:** no new contribution at or beyond this distance.
- Between both values, strength falls smoothly to zero. **Distance falloff** changes
  the curve; larger values attenuate the middle of the interval more strongly.
  Approaching a source uses the same curve in reverse. Legacy smoothing can
  briefly retain the previous output after crossing the cutoff.

The curve preview plots remaining contribution in percent against distance in
game units. It updates with Fade starts, Off beyond and Distance falloff.

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

### Ambient response

CrimsonHue reads CDT's separate `/v1/ambient/stream` on the same loopback port.
It requires a fresh global sky sample, fresh measured camera sky visibility and
the available local product estimate. The three working RGB channels provide a
**relative brightness proxy**, not display color. **Ambient exposure** and
**Ambient reference level** control its response; **Ambient mix** sets the amount
of fill. **Extra background light** defaults to 0% and applies only with valid
Ambient input and Ambient exposure above zero. **Ambient black threshold** also
defaults to 0; raise it to treat small positive Ambient values as black. Exactly
zero input produces zero fill with the defaults, after any configured transition
has ended. The raw readout continues to show the original value even when the
threshold suppresses the mapped result. **Ambient tint saturation**, in the Color
tab, defaults to 0% for neutral fill. **Local mix in bright Ambient** defaults to
100%, leaving local contributions unchanged; lower values soften their influence
as Ambient rises, and higher values strengthen it.
**Ambient transition** controls the smoothing time. Response levels and transition
time are editable in the Ambient tab; the tint controls are in Color. This
is a tunable visual model, not physical lux or pixel-accurate room illumination.

Sky and local visibility expire independently after 1500 ms, including client
time. On missing/stale Ambient, the fill is removed immediately and the app falls
back to the existing local-light mapping; the status line names that fallback.
Invalid or missing local-light telemetry still clears output and releases the
Entertainment stream as before. See `docs/AMBIENT-INTEGRATION.md` for limits and
acceptance checks.

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

Telemetry defaults to `ws://127.0.0.1:27311/v1/stream`; Setup allows
another local port. Only loopback addresses are accepted. Host restarts reconnect
automatically and reset sequence tracking. Additive schema 1.x fields are tolerated.

## Build and verify

Use a .NET 8 SDK (or newer with the .NET 8 x64 host pack) and Visual Studio 2022
with Desktop development with C++ and CMake tools on Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-Asi.ps1
dotnet run --project tests/CrimsonHue.Tests -c Release
artifacts\native-asi\Release\CrimsonHue.ControllerSmoke.exe artifacts\asi\CrimsonHue.asi artifacts\controller-smoke\controller.png
powershell -ExecutionPolicy Bypass -File scripts/Publish.ps1
```

The tests need OpenSSL for real DTLS interoperability. By default they use Git for
Windows' `C:\Program Files\Git\usr\bin\openssl.exe`; pass `--openssl <path>` after
the `dotnet run` argument separator to override. `--live-telemetry` adds a read-only
CDT light WebSocket control, `--live-bridge` probes the development bridge, and
`--live` runs those plus the Ambient WebSocket control. None changes lamps.

`--live-ambient` adds a read-only Ambient WebSocket control. `--live-mapping`
reads the protected saved area and fresh CDT streams, then prints calculated
channel RGB for the actual camera, a simulated 180° turn, Ambient only and local
lights only, plus local-only comparisons at 0% player and 100% camera origin,
and the active boundary, disc and origin settings;
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

The developer-only ControllerSmoke loads the actual headless ASI and installed
runtime, verifies the hidden controller, all 30 controls, wall calibration,
command validation and orderly shutdown. The matching CDT checkout supplies
`CrimsonHue.OverlaySmoke.exe`, which loads this ASI and renders both panels with
CDT's sole renderer, checking pixel readback, independent hide, Hue-only drawing
and resize. These harnesses never load saved credentials or connect to devices
and are excluded from releases. The publish script checks the exact controller candidate, rejects EXEs,
settings and secrets, and creates an ASI ZIP in `dist/` without overwriting versions.

## Distribution

Intended Nexus category: Crimson Desert / Utilities, as an independent ASI
requiring CrimsonDesertTelemetry and .NET 8 Desktop Runtime x64. The single
`CrimsonHue/` archive root contains one ASI plus guides and notices. The extracted
application cache lives in the current user's LocalAppData, outside DMM/game
directories. An EXE-free archive does not establish Nexus moderation or antivirus
acceptance. `docs/NEXUS.md` preserves the earlier 0.3.3 desktop listing;
`docs/ASI.md` describes this branch preview and `docs/HANDOVER.md` its verification.

Independent project, not affiliated with Signify or Philips Hue. Third-party
license notices accompany the release in `THIRD-PARTY-NOTICES.md` and
`NATIVE-THIRD-PARTY-NOTICES.txt`.
