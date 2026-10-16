# CrimsonHue 0.4.0 ASI preview — setup and tuning guide

CrimsonHue maps live Crimson Desert light telemetry to the channels of an
existing Hue Entertainment area. Its headless ASI uses CDT's in-game mixer panel. Its
output is an artistic lighting estimate, not a sample of the displayed pixels
or a measurement of the physical lamps.

## What to install

1. Install the matching **CDT 2.2.5-huepanel.5 preview** separately in the game.
   The public CDT 2.2.4 lacks this preview's Hue panel; obtain both matching ZIPs
   from this branch's validation checkpoint. Enable its server, rendered and upstream ManyLights
   capture, source visibility, and Ambient feed. Keep CDT running with the game.
   For all-around light sources, CrimsonHue needs CDT's fresh confirmed-clear
   visibility results. A missing or blocked source does not light a channel.
2. Install [.NET 8 Desktop Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
   Close the game. Import the ZIP through DMM, or copy its
   `CrimsonHue/CrimsonHue.asi` into the same ASI loading directory as CDT.
   Only the ASI is deployed; application files are embedded, and the runtime is
   installed separately. Start the game; the in-game mixer opens automatically.
   **CDT, an ASI loader, the game, bridge firmware and
   saved credentials are not in this ZIP.** See [ASI guide](ASI.md).
3. Use Windows 10/11 x64 and a Hue-compatible bridge with HTTPS API v2,
   Entertainment DTLS 1.2/PSK, color lamps and an existing Entertainment area.
   Set the area and channel positions in the Hue app first. diyHue is the
   development target. A physical Hue Bridge BSB002 with two Hue Play wall washers
   has passed pairing and read-only area/light-state import; physical
   Entertainment output on that bridge remains untested.
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
5. Check the live preview and raw readout, tune, then stop other sync apps and
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
**Hide settings** keeps sync running. Closing the overlay stops sync, saves and
hides it; the shortcut reopens it. Mouse and keyboard input are captured while
the overlay is open in the foreground; hide it to resume normal game controls.
Press **Stop** and wait for the stopped status before quitting the game.
Process termination can prevent restoration; the ASI cannot keep running after
the game exits. This branch has synthetic verification, with real game and lamp
acceptance still pending.

## Read the screen before tuning

The **raw CDT readout** displays source and Ambient values before CrimsonHue's
sliders. It can contain RGB values above 1 because the game data is HDR. The
**calculated channel colors** show the result after the mapping controls; a
physical lamp may still reproduce a dim command more brightly than expected.
**Freeze readout** pauses the displayed raw numbers for inspection while mapping
and live sync continue. Use the live status to distinguish fresh input from
missing, stale, blocked or unknown visibility. With no confirmed-clear local
lights and zero Ambient, the default calculated output becomes black after the
Ambient transition ends.

Every numeric control has a slider and an exact numeric input. Changes apply to
the live preview and active sync immediately. **Save settings** persists them;
**Reset this tab** restores the defaults for one tab. Start and normal window
close also save. The raw CDT values never change when you move a control.

## Sharing presets

Open **Presets**, enter an absolute `.json` file path, click **Export preset**,
and share the resulting readable
JSON file. It contains the 28 numeric mixer settings and two boundary switches,
plus a format version and name derived from the filename. It does **not** contain
the bridge address, Entertainment area ID, telemetry endpoint or bridge keys.
Never share `%LOCALAPPDATA%\CrimsonHue\bridge.secrets` or your complete local
`settings.json`.

To use a shared file, enter its absolute path and click **Preview import**. Review the list of
values that would change, then select the sections to apply. **Output**,
**Ambient** and **Color** start selected. **Space** starts unselected because
room direction, lamp positions and distance preferences vary between users.
Click **Apply selected** to save the chosen values; if lighting sync is already
running, the new values also affect it immediately. Cancel leaves everything as
it was. The import checks the preset version, every required value and valid
ranges; local bridge pairing and Entertainment area remain yours.

Shared presets cannot include Hue channel positions. Each installation reads
those from its own Entertainment area, so the same preset can look different in
different rooms and on different lamps.

## Wall-washer room calibration

The **Room** tab describes the illuminated wall as seen from your seat. A washer
can be beside the monitor while its light covers a side wall; its Hue point alone
cannot describe that area. Calibration is optional and starts disabled. The room
preview keeps the imported Hue dots and draws calibrated wall areas in blue.

1. Select your actual Entertainment area. Turn on **Demo preview only** to tune
   safely with synthetic input on those channels. **Preview one light source**
   provides direction and height sliders instead of the animated demo sources.
2. For each lamp, enable **Use wall area**, then set **Direction** and **Wall width**.
   Direction 0 is the screen, -90 is left, +90 is right, and +/-180 is behind you.
   Width is the angular extent of the wall from your seat, not the emitter's beam
   angle. **Start left/right** supplies an editable starting point spanning
   front-side to side (-90 to -30 or +30 to +90 degrees); it is not a calibration.
3. Expand each segment to set **Side offset**, **Height angle** and **Height span**.
   Height initially follows the Hue data. Do not assume the segment order matches
   the physical wall: inspect it using the separate test below.
4. **Save room calibration** persists it for this PC. Start and normal close also
   save it. **Use imported Hue positions** removes that lamp's override. Room
   setup is stored per area/light/segment and is never exported or replaced by
   a shared mixer preset. Imported bridge positions and channel IDs are preserved.

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
intersection remains small even with strongest-channel compensation. Existing
source-disc size, distance fade and strict side boundaries still apply. Source
disc size describes how far a game light spreads; wall width describes where your
room can display it. A wide source can influence both front-side regions despite
an unlit physical gap behind the monitor. Ambient remains a separate shared fill.

## A useful tuning order

1. Confirm that a moving, **fresh** CDT feed and the expected clear sources
   appear. If the raw feed is empty, no brightness slider can create a local
   source.
2. Set **Space** first. Check imported lamp positions, room direction, strict
   boundaries and the source footprint. In a third-person scene, try **Direction
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
| **Output gamma** (1, 0.1–4) | Higher values lift dim **nonzero** output. Black remains black. Use carefully with lamps that have a high minimum visible brightness. |
| **Legacy smoothing** (100 ms, 0–1000 ms) | Reduces jitter only on the older rendered-only feed when **both** strict boundaries are off. Confirmed-visible mode or either strict boundary updates local channel colors immediately, so this slider then has no local effect. |

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

| Control (default) | Effect and interaction |
| --- | --- |
| **Strict left / right** (On) | A left-side local source contributes exactly zero to right-side channels, and vice versa. |
| **Strict front / rear** (On) | A front local source contributes exactly zero to rear channels, and vice versa. Boundaries apply before angular weighting; a source with no matching channel is dropped. Center-line sources/channels may belong to either adjacent side. Ambient remains independent of both switches. |
| **Direction origin** (50%, 0–100% camera) | Position used to classify source direction: 0% player, 100% camera lens, intermediate values along the player-to-camera line. Camera orientation is used throughout. This can move a nearby source from rear to front in third-person view. It does **not** change player-relative distance fade. |
| **Source disc radius** (60°, 1–89°) | Angular radius of a bounded **2D circle** around each local source direction, including off-screen sources. Outside the disc, contribution is zero. Strict boundaries clip it; increasing radius never crosses an enabled boundary. This is not a world-space sphere or reflection model. |
| **Source disc soft edge** (25%, 0–100%) | Fraction of the disc's outer rim that fades to zero; 0% is a hard edge. The region outside the disc always remains zero. |
| **Fade starts** (0 game units, 0 to below Off beyond) | Full source distance strength up to this player-relative distance. Must stay below **Off beyond**. |
| **Off beyond** (35 game units, 1–1000) | Source contributes nothing at or beyond this player-relative distance. Use a smaller value for a compact room effect. The units are game units, not calibrated metres. |
| **Distance falloff** (2, 0.1–8) | Shape of the fade between **Fade starts** and **Off beyond**. Higher values suppress the middle and far part more strongly. The curve preview shows the result. |
| **Directional focus** (2, 0–16) | Distributes weight among already allowed channels inside the disc. Zero is even; higher values favor closer directions. It cannot cross a strict boundary or revive a channel outside the disc. |
| **Strongest-channel compensation** (100%, 0–100%) | Normalizes a source against its best allowed channel. At 100%, the strongest allowed direction can receive full angular strength; 0% leaves angular attenuation. The disc edge and strict boundaries still win. |
| **Room direction offset** (0°, −180–180°) | Rotates game camera direction relative to the imported room layout. Use if left/right/front/rear are consistently misaligned; it does not move Hue channel positions. |

Channel height matters to angular matching. A high rear channel is not
automatically a “sky lamp.” For a source close to the player and camera, changing
**Direction origin** can change its side, while the imported lamp height can
still place it outside the finite source disc. The preview shows the area layout.

## If the result looks wrong

| Observation | Check first |
| --- | --- |
| All lamps stay on in darkness | Inspect the **raw Ambient working level** and confirmed-clear source count. Set **Extra background light** to 0%; check the Ambient black threshold and transition. Ambient tint and output gamma cannot turn zero into light. If calculated output is tiny but a lamp remains visibly bright, the lamp/bridge may have a minimum visible output; compare the numeric channel RGB. |
| Fire on one side lights the other | Confirm both strict boundary switches are On, imported channel positions are correct, and **Room direction offset** matches the room. Ambient is shared fill and can still light both sides. |
| Rear lamp does not react to a source behind the character | Watch the raw source position and visibility, then try **Direction origin** from 0% toward 100%. Check disc radius, the channel's imported height and **Off beyond**. A source can be behind the player but ahead of a third-person camera. |
| Turning the camera barely changes output | Check that the raw feed and paired camera update; reduce Ambient mix while testing local sources; inspect whether a source is clear, within distance and within a lamp's disc. A large radius or low directional focus can spread the local response. |
| Fire is too red or too dim | Compare raw RGB with calculated RGB. Adjust local RGB balance/hue/saturation for color and sensitivity/mix/gamma for level. Changes affect **all** local sources, not only fire. A physical lamp can have its own color or minimum-brightness behavior. |
| No physical output | Confirm live CDT input, paired bridge, selected Entertainment area and **Start lighting sync**. Demo preview intentionally has no physical output. Another Entertainment app may own the stream. |

Credentials are saved in the current user's DPAPI-protected store, outside the
release ZIP. Normal Stop/close releases an owned Entertainment stream and tries
to restore the previously read light states. See the [README](../README.md)
for freshness and cleanup limits, and [Ambient integration](AMBIENT-INTEGRATION.md)
for the feed's limits.
