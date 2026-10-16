# Shared CDT/Hue overlay — 2026-10-16, Codex

**Superseded by live failure and fix:** installed `huepanel.2` crashed during
software-cursor rendering, a path the original hidden smoke omitted. Use CDT
**2.2.5-huepanel.3** with the unchanged Hue **0.4.0-asi.10**. ZIP SHA-256
`A401EA8E0AE8C12136E2816AB774ED9AAF66B9B4A86C90CCD67AB3F18447D1F1`.
Diagnosis, exact native stack/map, failing-before/passing-after cursor test,
input release hardening and 13/13 focused regressions are in
`artifacts/cdt-shared-overlay/docs/HUE_CURSOR_HANG.md`.
The `.3` fix is built and validated, not installed/game-tested. The historical
package-pair evidence below is preserved and does not claim `.2` passed startup.

The owner authorized moving the Hue HUD into mandatory CDT while continuing to
play. No installed ASI, game process, game hook or physical lamp was changed.
CDT integration lives in the isolated CDT Git worktree
`C:\DEV\CrimsonHue\artifacts\cdt-shared-overlay`, branch `codex/hue-shared-overlay`,
based on CDT `3284601`. Original CDT's unrelated untracked research stays intact.
CrimsonHue is on `codex/crimsonhue-asi`.

## Final immutable package pair

| Package | Profile | ZIP SHA-256 |
|---|---|---|
| `dist/CrimsonHue-0.4.0-asi.10-asi-win-x64.zip` | Headless controller, framework-dependent .NET 8 Desktop x64 | `31E12E963E8320E2692C263877FBA802C13AB323F0E8F1ADE19F3F51A7A6ACE7` |
| `artifacts/cdt-shared-overlay/artifacts/mod-manager/CrimsonDesertTelemetry-v2.2.5-huepanel.2-ModManagers.zip` | Production code, research OFF, native build 25477059 | `7FE26932D54D1478C94F803D0E53FA913B0BA7F8751545FB6C34ADFAFBA213F7` |

ASI SHA-256: Hue `AD3DBAEED3E04F19CBEFCF9B6B1E50FC42BD42F4AA0D8EBFC062B5C7A4D2C349`;
CDT `DFA7F32CE03CE3AE3BFE0435EA5C038349BD3FA9EBEC85AC8292C5B99EB6F3C4`.
Preliminary `.asi.9` / `huepanel.1` packages remain preserved; use this final pair.
Matching runtime requirements remain separate: CDT's ASP.NET Core 8 x64 and
Hue's Desktop Runtime 8 x64. No runtime or loader is bundled.

Exact game identity was read and preserved using CDT's backup script before
building: Steam build 25477059, EXE 1.0.0.2976, SHA-256
`57DA440D72F4DB974F25FEF047CF84C4DADD999A88CB2A3C5AF4C9BD67FDE1E7`.
The existing exact-build definition is retained. This is not new live acceptance.

## Evidence

- Hue nonphysical suite: **85/85** passed, including DTLS/OpenSSL, DPAPI,
  freshness, washer integration and ownership/cleanup. No lamp output.
- CDT managed API/compatibility suite: **79 PASS** checks.
- Final production CDT native suite: **39/39** passed, including existing
  all-UI, SDR/scRGB/HDR composition, swapchain and telemetry instrumentation tests.
- Exact final Hue package: ControllerSmoke passed hidden WPF/controller ABI,
  30 controls, washer/segment calibration, synthetic preview, physical identify
  rejection, command validation and host-preserving shutdown.
- Final production CDT shared-renderer harness + exact final Hue ASI passed
  optional-module absence, bounded queue/retained cleanup, real UI rendering,
  pixel readback (**850889 visible pixels**), independent CDT/Hue hide,
  Hue-only reopen and resize. Screenshot visually inspected at
  `artifacts/shared-overlay-final/ingame.png`. The harness links CDT's production
  overlay library; it does not load the installed game's CDT module.
- Final Hue ASI's import table contains **no DXGI, D3D12 or D3DCompiler**.
  The production target no longer links the old overlay library. UiProtocol=1;
  legacy RenderedFrames=0 and production ToggleWindow is inert.
- Maintained builders/validators passed both exact package contracts and
  hash-compared ZIP entries with staging. Hue has seven exact ASI/docs files;
  CDT has its nine expected ASI/managed/INI/CFG/docs files. No saved credentials,
  settings, loose JSON or EXEs included. Both source AGENTS/CLAUDE pairs match.

Local logs: `artifacts/headless-core-tests.log`, `cdt-shared-managed-tests.log`,
`cdt-panel-final-tests.log`, `shared-overlay-final.log`,
`headless-final-imports.log`, `hue-headless-package-final.log`,
`cdt-hue-package-final.log`.

## Remaining acceptance and next step

The pair is **not installed, published or game-tested**. Actual foreground
mouse/keyboard behavior, graphics wrappers, DMM upgrade and physical Hue output
remain unverified. The owner declined repeated A/B restarts; do not repeat them.
Next: with the game closed, install both matching packages, verify deployed
hashes and run one combined game check of CDT HUD, Hue panel and saved area.
Do not start physical sync or change washer calibration implicitly.
