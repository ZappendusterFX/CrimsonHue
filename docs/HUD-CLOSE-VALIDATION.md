# HUD close keeps lighting sync — 2026-10-17

The owner reported that clicking the title-bar X stopped lighting sync, while
Ctrl+Shift+H kept it running. The maintained CDT panel sends `close` for X and
hides itself. The Hue controller handled `close` by calling Stop before saving.
The shortcut only changes panel visibility. This directly explains the two
different outcomes; it does not explain unrelated bridge or telemetry failures.

`close` now saves settings without cancelling or detaching the active session.
X, Hide settings and the shortcut keep sync running. Explicit Stop, controller
shutdown and stream error handling retain their existing cleanup behavior.
The UI footer and packaged guides describe this behavior. The unchanged protocol
also allows older compatible CDT panels to send `close` to the corrected Hue
controller, although their footer still describes the old behavior.

The isolated controller startup smoke exercises the actual overlay command
handlers with a cancellable pending task as a session marker. It checks that
`close` retains the task, token source and enabled Stop control, without
cancelling or completing the marker. It then checks that explicit `stop`
requests cancellation. This uses no real bridge, telemetry or physical stream.
The packaged-controller smoke includes these assertions automatically.

Validation:

- Core: **110/110 passed**, including loopback DTLS; log
  `artifacts/hud-close-core-tests.log`.
- Exact Hue package: controller smoke and expanded/ZIP validators passed;
  log `artifacts/hud-close-hue-package.log`.
- New immutable Hue **0.4.0-asi.19**:
  `dist/CrimsonHue-0.4.0-asi.19-asi-win-x64.zip`.
  ZIP SHA256 `D1999EBD166DDF7226574937223202D541B393FD974F6899A072365416389BAF`;
  ASI SHA256 `C2D6427A3965777BCC47674DE156995E8C2B3EC36159D0FDAF2B36174B8E57CF`.
- Matching CDT **2.2.5-huepanel.9** contains the updated panel guidance;
  its worktree records package and shared-renderer evidence in
  `docs/HUE_HUD_CLOSE.md`.
- **12/12** focused CDT input/overlay checks passed. One hidden shared D3D12
  WARP run against the exact packaged Hue .asi.19 passed pixel readback
  (748,434 visible pixels), hide/reopen, independent CDT HUD, resize and shutdown.
  The screenshot was visually checked, including the new footer. Logs in the
  CDT worktree: `artifacts/hud-close-native-tests.log` and
  `artifacts/hud-close-shared-smoke.log`; image `artifacts/hud-close/lighting.png`.
- CDT ZIP SHA256
  `A418EAAF59731F633968955A4187B6335DC672A7A5BB0B581D9B409F38D94D17`;
  ASI SHA256 `E2E9646AF728CF7144B08847E26572034F29F44449E19F0953B3D90E2E49DCC2`.
  Production/research OFF, native build 25477059; maintained source copies match.

No installed ASI, game state, real settings or lamp output was changed. Existing
release packages remain immutable. Install with the game closed; real mouse-X,
shortcut reopening and continuing physical output need owner acceptance.
