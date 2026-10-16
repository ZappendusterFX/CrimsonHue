# Motion smoothing — 0.4.0-asi.13, 2026-10-16

Profile: independent headless CrimsonHue ASI, win-x64, .NET 8 Desktop Runtime
required separately. Compatible UI host for this preview: CDT 2.2.5-huepanel.5
(production, research off, build 25477059). Only Hue changed. No new game-build
or physical-lamp acceptance is implied by the tests below.

## Package

- ZIP: `C:\DEV\CrimsonHue\dist\CrimsonHue-0.4.0-asi.13-asi-win-x64.zip`
- ZIP SHA-256: `4736E01A06D20E083767C2E13119BAE9F1C6C4BE7BAB22548479BA7C98EFACCF`
- ASI SHA-256: `E3C63EF7F6939F26E6D9244881162CC62B80DC2491D940A1C50E9EBFBDA7388E`
- Built through `scripts/Publish-Asi.ps1 -Version 0.4.0-asi.13`; seven exact
  files in one CrimsonHue/ root, one ASI with five embedded application files.
  ZIP/staging comparison passed. No credentials/settings/runtime/loose EXE/DLL.
- Exact packaged controller smoke passed ABI/hidden WPF/30 controls, wall/segment
  commands, synthetic direction preview, physical-output isolation and shutdown.
- **Not installed or game/lamp-tested.** Existing .asi.12/huepanel.5 remained live.

## Cause and change

Installed .asi.12 forces instantaneous local output for confirmed-visible input
or either strict spatial boundary, ignoring the saved 100 ms SmoothingMs. The
owner reports severe physical strobing while moving, stable while stationary.

The local layer now uses the configured exponential transition in sRGB space,
then is decoded before independent Ambient mixing. Spatial rules govern each new
target; previous color briefly fades across a side or visibility change. Zero ms
restores immediate output. The control is named Local light transition. Default
and saved values are retained. A 250..400 ms setting offers a slower response.

Streaming uses elapsed monotonic time rather than assuming every loop takes
exactly 1/30 second. Missing local telemetry clears history and output immediately,
including a freshness failure after handshake. Fresh Ambient does not bypass
that full-local-frame guard. Ambient loss clears only its layer. Master/channel
brightness caps apply after smoothing.

## Live input evidence, before update

The owner intentionally ran and turned during an eight-second read-only sample
using the exact installed .asi.12 Core DLL as a separate neutral-API consumer:

- 171 reads: two connection-startup waits, then **169/169 fresh local and Ambient**.
- 117 local captures and 16 sky captures; no subsequent missing/stale/invalid read.
- Clear source count 52..83, changed 132 times; these count changes do not establish
  whether individual visibility verdicts or source population changes dominate.
- Ambient level 1.06249..18.91215, never zero. Player/camera values demonstrate motion.

Raw evidence: `artifacts/ambient-motion-readonly-sample.json`. Earlier existing
read-only diagnostics also showed successful Ambient-only mapping with saved
settings (`artifacts/motion-before-readonly.log`). These observations establish
fresh upstream input, not the actual in-game controller state or lamp brightness.
No physical output was initiated by these checks. Missing visible Ambient remains
unresolved; the new status helps inspect actual use in the next in-game run.

## Automated verification

**93/93 tests passed** in `artifacts/motion-ambient-diagnostics-tests.log`:

- Alternating visibility is damped in confirmed input and all boundary modes.
- Camera motion between calibrated washers fades and converges to spatial targets.
- 15/30/60/120 Hz produce the same transition at equal elapsed times.
- Stationary convergence/reset, immediate master/channel limits, independent
  Ambient smoothing and stale-Ambient removal while a local tail remains.
- Mock transport compares diagnostics with sent RGB packets for Ambient inclusion,
  Ambient loss with local light retained, and full telemetry loss clearing to black.
  Failed handshake publishes zero sent frames; cleanup/ownership tests pass.

Three spatial-only fixtures explicitly use zero transition and retain exact
opposite-side exclusion assertions. Earlier sandbox failures were existing temp
file/loopback integration restrictions; the final complete run outside the sandbox
passed. Managed/native builds and packaged smoke passed; the only build warning
was the existing third-party MinHook CMake deprecation notice.

Package log: `artifacts/motion-asi13-package.log`. CDT code/renderer is unchanged;
its previous GPU/input evidence remains scoped to that earlier run. Existing
immutable packages and original CDT research files were preserved.
