# CrimsonHue 1.0.0 — setup and tuning guide

CrimsonHue maps live Crimson Desert light telemetry to the channels of an
existing Hue Entertainment area. Its headless ASI uses CDT's in-game mixer panel. Its
output is an artistic lighting estimate, not a sample of the displayed pixels
or a measurement of the physical lamps.

## What to install

1. Install **CrimsonDesertTelemetry 2.2.5** separately in the game.
   Earlier public CDT releases do not include the Hue panel. Download both ZIPs
   from their GitHub releases. Enable CDT's server, rendered and upstream ManyLights
   capture, source visibility, and Ambient feed. Keep CDT running with the game.
   For all-around light sources, CrimsonHue needs CDT's fresh confirmed-clear
   visibility results. A missing or blocked source contributes no new light;
   the previous local output fades according to **Local light transition**.
2. Install [.NET 8 Desktop Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
   Close the game. Import the ZIP through DMM, or copy its
   `CrimsonHue/CrimsonHue.asi` into the same ASI loading directory as CDT.
   Only the ASI is deployed; application files are embedded, and the runtime is
   installed separately. Start the game; the in-game mixer opens automatically.
   **CDT, an ASI loader, the game, bridge firmware and
   saved credentials are not in this ZIP.** See [ASI guide](ASI.md).
   Close the game and upgrade both matching packages for this version: the
   HUD close guidance is updated in CDT and the stream fix is in CrimsonHue.
   CDT 2.2.5 includes the held-input handoff and duplicate-cursor corrections;
   CrimsonHue 1.0.0 supplies sharper Recommended spatial defaults and adjustable
   light-type and own-lantern weights. Update both.
3. Use Windows 10/11 x64 and a Hue-compatible bridge with HTTPS API v2,
   Entertainment DTLS 1.2/PSK, color lamps and an existing Entertainment area.
   Set the area and channel positions in the Hue app first. diyHue is the
   development target. A physical Hue Bridge BSB002 with two Hue Play wall washers
   has passed pairing and read-only area/light-state import. The owner reports
   physical Entertainment output with private builds, including Play bars and
   wall washers. Exact 1.0.0 installation/game/input acceptance remains separate.
4. In **Setup**, enter the bridge's own private IPv4 address and click
   **Find bridge**; it applies the address directly. For a new pairing, press the round link button **on the
   physical Hue Bridge**, then click **Trust bridge & pair** within 30 seconds.
   A saved pairing is reused only when address and certificate match.
   Wait for the green **HUE BRIDGE: CONNECTED** confirmation, then select your
   Entertainment area. Finding a bridge alone is not a connected confirmation.
   A failed connection shows an explicit error; **Connection details** has bounded
   error codes for diagnosis without credentials. The address prefilled
   in the application is a development example: replace it. CDT's local stream
   defaults to `ws://127.0.0.1:27311/v1/stream` on the same PC.
5. Use **Recommended** on the Lighting page, then stop other sync apps and
   press **Start lighting sync**. **Demo preview** uses synthetic input and never
   drives lamps. Normal preview uses CDT and imported channel positions but
   does not claim the Entertainment stream.

The .NET 8 Desktop Runtime x64 is required. The plain .NET or ASP.NET Runtime
alone does not include WPF, and .NET 9/10 alone does not fulfill this requirement.
Source builds also
need Visual Studio's C++/CMake tools; see the [README](../README.md).

**Ctrl + Shift + H** is the default show/hide shortcut while the game has focus.
In **Setup → Overlay shortcut**, choose the key and Ctrl/Shift/Alt modifiers,
then click **Save shortcut**. It applies immediately and is remembered across
launches. Mixer presets do not change it. Avoid F8–F11 alongside CDT, which
also reacts to those keys when modifiers are held.
**Hide settings**, the shortcut and the title-bar **X** keep sync running.
X saves settings and hides the overlay; the shortcut reopens it.
Mouse and keyboard input are captured while
the overlay is open in the foreground; hide it to resume normal game controls.
If a game button or key is still held when opening the HUD, release it first;
the panel shows a reminder until it can take over input.
Press **Stop** and wait for the stopped status before quitting the game.
Process termination can prevent restoration; the ASI cannot keep running after
the game exits. Synthetic verification does not establish real-game input,
physical brightness, HDR-display or mod-manager behavior for the exact release ZIP.

## Everyday lighting

New installations use **Recommended**. The normal page has three controls:
**Overall brightness** limits every lamp; **Background light** sets the shared
environment layer; **Local light effects** sets the amount from nearby sources.
**Response** chooses Quick, Balanced or Calm and coordinates both transitions.
Balanced uses 250 ms for local sources and 600 ms for the background.
The spatial defaults use a 30° source radius, 20% edge softness and directional
focus 4 to keep adjacent color zones more distinct. Advanced resets use those
same spatial defaults. Existing Recommended installations adopt them at next
launch; saved Custom configurations retain their values.

**Lighting → Light balance** works in both Recommended and Custom:

- **Point lights** (0–200%) and **Spotlights** (0–1000%) independently weight all
  contributions of that reported kind, including grouped sources. 100% is
  unchanged; 0% removes that kind, 200% doubles its contribution, and 1000%
  multiplies spotlights by ten before final output limiting. Defaults stay at 100%.
- **Own lantern** is an additional 0–100% multiplier for the recognized equipped
  lantern pair. Recommended starts at 10%; 0% excludes it completely. At 100% it
  follows the ordinary spotlight weight. Raising Spotlights raises other sources
  of that kind; the extra lantern control keeps your own dim.

The game reports geometric kinds, rather than semantic fire/blue-light types.
Point and spot sources can each be standalone or grouped; grouping is a separate
property. The Spotlight control affects both standalone and grouped spotlights.
Sources without a recognized kind retain normal strength. The own-lantern match
uses a conservative signature for game build 25477059, rather than an engine
ownership ID. Unsupported builds, missing metadata, incomplete or ambiguous
pairs keep their ordinary type weight. The matched count refers to the current
input frame. Raw input stays unchanged; normal transitions and final output
limits still apply. These percentages are mixer weights, not measured brightness.
Save, Start and normal close remember each mode's values. Old Custom settings
and version-1 presets default to 100% for all three weights to preserve behavior.

The recommended profile supplies a coherent mapping with neutral colors/gamma,
no black threshold, no artificial background floor and continuous spherical
blending. It uses the game data; it does not normalize every dark scene into a
bright room. Room alignment and Point/Wall calibration remain personal.

Existing installations start in **Custom** to preserve their current behavior.
Choose **Use Recommended** to adopt the profile while keeping your current overall
brightness limit. Your complete custom tuning remains stored separately; **Advanced
→ Use Custom settings** restores it. Save, Start and normal close persist the mode
and both configurations. The old Mapping block stores custom tuning, so external
diagnostic scripts must resolve the Lighting preferences to reproduce live output.

The status explains missing data, zero master brightness, a background disabled
by settings, a black threshold removing the input, and zero camera sky visibility.
Calculated lamp percentages describe commands, not measured light or proof of
delivery. A sky-only feed cannot describe every indoor reflection or exposure;
the profile does not invent those missing measurements.

## Advanced readout and tuning

**Advanced → CDT raw input** displays source and Ambient values before CrimsonHue's
sliders. It can contain RGB values above 1 because the game data is HDR. The
**calculated channel colors** show the result after the mapping controls; a
physical lamp may still reproduce a dim command more brightly than expected.
**Freeze readout** pauses the displayed raw numbers for inspection while mapping
and live sync continue. Use the live status to distinguish fresh input from
missing, stale, blocked or unknown visibility. With no confirmed-clear local
lights and zero Ambient, the default calculated output becomes black after the
Ambient transition ends.

CDT's visibility verdict is based on physics collision, not optical visibility.
A glass pane or collision shell around a lamp cage may block all sample rays
even though you can see the lamp or its light on a wall. Such a source is removed
before CrimsonHue's brightness and Point/Spot controls. More gain cannot recover
it. The release does not include material-aware or renderer-derived optical
visibility. A lamp's apparent color also does not identify its geometric kind:
blue fixtures can be point lights or spotlights.

All 31 numeric controls remain under **Advanced**, editable in **Custom** mode.
Each has a slider and an exact numeric input. Changes apply to
the live preview and active sync immediately. **Save settings** persists them;
**Reset this tab** restores the defaults for one tab. Start and normal window
close also save. The raw CDT values never change when you move a control.

## Sharing presets

Open **Advanced → Presets**, enter an absolute `.json` file path, click **Export preset**,
and share the resulting readable
JSON file. Version 2 contains the 31 numeric mixer settings, plus a format version and
name derived from the filename. The legacy `SeparateLeftRight` and
`SeparateFrontRear` fields remain for compatibility; they no longer affect
mapping and have no HUD switches. It does **not** contain
the bridge address, Entertainment area ID, telemetry endpoint or bridge keys.
Never share `%LOCALAPPDATA%\CrimsonHue\bridge.secrets` or your complete local
`settings.json`.

To use a shared file, enter its absolute path and click **Preview import**. Review the list of
values that would change, then select the sections to apply. **Output**,
**Ambient** and **Color** start selected. **Space** starts unselected because
room direction, lamp positions and distance preferences vary between users.
Click **Apply selected** to switch to Custom and save the chosen values; if lighting sync is already
running, the new values also affect it immediately. Cancel leaves everything as
it was. The import checks the preset version, every required value and valid
ranges; local bridge pairing and Entertainment area remain yours.
The three light-balance values belong to **Output**. Version-1 presets still
import with neutral weights; exports use version 2 and require the updated app.

Shared presets cannot include Hue channel positions. Each installation reads
those from its own Entertainment area, so the same preset can look different in
different rooms and on different lamps.

## Wall-washer room calibration

The **Room** tab describes the illuminated wall as seen from your seat. A washer
can be beside the monitor while its light covers a side wall; its Hue point alone
cannot describe that area. Calibration is optional and starts disabled. The room
preview keeps the imported Hue dots and draws calibrated wall areas in blue.

An area can mix ordinary bulbs, multi-segment lights and wall washers. Leave
**Point (Hue position)** selected for a light represented by its imported Hue point.
Choose **Wall area** for a light whose visible illumination should cover a wall region.
This choice is per light, not per area. A normal bulb directed at a wall may also
benefit from a wall region; the model describes the visible result, not a product
name or a physical spotlight beam.

Hue Entertainment v2 supplies channels and segment membership; CrimsonHue keeps
these identities and imports the channel positions and brightness balance. A
multi-segment light therefore keeps independently addressable outputs. Segment
count alone does not determine where it shines: a strip and a wall washer can
both have multiple segments. Room calibration adds the visible direction and
extent of each segment without changing the Hue configuration.

1. Select your actual Entertainment area. Turn on **Demo preview only** to tune
   safely with synthetic input on those channels. **Preview one light source**
   provides direction and height sliders instead of the animated demo sources.
2. For each light that illuminates a wall region, select **Wall area**, then set **Direction** and **Wall width** (one segment) or **Segment width** (multiple segments).
   Direction 0 is the screen, -90 is left, +90 is right, and +/-180 is behind you.
   Width is the angular extent of each segment's wall region from your seat, not
   the emitter's beam angle or the total width of all segments. **Start left/right** supplies an editable starting point spanning
   front-side to side (-90 to -30 or +30 to +90 degrees); it is not a calibration.
3. Expand each segment to set **Side offset**, **Height angle** and **Height span**.
   Height initially follows the Hue data. Do not assume the segment order matches
   the physical wall: inspect it using the separate test below.
   For a washer lying horizontally, give its three segments the same Height angle
   and different Side offsets. For example, offsets -10/0/+10 degrees with a
   10-degree Segment width form three adjacent regions around the light's Direction.
   These are starting values; fit the actual illuminated patches from your seat.
   Reverse the offsets if channel identification shows the opposite physical order.
4. **Save room calibration** persists it for this PC. Start and normal close also
   save it. **Point (Hue position)** disables wall mapping but keeps its settings
   for later. **Reset to imported Hue positions** removes that lamp's override. Room
   setup is stored per area/light/segment and is never exported or replaced by
   a shared mixer preset. Imported bridge positions and channel IDs are preserved.

The panel shows the number of Hue segments for each light. If the Hue area later
adds a segment to an existing calibration, its placement is marked for review.
Adjust that segment and click **Confirm segment placement**. Until confirmed, it
adds no local game-light contribution to wall mapping; Ambient remains available.
Other calibrated segments keep working. A shared Hue channel still receives the
combined contributions of its members and cannot isolate one physical segment.
Removed segments are reported; changing a different angle or saving alone never
confirms new segment placement. Point mode continues to use imported positions.

For a physical segment check, stop sync, turn off **Demo preview only**, and click
**Test CH ... (2 s, 10%)** for one channel. This explicitly sends white at 10% to
that channel and black to the others for two seconds. It requires the paired
bridge, but no CDT input. **Stop** cancels it early. The test refuses another
app's active Entertainment stream, clears output at the time limit, then releases
and restores captured static light states when still owned. It is disabled in
demo and in the developer harness. Channels shared by multiple segments cannot
identify one segment independently. A failed/unreachable bridge can prevent
restoration; the status reports cleanup failures.

Mapping averages the source's contribution over the calibrated wall area; a small
intersection remains small even with strongest-channel compensation. Source
influence radius, spherical edge softness and distance fade still apply. Source
radius describes how far a game light spreads; wall width describes where your
room can display it. A wide source can influence both front-side regions despite
an unlit physical gap behind the monitor. Ambient remains a separate shared fill.
Compensation uses the full room geometry, independently of which parts intersect
the source radius. Wall intensity can differ from older versions that normalized
against only the covered part; the full calibrated region now defines the average.

## A useful tuning order

1. Confirm that a moving, **fresh** CDT feed and the expected clear sources
   appear. If the raw feed is empty, no brightness slider can create a local
   source.
2. Set **Space** first. Check imported lamp positions, room direction, source
   influence radius and spherical edge softness. In a third-person scene, try **Direction
   origin** from player to camera while watching which side receives a source.
   Then set **Off beyond**, **Fade starts** and **Distance falloff**; the curve
   shows the distance response in game units.
3. Tune **Output** with a nearby clear source. Set the safety cap first, then
   source sensitivity and mix. Use gamma to lift dim nonzero values. Gamma
   cannot turn true black into light.
4. Tune **Ambient** separately using the raw working level. Decide whether
   near-black samples should remain black; keep **Extra background light** at
   0% if you want a truly dark room when the raw level is zero.
5. Adjust **Color** only after location and strength look right. Color controls
   use one rule for all local sources; there is no special fire recoloring.

## Output controls

| Control (default) | Effect and interaction |
| --- | --- |
| **Maximum brightness** (85%, 0–100%) | Final upper limit on every channel. Imported per-channel brightness balance still applies. It cannot create light when calculated RGB is zero. |
| **Light sensitivity** (1.5×, 0–10×) | Exposure applied to each local source before distance fade. Zero removes local sources; it does not remove Ambient. Raising it can cause bright sources to compress, making changes less obvious. |
| **Local light mix** (100%, 0–400%) | Amount of mapped local-light RGB added to the Ambient layer. Zero removes local sources from output even when raw sources are present. |
| **Point lights** (100%, 0–200%) | Multiplies accepted point contributions before spatial mixing. Applies to standalone and grouped points; no effect on excluded sources. |
| **Spotlights** (100%, 0–1000%) | Multiplies accepted spot contributions before spatial mixing. Applies to standalone and grouped spots; cones are not simulated. |
| **Own lantern** (10% Recommended, 100% neutral legacy/Custom default; 0–100%) | Additional multiplier for the complete conservatively matched lantern pair on build 25477059. Unknown or ambiguous sources keep ordinary type strength. |
| **Output gamma** (1, 0.1–4) | Higher values lift dim **nonzero** output. Black remains black. Use carefully with lamps that have a high minimum visible brightness. |
| **Local light transition** (100 ms, 0–1000 ms) | Smooths local brightness and color over time in every input mode. Old local color fades briefly after a visibility or direction change. Zero is immediate; 250–400 ms gives a slower response for fast wall washers. Ambient has its own transition. Missing local telemetry clears output immediately. |

## Ambient controls

CDT sends a separate global sky sample, measured camera sky visibility and a
local Ambient estimate. CrimsonHue uses its **working level** as a relative
brightness proxy. The raw sky RGB is diagnostic, not a ready-made room color.
Ambient is non-directional fill; it does not simulate light reflected from a
nearby brazier. Stale Ambient is removed, leaving local-light mapping.

| Control (default) | Effect and interaction |
| --- | --- |
| **Ambient exposure** (1×, 0–10×) | Scales the level after the black threshold. Zero disables Ambient, including **Extra background light**. |
| **Ambient reference level** (4 raw units, 0.01–100) | Response scale: with 1× exposure, an above-threshold level equal to this value gives about 63% response. Lower values make a given small level more influential. It is not the raw input or a brightness floor. |
| **Ambient mix** (40%, 0–100%) | Maximum linear contribution of Ambient before the master brightness cap. Zero removes Ambient fill. |
| **Extra background light** (0%, 0–100%) | Optional fill for **fresh, enabled** Ambient, including a black sample. Raising this deliberately lights otherwise dark scenes. Keep at zero for source-driven darkness. |
| **Ambient black threshold** (0 raw units, 0–100) | Subtracts a threshold from the raw working level; remaining zero maps to no natural Ambient response. The raw readout still shows the original number. Combine with **Extra background light = 0%** if low raw values should appear black. |
| **Local mix in bright Ambient** (100%, 0–200%) | Changes the local-light contribution as Ambient rises. 100% preserves it; lower values reduce local contrast in bright Ambient, higher values strengthen it. It does not make Ambient directional. |
| **Ambient transition** (400 ms, 0–5000 ms) | Smooths changes to valid Ambient brightness. Zero is immediate. A previous Ambient value can remain briefly during a normal transition; missing or stale Ambient removes the fill. |

## Color controls

Local controls affect all local sources by the same rule and do not alter raw CDT
RGB. Ambient tint is independent of the local hue and balance controls.

| Control (default) | Effect and interaction |
| --- | --- |
| **Local hue shift** (0°, −180–180°) | Rotates every local source's hue by the same amount. Zero retains CDT's color ratios after the balances below. |
| **Local saturation** (100%, 0–200%) | Zero makes local output grayscale; 100% preserves the mapped saturation; higher values intensify it. |
| **Red / Green / Blue balance** (1× each, 0–4×) | Per-channel multipliers on local-source RGB before exposure. Use small changes to compensate for a lamp's color bias; zero removes that component from all local sources. |
| **Ambient tint hue** (0°, 0–360°) | User-chosen Ambient tint hue. It does not derive a hue from CDT sky RGB. |
| **Ambient tint saturation** (0%, 0–100%) | Zero makes Ambient neutral; raise it to apply the chosen tint hue. It does not recolor local sources. |

## Space controls

CDT supplies source positions. CrimsonHue imports the Hue area's channel
positions. The paired **camera axes** define front, right and up. A source may
affect a matching room lamp even when it is outside the game image. Source
visibility comes from CDT; CrimsonHue does not cast its own geometry rays.
Local contribution follows angular distance on the sphere. Left/right,
front/rear, above/below and diagonals use the same continuous rule, with no hard
axis gates. The old axis-separation switches are no longer shown or applied.

| Control (default) | Effect and interaction |
| --- | --- |
| **Direction origin** (50%, 0–100% camera) | Position used to classify source direction: 0% player, 100% camera lens, intermediate values along the player-to-camera line. Camera orientation is used throughout. This can move a nearby source from rear to front in third-person view. It does **not** change player-relative distance fade. |
| **Source influence radius** (30°, 1–180°) | Angular radius around each local source direction on the sphere, including off-screen sources. Outside this radius, contribution is zero. A wider radius increases overlap and can bridge gaps between widely spaced lamp directions; 180° reaches every direction. This describes angular influence, not reflections or a physical volume. |
| **Spherical edge softness** (20%, 0–100%) | Outer fraction of the angular radius that fades to zero. Choosing 0% explicitly gives the radius a sharp edge. The region outside the radius remains zero. |
| **Fade starts** (0 game units, 0 to below Off beyond) | Full source distance strength up to this player-relative distance. Must stay below **Off beyond**. |
| **Off beyond** (35 game units, 1–1000) | Source contributes nothing at or beyond this player-relative distance. Use a smaller value for a compact room effect. The units are game units, not calibrated metres. |
| **Distance falloff** (2, 0.1–8) | Shape of the fade between **Fade starts** and **Off beyond**. Higher values suppress the middle and far part more strongly. The curve preview shows the result. |
| **Directional focus** (4, 0–16) | Weights channels within the source's spherical influence. Zero is even before edge softness; higher values favor closer directions. Channels outside the radius receive no contribution. |
| **Strongest-channel compensation** (100%, 0–100%) | Compensates directional attenuation against the strongest affinity in the full room geometry. That geometry is independent of which channels intersect the source radius, preventing compensation jumps when channels enter or leave it. 0% leaves angular attenuation. Spherical edge softness and the wall-area average still apply. |
| **Room direction offset** (0°, −180–180°) | Rotates game camera direction relative to the imported room layout. Use if left/right/front/rear are consistently misaligned; it does not move Hue channel positions. |

Channel height matters to angular matching. A high rear channel is not
automatically a “sky lamp.” For a source close to the player and camera, changing
**Direction origin** can change its side, while the imported lamp height can
still place it outside the source influence radius. The preview shows the area layout.

These spatial rules describe the target colors. **Local light transition** briefly
fades previous colors toward those targets; set it to zero for immediate spatial
changes when checking calibration.

## If the result looks wrong

| Observation | Check first |
| --- | --- |
| Rapid flicker while moving | Keep **Spherical edge softness** above 0% for a soft source-radius edge. **Output → Local light transition** controls changes over time; 250–400 ms gives a slower response. Visibility and raw input can still change quickly; physical behavior depends on your hardware and scene. |
| Ambient seems absent | While syncing, the Ambient status now says whether the stream includes Ambient or is paused. **CDT raw input** also shows the last streamed Ambient level/fill and counts of frames cleared for local data or missing Ambient. These are calculated values, not measured lamp brightness. Ambient tint saturation 0% produces neutral white fill. |
| All lamps stay on in darkness | Inspect the **raw Ambient working level** and confirmed-clear source count. Set **Extra background light** to 0%; check the Ambient black threshold and transition. Ambient tint and output gamma cannot turn zero into light. If calculated output is tiny but a lamp remains visibly bright, the lamp/bridge may have a minimum visible output; compare the numeric channel RGB. |
| Fire on one side lights the other | A source near a room axis can influence both sides through spherical blending. Check imported positions, wall calibration and **Room direction offset**; reduce **Source influence radius** or raise **Directional focus** for a narrower effect. Ambient remains shared fill. |
| Rear lamp does not react to a source behind the character | Watch the raw source position and visibility, then try **Direction origin** from 0% toward 100%. Check source influence radius, the channel's imported height and **Off beyond**. A source can be behind the player but ahead of a third-person camera. |
| Turning the camera barely changes output | Check that the raw feed and paired camera update; reduce Ambient mix while testing local sources; inspect whether a source is clear, within distance and within the channel's angular reach. A large radius or low directional focus can spread the local response. |
| Fire is too red or too dim | Compare raw RGB with calculated RGB. Adjust local RGB balance/hue/saturation for color and sensitivity/mix/gamma for level. Changes affect **all** local sources, not only fire. A physical lamp can have its own color or minimum-brightness behavior. |
| A visible glass/cage lamp produces no local effect | Check source visibility first. A fresh `blocked` result is excluded before Point/Spot gain; collision geometry can disagree with visible geometry or transparency. This is a known input limitation. |
| No physical output | Confirm live CDT input, paired bridge, selected Entertainment area and **Start lighting sync**. Demo preview intentionally has no physical output. Another Entertainment app may own the stream. |

Credentials are saved in the current user's DPAPI-protected store, outside the
release ZIP. **Stop** releases an owned Entertainment stream and tries
to restore the previously read light states. See the [README](../README.md)
for freshness and cleanup limits, and [Ambient integration](AMBIENT-INTEGRATION.md)
for the feed's limits.
