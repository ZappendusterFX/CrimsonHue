# CrimsonHue 1.0.7 — mixer hosted by CDT

**Hue 1.0.7 / CDT 2.2.9** adds configurable menu lighting and independent CDT
acquisition pause. Menu color/hex, brightness, Save and an optional Gold preset
are directly in the HUD's Lighting page; see [the setup guide](USER-GUIDE.md#menu-lighting).
Warm white at 20% remains the default; existing settings are preserved.
Fresh gameplay data blends back in over 500 ms after resume. CDT distinguishes
intentional capture pauses from light failures and avoids repeated ready notices
on menu resume. This does not fix the reported Skills-menu stutter or repeated
controller hover sounds, which persisted in the owner's menu-mode test.

Optional OpenRGB output is included in the regular release.
Enable it in its own HUD tab, read devices and place them in the 3D room.
It runs alongside Hue with the common Start/Stop and still needs the existing
Hue bridge/area setup. New users start with OpenRGB disabled; saved private-test
enable state, selections and positions are retained. Coverage defaults to 90°,
background to 25% of shared fill and output limit to 25%. Source RGB and the shared
Hue profile are unchanged. Demo/smoke never send physical output.

Use **CrimsonDesertTelemetry 2.2.9** for menu mode and the current shared HUD.
Its update recovery runs only when needed, after valid game-world data is present,
with progress in the shared HUD. This can handle compatible address shifts;
changed engine layouts can still require a CDT update. Hue consumes only the
neutral API and does not gate output on the game's build label.

Drag a device across the room, adjust Height for vertical position and right-drag
to orbit the view. Connection/output limit contains port and brightness. Placements
are stored by hashed hardware identity, independently of transient SDK IDs, Hue
areas and shareable lighting presets. New devices default to front, output 25%.
The existing bridge/area is still needed for the common Start button.

SDK server: localhost:6742. Each selected device receives one mapped color for
its own configured position. See [OpenRGB setup](USER-GUIDE.md#openrgb-alongside-hue).
Hide/close keeps both outputs running; Stop cancels both and awaits cleanup.
The app's Demo and the packaged synthetic smoke never create OpenRGB output.

CDT draws the interactive CrimsonHue mixer in its existing D3D12/ImGui frame.
`CrimsonHue.asi` hosts the existing C# lamp controller on a hidden WPF STA. It
installs no graphics hooks and owns no D3D12 device, swapchain or ImGui context.
Telemetry still arrives exclusively through CDT's neutral HTTP/WebSocket API.
No separate EXE, process or visible WPF settings window is used.

## Installation and use

1. Install Microsoft .NET 8 **Desktop Runtime x64**. CDT also requires its
   documented .NET 8 ASP.NET Core Runtime x64. Newer runtimes alone do not meet
   these .NET 8 requirements.
2. Close Crimson Desert and stop any old desktop CrimsonHue sync. Use
   **CrimsonDesertTelemetry 2.2.9** for menu mode and the current shared panel.
   Install both packages with the game closed.
3. For a new installation, import both ZIPs through DMM using the existing x64
   ASI loader. Use Hue 1.0.7 and CDT 2.2.9; both retain the previous movement,
   lantern and changing-effect improvements. CrimsonHue's
   package contains one `CrimsonHue/CrimsonHue.asi` plus documentation/notices;
   it embeds its five application/dependency files. No loose DLL, JSON, CFG, EXE,
   runtime installer or loader is shipped in the Hue ZIP. Keep all CDT companions
   from the matching CDT ZIP; check for stale DMM upgrade files.
4. Start the game. **Ctrl+Shift+H** shows/hides the Hue panel while the game has
   focus. CDT owns this shortcut and panel input. The binding remains editable
   in **Setup → Overlay shortcut** and is saved in CrimsonHue's user settings.
   Avoid CDT's F8–F11 bindings, which also react with modifiers held.
5. In Setup, enter the bridge IP and click **Find bridge**. Its guided steps
   explain the round bridge button and **Trust bridge & pair** for new pairings.
   A matching saved pairing is reused. Wait for green **HUE BRIDGE: CONNECTED**;
   this confirms authenticated area loading. Failed connections show a distinct
   error and optional bounded diagnostic codes. Shortcut/telemetry settings are
   collapsed below the bridge flow. Setup imports your existing area and protected pairing.
   **Lighting** has three amount controls and a response choice. New setups use
   **Recommended**; existing custom values are preserved until you explicitly
   switch. **Advanced** retains the 31 numeric controls, presets and raw readout.
   **Room** retains wall/segment calibration. **Demo preview**
   never sends physical output. **Test CH** is an explicit two-second 10% white
   segment identification with ownership-aware cleanup; demo must be off.
6. **Start lighting sync** is explicit. Loading either ASI never starts it.
   **Hide settings**, the shortcut and the title-bar **X** keep sync running.
   X saves settings and hides the panel. Before quitting, **Stop** and wait for
   stopped status so the owned
   stream can be released and captured states restored when the bridge is reachable.

The Hue panel is optional and works independently of CDT's HUD/marker visibility.
`[CrimsonHue] Enabled=0` in CDT's INI disables the panel integration. Without a
compatible Hue ASI there is no Hue panel; old independent-renderer ASIs are
rejected by the UI protocol. A late ASI load can miss swapchain creation, so
restart the game after changing installed modules.

Existing settings and current-user DPAPI credentials stay under
`%LOCALAPPDATA%\CrimsonHue`. CDT receives only credential-free presentation state
and control actions. Run one Hue consumer at a time; it refuses another app's
active Entertainment stream. Room calibration is local and presets preserve it.

This controller blends local sources by angular distance on the sphere across
every axis, replacing the previous left/right and front/rear gates. Space offers
**Source influence radius** (30° recommended/reset default, 1–180°) and
**Spherical edge softness** (20% recommended/reset default; 0% explicitly selects
a sharp radius edge). **Directional focus** defaults to 4. These settings reduce
overlap between neighboring color zones while retaining a smooth angular edge.
Existing Recommended settings use them at next launch; saved Custom values and
historical omitted-field defaults are preserved. The old axis flags
remain serialized for compatibility but are ignored and have no HUD switches.
Compensation uses full room geometry independently of source-radius membership;
wall contributions average over the calibrated region. Existing wall output can
therefore differ from the old covered-region normalization.

Point lights range from 0–200%, Spotlights from 0–1000%, and Own lantern from
0–100%. Recommended starts with 100% / 100% / 10%. Lantern recognition is limited
to a conservative complete-pair signature in CDT metadata, independent of its
game-build label, with ordinary spotlight behavior on incomplete or ambiguous input. Room calibration can assign
horizontal as well as vertical segment regions; imported Hue identities stay intact.
The included lantern recognition supports the observed cone profiles and swinging
motion. The complete current release pair is Hue 1.0.7 with CDT 2.2.9.

CDT's physics rays measure collision, not optical visibility. Glass or a closed
collision shell around a visible cage lamp can exclude it before mapping. The
release does not read a material-aware or renderer-derived optical visibility
signal. More gain cannot recover excluded sources.

## Runtime and cache

Static nethost discovers the installed runtime; no dotnet.exe or global DLL-search
change is used. The headless controller exports protocol 1 with bounded UTF-8
JSON read/command functions. CDT pins the already loaded compatible ASI, reads
state on its worker and queues at most 64 commands of at most 16 KiB. Snapshots
are limited to 256 KiB. Present reads the cached state and queues commands only;
it never enters CoreCLR, the Hue bridge or a telemetry socket.

The five application/dependency files are compressed inside CrimsonHue.asi and
verified/extracted under `%LOCALAPPDATA%\CrimsonHue\asi-cache\<cabinet-sha256>`.
Names, counts, sizes and SHA-256 are verified on start. Invalid/interrupted caches
are preserved and rebuilt. Cache files stay outside the game/DMM directories.
Removing the ASI disables the controller; local cache/settings remain inert.

## Graphics and lifetime

CDT owns the sole factory/Present/resize/color-space hooks, device resources and
ImGui context. It hosts the Hue UI and input adapter in that context. Its existing
SDR, scRGB and HDR10 composition paths remain in use. Passive CDT HUD/markers draw
behind the interactive Hue panel. Hiding Hue leaves CDT's HUD independently
available. Graphics faults queue Hue Stop and release captured input. Resize and
HDR transitions rebuild through CDT's fenced worker path.

Both modules are pinned until process exit; hot unload/reload is unsupported.
`CrimsonHueStop` requests controller shutdown without terminating the host.
Status is 0 starting, 1 ready, 2 stopped, 3 ignored helper, negative on failure.
`CrimsonHueUiProtocol` returns 1; legacy RenderedFrames always returns 0 and
ToggleWindow is inert in production. CDT owns visibility. Controller diagnostics
use fixed credential-free messages under `%LOCALAPPDATA%\CrimsonHue\diagnostics`;
CDT's overlay log reports graphics readiness.

Game termination kills plugin threads. There is no exit hook guaranteeing lamp
restoration after a crash or forced termination. Stop before exit. Other mods
hosting CoreCLR/WPF and arbitrary native faults remain process-level constraints.

## Build and evidence

Use .NET 8 SDK/host pack and Visual Studio 2022 C++/CMake on Windows:

```powershell
pwsh -File scripts/Build-Asi.ps1
dotnet run --project tests/CrimsonHue.Tests -c Release
artifacts\native-asi\Release\CrimsonHue.ControllerSmoke.exe artifacts\asi\CrimsonHue.asi artifacts\controller-smoke\controller.png
pwsh -File scripts/Sync-CdtPanel.ps1 -TelemetryRoot C:\DEV\CrimsonDesertTelemetry
# Build the matching CDT checkout using its maintained native/package scripts.
C:\DEV\CrimsonDesertTelemetry\build\native-package\Release\CrimsonHue.OverlaySmoke.exe artifacts\asi\CrimsonHue.asi artifacts\shared-overlay\ingame.png
pwsh -File scripts/Publish-Asi.ps1
```

The headless controller smoke verifies the actual ASI's ABI, hidden controller,
31 controls, spherical-radius commands, washer/segment calibration, synthetic direction preview, rejected
physical identify, validation and host-preserving shutdown. CDT's shared overlay
smoke adds actual D3D12/WARP rendering, pixel readback, independent hiding,
Hue-only reopen and resize. Neither loads saved credentials or connects to lamps.
The package validator hash-compares the seven exact files with the ZIP.

Old independent-overlay/foreign-hook harness sources remain as historical
reproduction tools, not acceptance tests for this architecture. HookRaceTests
proved that interleaved independent MinHook installation can lose one overlay.
The shared renderer removes that mechanism. Synthetic success is separate from
real game/input, DMM import and physical-lamp acceptance. The owner has used
private builds in-game with physical lamps, including parallel OpenRGB and menu
lighting. The exact public 1.0.7 ZIP has not been installed or game-tested as part
of release verification. See HANDOVER.md and the repository's OPENRGB.md.
