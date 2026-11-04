# Independent output candidate — 2026-11-04

Private pair: **CrimsonHue 1.0.8-outputs.1 / CDT 2.2.10-outputs.1**.
Implemented and committed; not installed or published. Stable GitHub releases
remain Hue 1.0.7 / CDT 2.2.9. This is not a fix for the controller Skills-menu bug.

## Behavior

Hue, OpenRGB and Tuya can be selected individually or in any combination. Hue
pairing is optional for the other outputs. Each selected, configured output has
its own task; a backend failure does not cancel healthy backends. Stop cancels
all tasks and awaits cleanup. Existing Hue pairing/optional-output settings are
preserved. New setups start with no outputs selected.

Tuya supports compatible Wi-Fi protocol 3.5 RGB lamps with configurable DPS.
Address, device ID and key are entered in a separate Windows dialog; one atomic
current-user DPAPI bundle stores layout and keys together. Keys and connection
details do not enter CDT's shared HUD snapshot or shareable lighting presets.
Per-lamp workers keep only the newest desired color; changed writes are spaced
at least 300 ms after the previous write completes, with 2.5-second failure
backoff. Colors are reauthorized after the session handshake. Cleanup restores
supported prior state only while ownership and reachability can be confirmed.
The TCP check does not verify keys or send light commands.

## Verification

- Hue Core: **214/214 pass**, including all output selections, failure/Stop
  isolation, Tuya framing/authentication, loopback transport, latest-value
  delivery, freshness after handshake, ownership cleanup and atomic protected
  persistence. `artifacts/independent-outputs-tests-verified.log`.
- Exact packaged ASI controller smoke passes: hidden controller, command ABI,
  output independence, menu controls, settings validation and dry Demo behavior.
  `artifacts/independent-outputs-package.log`.
- Shared CDT D3D12 renderer: Setup, OpenRGB-only, Tuya-only and Lighting pass.
  OpenRGB-only and Tuya-only repeated with the exact packaged Hue ASI and final
  production CDT renderer build. Logs/screenshots:
  `C:/DEV/CrimsonDesertTelemetry/artifacts/independent-outputs-*`.
- Exact packaged ASI WPF smoke passes; secure Tuya setup screenshot visually
  checked. `artifacts/independent-outputs-packaged-wpf.log` and
  `artifacts/independent-outputs-packaged-wpf-tuya-setup.png`.
- CDT production native tests: **45/45 pass**. The unchanged CDT managed suite
  was not rerun for this panel-only change.
- Both maintained package builders and folder/ZIP validators pass. Hue contains
  one ASI with embedded application components and seven exact package files;
  no loose runtime, executable, settings or credentials. CDT uses production
  Research OFF / native build 26423880 and nine package files.
- Shared panel manifest and AGENTS/CLAUDE byte parity checked in both repositories.

These are synthetic, loopback and package checks. No actual lamp commands,
private credential import, deployed-file changes, owner installation, game/input
acceptance or real three-backend concurrency test was performed. Tuya 3.3/3.4,
Bluetooth and Zigbee are not supported by this backend.

## Immutable packages

| Package | Payload source | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `dist/CrimsonHue-1.0.8-outputs.1-asi-win-x64.zip` | `0284cb5` | 3102437 | `4A415B51B25CD098D18FBF368E27A2AF180DDD1C7F03978AD859526FC870B579` |
| `C:/DEV/CrimsonDesertTelemetry/artifacts/mod-manager/CrimsonDesertTelemetry-v2.2.10-outputs.1-ModManagers.zip` | `72d699a` | 1186599 | `15557E07FBACF886113BA4297E57D9C23836ACCB4126366369EEC072490AC620` |

Each ZIP has an adjacent `.sha256` file. Per-file receipts are in each
repository's `artifacts/independent-outputs-receipt.json`.

Owner-only next step: stop sync, close the game and install both matching private
packages through DMM. After restart, verify OpenRGB with Hue unchecked, then
configure compatible Tuya lamps and check the selected combination. Installation
and physical-output acceptance remain separate from the checks above.
