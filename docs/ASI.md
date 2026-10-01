# CrimsonHue ASI preview

This branch produces an independent `CrimsonHue.asi`. It consumes CDT over the
same HTTP/WebSocket API as the previous desktop consumer. The native loader hosts
the existing C# core and WPF mixer in the game's process. It does not launch an
EXE, host telemetry, read game memory or install graphics hooks. The package has
no EXE files, including the optional .NET `createdump.exe` helper.

The mixer remains a separate Windows window, rather than a renderer overlay.
This preserves every control, imported Hue layout, preset and current-user DPAPI
credential without duplicating the parser, mapper or stream lifecycle in C++.
The native ASI is small; its bundled .NET/WPF runtime makes the full package
roughly as large as the previous desktop distribution.

## Installation and use

1. Close Crimson Desert and stop the old CrimsonHue desktop app's sync.
2. Keep the working CDT installation and its existing x64 ASI loader.
3. Place these two entries together in that loader's ASI directory:

   ```text
   CrimsonHue.asi
   CrimsonHue/
       CrimsonHue.dll
       CrimsonHue.Core.dll
       CrimsonHue.runtimeconfig.json
       CrimsonHue.deps.json
       hostfxr.dll
       hostpolicy.dll
       coreclr.dll
       ...all remaining bundled DLLs, resources and license files...
   ```

   Do not scatter the payload DLLs into the game's root, rename the payload
   directory, or remove individual DLLs. The ASI resolves the payload relative
   to its own location, independently of the game's current directory. This ZIP
   contains no ASI loader and does not replace CDT.
4. Start the game. The mixer opens; physical sync remains stopped until you click
   **Start lighting sync**. Existing settings/credentials from 0.3.3 are reused
   from `%LOCALAPPDATA%\CrimsonHue` under the same Windows account.
5. Use **Ctrl + Shift + F10** while the game or mixer has focus to show/hide the
   mixer. **Hide settings** also hides it while sync continues. Closing the window
   stops sync, saves settings and hides it. The shortcut reopens the same window.
   A conflicting hotkey is reported in the footer; Hide settings becomes Minimize
   settings and Close minimizes after stopping sync, keeping taskbar access.
6. Before quitting the game, click **Stop** and wait for stopped status. This
   releases the owned area and restores the previously captured light states
   where the bridge remains reachable and ownership still matches.

Both the old desktop consumer and the ASI share settings and bridge credentials.
Run only one consumer at a time. Neither automatically takes over an active area.
Removing the ASI/payload with the game closed returns to the earlier desktop
distribution; no saved credentials or mapping values are migrated or deleted.

## Lifetime and limits

CLR initialization, UI creation and all network operations occur on a worker
outside `DllMain`. WPF runs on a dedicated STA with its own dispatcher. Runtime
DLLs are loaded from the payload; the process DLL search path is not changed.
The ASI is pinned for the process lifetime because CLR delegates cannot safely
survive unloading their native host. Hot reload/unload is unsupported: close the
game before replacing or removing the ASI.

Normal UI Stop/close uses the existing bounded DTLS, freshness, stream ownership
and restoration logic. The exported `CrimsonHueStop` queues that same shutdown
without blocking the loader lock. It shuts down only the plugin UI, never the
game process. `CrimsonHueStatus` reports 0 starting, 1 ready, 2 stopped, and negative
values for initialization failure. `CrimsonHueToggleWindow` queues show/hide.

Ending the host process kills the ASI's threads. No bridge restoration is promised
on game exit, crash or forced termination, and this preview installs no exit hook.
Explicit Stop before game exit is therefore part of normal use. Its window and
background work cannot remain alive once the game exits. Interaction with other
mods that already host CoreCLR is unverified; incompatible runtime initialization
fails without starting a stream. A hosted UI failure requests stream shutdown;
this does not isolate arbitrary native faults from the game process.

Build/testing require the .NET SDK and Visual Studio; end users need neither.
Removing EXEs addresses the previous package's EXE content, but does not establish
Nexus upload approval or antivirus acceptance.

## Build and evidence

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-Asi.ps1
dotnet run --project tests/CrimsonHue.Tests -c Release
artifacts\native-asi\Release\CrimsonHue.AsiSmoke.exe artifacts\asi\CrimsonHue.asi artifacts\asi-smoke.png
powershell -ExecutionPolicy Bypass -File scripts/Publish-Asi.ps1
```

The native harness is a development EXE and is never packaged. It loads the ASI
through `LoadLibraryEx` in an ordinary native process, with a synthetic-only
`CRIMSONHUE_ASI_SMOKE_PATH`. In that mode the UI does not load credentials, open
network connections or enable physical output. It verifies the existing control
coverage, renders six panels, exercises hide/reopen/close, then requests orderly
UI shutdown and confirms its own host process survives.

The harness also accepts `--stop-early`, `--stop-ready` and `--missing-payload`.
The latter requires a copy of the ASI in a test directory without its payload.
These checks cover startup cancellation, native-triggered shutdown and load
failure while keeping the native host alive.

Synthetic native/WPF loading and the existing mock HTTPS/DTLS tests are distinct
from CDT/game integration and physical lamp acceptance. The repository's
`docs/HANDOVER.md` records exact evidence and the next owner action.

Hosting follows the [.NET native hosting design](https://github.com/dotnet/runtime/blob/main/docs/design/features/native-hosting.md),
using `hostfxr_initialize_for_dotnet_command_line` and `hdt_get_function_pointer`
with `UnmanagedCallersOnly` entry points in the default assembly context. The
command-line API prepares the app-local runtime without executing a managed Main.
