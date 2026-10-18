# Focused default zones and single HUD cursor — 2026-10-18

The owner reports that adjacent lighting zones blend together too much and asks
for better default tuning using the existing variables. A second visible cursor
was also reported; only one cursor actually controls the HUD.

## Default tuning

The mapper algorithm is unchanged. Recommended, fresh Custom initialization and
Advanced reset now share `LightingProfile.Defaults`:

| Setting | Previous | New |
| --- | --- | --- |
| Source influence radius | 60 degrees | 30 degrees |
| Spherical edge softness | 25% | 20% |
| Directional focus | 2 | 4 |

The soft angular edge is now 6 degrees wide, rather than 15. Temporal response,
gain, colors, distance fade and Ambient behavior retain their existing values.
Saved Custom settings and imported presets are preserved. Historical omitted
fields still use MappingSettings' old constructor defaults, avoiding an implicit
reinterpretation of legacy settings. Existing Recommended mode resolves the new
spatial values after updating; Custom requires Use Recommended or Space reset.

Offline point-geometry replays of two real captures from the blue table lantern
scene (3299 and 5187) compare 60/2/25 against 35/4/20, 30/4/20, 30/6/20 and
25/4/20. At 30/4/20, channel 1 changes from warm `(1, .630, .418)` to cool
`(.316, .344, .371)` in capture 3299, and from `(1, .629, .421)` to
`(.338, .367, .396)` in capture 5187. The side washer calculations stay warm.
This is settled, local-only calculation with imported point coordinates, not
measured lamp output or a replay of live wall-region calibration/smoothing.
The source's emitted-light RGB remains modestly blue; its emissive surface is
more saturated in the owner's screenshot. See [lamp diagnosis](BLUE-LAMP-DIAGNOSIS.md).

Core tests: **116/116 pass**, including adjacent red/blue zone separation with
the old profile as a reproducing overlap control, all three axis crossings,
legacy persistence, Custom round trips, streaming ownership and DTLS cleanup.
The old continuity test used a fixed color difference bound; the sharper but
continuous gradient exceeded it (0.0015846 at a 0.0001 step). The test now checks
convergence over three successively smaller steps, with temporal smoothing off,
and requires both sides to remain lit. Measured differences shrink tenfold per
step. This tests continuity independently of the intended sharper gradient.

## Cursor ownership

The maintained native adapter drew an independent ImGui cursor without hiding
the native Windows shape. That code permits the reported second, ineffective
cursor. Now SetCursor(NULL) hides the native shape while the foreground HUD owns
input and the physical pointer is within the game client. Client WM_SETCURSOR
is handled so the window's ordinary cursor selection cannot undo the hide; each
owned frame also reapplies it. The last suppressed shape is borrowed and restored
on release only when this foreground client still has a null cursor. A newer
visible game shape, background app and pointer outside the client are preserved.

The implementation follows the documented [SetCursor ownership rules](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setcursor)
and [WM_SETCURSOR handling](https://learn.microsoft.com/en-us/windows/win32/menurc/wm-setcursor).
It does not change ShowCursor's display counter, cursor position, raw device
registration, game key-up handling, the telemetry API or stream ownership.
The maintained Hue panel is synchronized to the existing CDT worktree; its
SOURCE-SHA256 manifest is regenerated from normalized source bytes.

The actual adapter compiled with mocked User32 passes native/software cursor
exclusivity, client/non-client message routing, shape restore, preserving newer
game/foreign shapes, outside-client scope and held-game-input deferral. Reentrant
cursor/clip calls and prior raw movement/fallback/DPI/wheel/held-release cases
pass. Omitting frame suppression in the copied test source fails specifically
with `Owned HUD left both the native and software cursors visible`; restoring
the maintained source passes. No real cursor, focus or game input was changed.

## Packages and evidence

Fresh immutable pair: **Hue0.4.0-asi.21 + CDT2.2.5-huepanel.11**,
CDT production/research OFF/native build25477059.

- Hue ZIP: `dist/CrimsonHue-0.4.0-asi.21-asi-win-x64.zip`.
  SHA256 `09512D09AF0110FC14BB1EB72FC09062BD3866717575CA78EE6C501C56F83A1B`.
- Hue ASI SHA256 `A0A6251A9725C6AC779F76DE4271652A3551FAC282D4EE68B10FC33B5AD10C86`.
- Exact packaged Hue controller ABI/profile/reset/Custom/isolation smoke and
  expanded/ZIP package validators pass. The package has one ASI and guides,
  with no local settings, credentials, EXE or loose runtime companions.
- CDT ZIP: `artifacts/cdt-shared-overlay/artifacts/mod-manager/CrimsonDesertTelemetry-v2.2.5-huepanel.11-ModManagers.zip`.
  SHA256 `027966C7D309E89761897FF0F18B1D899573C5F3E5DFDBAA9C23140BE53C0501`.
- CDT ASI SHA256 `70677F4964C75564A921DFB13DFF199B283E644FB1678CB46975F3AE1C9BC501`.
  Package profile, expanded payload and ZIP validators pass. **12/12** focused
  native input/overlay/notification/HDR tests pass in the production build tree.
- The exact packaged Hue.21 passes the shared CDT renderer's hidden D3D12 WARP
  smoke: 748,434 visible pixels, independent hide/Hue-only reopen, resize and
  shutdown. Screenshot visually reviewed. One remaining test expectation of 60
  degrees was corrected after the initial smoke stopped there; only the smoke
  executable was rebuilt. The renderer library remained unchanged, SHA256
  `856B851C1BB53D1A30FE7F9C37CBB468B6B2B103805EA8DAAA04C45E445A9C10`.
- Logs: `artifacts/focused-defaults-tests.log`, `focused-defaults-package.log`,
  `cursor-single-mutant-build.log`, `cursor-single-mutant-test.log`,
  `cursor-single-restored-build.log`, `cursor-single-restored-test.log`.
  Offline replay grid: `artifacts/blue-lamp-replay/grid-*.json`.
  CDT logs under its worktree's `artifacts/`: `single-cursor-package.log`,
  `single-cursor-native-tests.log`, `single-cursor-shared-smoke.log`;
  synthetic image `single-cursor/lighting.png`. Combined payload hashes are
  `artifacts/focused-zones-release-hashes.json` in the Hue repository.

Neither package was installed. No real settings, game input, process writes or
physical lamp output were changed. Real-game acceptance of the tighter zones
and absence of the extra cursor remains for the owner after installing both
packages with the game closed and selecting Recommended.
