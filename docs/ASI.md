# CrimsonHue 0.4.0-asi.24 — mixer hosted by CDT

CDT draws the interactive CrimsonHue mixer in its existing D3D12/ImGui frame.
`CrimsonHue.asi` hosts the existing C# lamp controller on a hidden WPF STA. It
installs no graphics hooks and owns no D3D12 device, swapchain or ImGui context.
Telemetry still arrives exclusively through CDT's neutral HTTP/WebSocket API.
No separate EXE, process or visible WPF settings window is used.

## Installation and use

1. Install Microsoft .NET 8 **Desktop Runtime x64**. CDT also requires its
   documented .NET 8 ASP.NET Core Runtime x64. Newer runtimes alone do not meet
   these .NET 8 requirements.
2. Close Crimson Desert and stop any old desktop CrimsonHue sync. Use the matching
   **CDT 2.2.5-huepanel.14** preview with this version. Public CDT 2.2.4 does not host
   the Hue panel. Install both updated packages with the game closed: Hue keeps
   sync running when X is clicked; CDT displays the matching close guidance.
   The held-input handoff correction is in CDT huepanel.10. Hue .asi.24 provides
   the sharper recommended spatial defaults and separate point/spot/own-lantern
   weights; CDT.13 exposes the controls and retains CDT.11's cursor fix.
   Update both matching packages.
3. For a new installation, import both ZIPs through DMM using the existing x64
   ASI loader. For this upgrade, import both matching ZIPs. CrimsonHue's
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

The Hue panel works independently of CDT's HUD/marker visibility. Optional
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
therefore differ from the old covered-region normalization. Physical acceptance
of these changes remains pending.

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
real game/input, DMM import and physical-lamp acceptance; see HANDOVER.md.
