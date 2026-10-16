# CrimsonHue 0.4.0-asi.8 — independent in-game mixer

`CrimsonHue.asi` draws an interactive ImGui mixer directly into the game's D3D12
backbuffer. It consumes CDT's neutral HTTP/WebSocket API and keeps the existing
C# mapping, pairing, DPAPI credentials and ownership-aware Hue stream lifecycle.
There is no separate EXE/process or visible WPF settings window.

## Installation and use

1. Install [Microsoft .NET 8 **Desktop Runtime x64**](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
   Plain .NET or ASP.NET Runtime alone does not supply WindowsDesktop/WPF.
   .NET 9/10 alone does not satisfy the .NET 8 framework requirement.
2. Close Crimson Desert and stop the old CrimsonHue desktop app's sync.
   Keep the working CDT installation and its existing x64 ASI loader.
3. Import the versioned ZIP through DMM, or manually copy the archive's
   `CrimsonHue/CrimsonHue.asi` beside CDT's ASI. Only this ASI needs deploying.
   The package root also contains guides and license notices. There are no loose
   DLL, JSON, CFG or EXE companions, no runtime installer and no ASI loader.
4. Start the game. The mixer opens in-game. **Ctrl + Shift + H** shows/hides it
   while the game has focus. The open overlay captures mouse/keyboard input;
   hide it to resume game controls. Move/resize the panel using its title/border.
   **Setup → Overlay shortcut** lets you choose a key and Ctrl/Shift/Alt modifiers.
   Click **Save shortcut** to apply and persist it. CDT's F8–F11 shortcuts also
   react with modifiers held; choose another key when running both mods.
5. In Setup, edit addresses and click **Apply addresses** before Find/reconnect.
   Pair and select an existing Entertainment area as described in the
   [user guide](USER-GUIDE.md). All 28 numeric controls and both boundary switches
   are available, with the room output, distance curve and frozen raw readout.
   The Presets tab accepts absolute `.json` file paths and previews selected
   section changes before applying. Space remains off by default for imports.
   **Room** calibrates the visible wall area of each washer and its segments;
   the single-source demo uses your selected area without physical output.
   Explicit **Test CH** buttons identify a segment at 10% white for two seconds
   without requiring telemetry; demo must be off. Test Stop/cleanup completes
   before another sync or test can start. Room setup is local and presets preserve it.
6. **Start lighting sync** is explicit; loading or demo preview never starts it.
   **Hide settings** and the shortcut keep active sync running. The panel's X
   stops sync, saves and hides it. Reopen it with the shortcut.
7. Before quitting the game, click **Stop** and wait for stopped status. This
   releases the owned area and restores captured states where the bridge is
   reachable and stream ownership still matches.

Existing settings and current-user protected credentials are reused from
`%LOCALAPPDATA%\CrimsonHue`. Run only one CrimsonHue consumer at a time.
Neither takes over another application's active Entertainment area.

## Runtime and cache

The installed Desktop Runtime supplies CoreCLR/WPF. Static nethost discovers
its hostfxr; no dotnet.exe is launched and no global DLL search path is changed.
The existing WPF controller runs unshown on its own STA, allowing the native UI
to reuse the verified consumer logic. The production path never calls Show on
that window. A bounded credential-free UI snapshot and command queue connect
the renderer and controller; Present never calls the bridge or CoreCLR.

Five application/dependency files are compressed inside the ASI, then extracted
under `%LOCALAPPDATA%\CrimsonHue\asi-cache\<cabinet-sha256>`. Each start verifies
file names, count, sizes and SHA-256. Interrupted extraction is not published;
an invalid cache is preserved and rebuilt, with a fresh private copy used when
the old cache is locked. Cache DLLs/metadata stay outside the game and DMM
directories. Removing the ASI disables the plugin; cache/settings stay local
and do not execute independently. Old immutable release ZIPs are preserved.

The single-ASI layout avoids companion deployment/removal problems documented
in CDT's `docs/MOD_MANAGER_VALIDATION.md`, including DMM retaining DLL/CFG files
and classifying loose JSON as patches. Actual DMM import/removal of this new
package still requires validation.

## Graphics and lifetime limits

The native renderer tracks the command queue supplied to DXGI swapchain creation,
then hooks Present/Present1, resizing and color-space changes. It waits for
graphics wrappers to finish creation, initializes GPU resources on a worker,
fences in-flight buffers and skips a draw if its upload slot is busy. It uses a
private ImGui context and chains the window procedure. SDR, scRGB and HDR10 have
explicit output paths; HDR UI uses 200-nit paper white. Unsupported output or
graphics faults disable drawing; a draw fault requests stream Stop and releases
captured input. Coexistence with CDT, other input hooks and real graphics wrappers
has not yet been game-tested. A late load can miss swapchain creation: restart
the game. Helper processes such as crashpad_handler.exe are ignored.

Native graphics diagnostics are generated in
`%LOCALAPPDATA%\CrimsonHue\diagnostics\overlay-<pid>-<tick>.log` (at most 64 KiB
per launch). They record fixed graphics/controller status, hook failures,
D3D12 initialization error codes, first submitted frame and shortcut events.
They do not contain settings, credentials or UI/API snapshots. Installation
failure is retried at most ten times, 500 ms apart. Shortcut polling continues
even without a captured swapchain, so a nonworking toggle leaves a diagnosis.
The owner reported no visible overlay with `.asi.4` on 2026-10-03. This candidate
adds diagnostics; the actual game failure has not yet been located or resolved.

The shortcut is stored in the existing local `settings.json` as
`"Shortcut": { "Key": 72, "Control": true, "Shift": true, "Alt": false }`.
`Key` is the Windows virtual-key value; 72 is H. Missing/invalid key bindings
fall back to this default. Modifier-only and mouse keys are rejected. No game
directory configuration file is needed. Loading a mixer preset preserves it.

CLR initialization and graphics work occur outside DllMain. The ASI is pinned
until process exit; hot unload/reload is unsupported. Close the game before
replacing/removing it. Native `CrimsonHueStop` requests bounded controller cleanup
without ending the game process. `CrimsonHueStatus` is 0 starting, 1 ready,
2 stopped, 3 ignored helper and negative on failure; graphics readiness is
separate from controller readiness.

Process termination kills the plugin's threads. This preview installs no exit
hook and cannot promise restoration on game exit, crash or forced termination.
Stop before exit remains necessary. Other mods hosting CoreCLR/WPF can conflict;
initialization failure does not start physical output. Arbitrary native faults
are not isolated from the game process. Removing EXEs does not guarantee Nexus
moderation or antivirus acceptance.

## Build and evidence

Use the .NET 8 SDK/host pack and Visual Studio 2022 C++/CMake tools on Windows.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-Asi.ps1
dotnet run --project tests/CrimsonHue.Tests -c Release
artifacts\native-asi\Release\CrimsonHue.OverlaySmoke.exe artifacts\asi\CrimsonHue.asi artifacts\overlay-smoke\ingame.png
artifacts\native-asi\Release\CrimsonHue.HdrTests.exe
artifacts\native-asi\Release\CrimsonHue.BridgeTests.exe
artifacts\native-asi\Release\CrimsonHue.OverlaySmoke.exe artifacts\asi\CrimsonHue.asi artifacts\foreign-before\ingame.png --foreign-before
artifacts\native-asi\Release\CrimsonHue.OverlaySmoke.exe artifacts\asi\CrimsonHue.asi artifacts\foreign-after\ingame.png --foreign-after
powershell -ExecutionPolicy Bypass -File scripts/Publish-Asi.ps1
```

The developer harnesses are never packaged. OverlaySmoke loads the exact ASI
into a native process with a hidden synthetic-only controller: no saved
credentials, network connection or physical stream. It checks all 30 controls,
wall-area/segment commands, isolated source preview and physical-test rejection,
command validation, actual D3D12/WARP pixel readback, hide/reopen, resize and
host-preserving shutdown. HdrTests checks offscreen composition against scRGB,
PQ/BT.2020 and SDR pixel values. The older AsiSmoke renders synthetic WPF panels
and supports cancellation/missing runtime/resource/cache checks; it is not the
production UI. Publish checks the exact candidate and hash-compares the ZIP's
seven whitelisted files with its staging folder.

The two foreign-hook modes also load a developer-only DLL with its own static
MinHook and forwarding factory/Present hooks, before or after CrimsonHue.
Both must receive creation/Present calls while CrimsonHue draws and resizes.
This tests generic hook coexistence; it does not reproduce ReShade/Streamline
or prove coexistence in Crimson Desert.

Synthetic native/GPU evidence is distinct from actual Crimson Desert, DMM,
physical lamp and HDR-display acceptance. Exact results and next owner action
are recorded in [HANDOVER](HANDOVER.md).

Hosting follows the [.NET native hosting design](https://github.com/dotnet/runtime/blob/main/docs/design/features/native-hosting.md),
using hostfxr command-line initialization without executing a managed Main.
Generic graphics/HDR code is adapted under MIT from CrimsonDesertTelemetry;
there is no telemetry implementation or game-memory reader in this repository.
