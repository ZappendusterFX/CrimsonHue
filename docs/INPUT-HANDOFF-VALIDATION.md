# Held input at HUD transitions — 2026-10-17

The owner reports a sword attack continuing indefinitely after an uncertain HUD
close sequence; opening and closing the HUD again cleared it. The exact game-side
input route and event order were not recorded. The maintained input adapter did
have a directly related defect: it switched capture without knowing which presses
the game had already received, and then swallowed every normal/raw release while
the HUD owned input. This can leave a game-side held action without its release.

The adapter now tracks delivered legacy and raw mouse/keyboard presses separately.
Before HUD capture it waits for both physical release and any observed game-owned
release messages. Physical key state alone cannot prove a queued release has
reached the game. A message's routing decision remains fixed through dispatch.
The HUD displays a reminder while a pre-existing game press is still held.

On Hide, X, shortcut or Stop, active HUD focus and clipping are released. Any
HUD-owned press/release pair is finished before new game presses are forwarded.
The worker maintains this handoff with all overlays hidden; actual release
messages can also complete it without another rendered frame. Focus loss/window
changes reset local tracking. ImGui keys, buttons and pending events are reset
on its render thread before reuse, never from the shortcut worker.

No SendInput, fake game key-ups, cursor warps, raw-device registration or buffered
raw-queue reads were introduced. The adapter observes only dispatched Win32
normal/raw mouse and keyboard messages plus physical key state. It does not prove
or implement suppression of an independently polled or buffered game input path.
Microsoft documents these distinct [raw input reading routes](https://learn.microsoft.com/en-us/windows/win32/inputdev/using-raw-input),
including the [mouse](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse)
and [keyboard](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawkeyboard)
release flags used here.

The actual adapter passes mocked User32 regression sequences for an already-held
attack, physical-up before queued-up, both legacy/raw delivery orders, raw-only
and mixed release/new-press packets, keyboard and right-modifier releases, closing
with an HUD click down, no-render/worker completion, focus return and ImGui reset.
Earlier cursor/relative/absolute/DPI/fallback/wheel checks remain included.
A mutation that ignores game-owned held messages fails specifically at
"HUD trusted physical-up before the queued release reached the game"; the restored
implementation passes. No real input or foreground window was manipulated.

The correction ships in **CDT2.2.5-huepanel.10**. Keep the existing
**Hue0.4.0-asi.20** controller; no managed-code change or new Hue package is needed.
Package and paired-renderer evidence is recorded in the existing CDT worktree's
`docs/HUE_INPUT_HANDOFF.md`. The older packages remain immutable.

- CDT ZIP: `artifacts/cdt-shared-overlay/artifacts/mod-manager/CrimsonDesertTelemetry-v2.2.5-huepanel.10-ModManagers.zip`.
  SHA256 `92426F7A898A24EB5CC19F09845C4956B32EF392C21FE95A13B6D6DB4315315D`.
- CDT ASI SHA256 `CE219A4FAAEBC40D2164F139AB07B13ADED712E520D81E5EA5CA1DB92EE735D2`.
- Maintained package/profile/expanded/ZIP validation passed. **12/12** focused
  CTest input/overlay tests and one hidden shared D3D12 WARP test passed with the
  exact existing Hue.20 ASI, including748,434 visible pixels, independent
  hide/reopen, resize and shutdown. Normal HUD screenshot visually reviewed.
- Logs in the CDT worktree: `artifacts/input-handoff-package.log`,
  `artifacts/input-handoff-native-tests.log`, `artifacts/input-handoff-shared-smoke.log`;
  image `artifacts/input-handoff/lighting.png`. Maintained/copy panel hashes match.
- Hue.20 ZIP remains `02DAE64D672E64E4BA79AAE0E586C3F0E6A3D9B1AA177B0DAA13D0E7AC241665`.
  No new Hue build was made; workspace guides name the newer CDT input update.

No game installation, real settings, gameplay input, process write or physical
lamp output occurred. The exact owner-observed attack loop still requires game
acceptance after installing CDT.10 with the game closed. The code-level Win32
handoff defect and its synthetic regression are separate from that acceptance.
