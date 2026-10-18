# Light-kind weights and own-lantern strength — 2026-10-18

## Result

Lighting now has three **Light balance** controls in both Recommended and Custom:

- **Point lights**, 0–200%, initially 100%.
- **Spotlights**, 0–200%, initially 100%.
- **Own lantern**, an additional 0–100% multiplier, Recommended/reset default 10%.
  Zero excludes the recognized pair; 100% follows the ordinary spotlight weight.

The owner explicitly requested both source-kind weighting and separate control
over their equipped lantern. This replaces the earlier unshipped checkbox
candidate. No binary or source was installed into the game. That candidate's
source snapshot and immutable Hue.22/CDT.12 packages remain under ignored artifacts.
The current pair is **Hue 0.4.0-asi.23 + CDT 2.2.5-huepanel.13**.

Weights multiply each source's bounded linear contribution after source exposure,
before spatial mixing and final output limiting. The own-lantern weight multiplies
the spotlight weight. Normal distance, spatial mapping, transitions, Ambient,
channel balance and final limits remain active. Raw telemetry/RGB is unchanged.
The controller uses its existing effective MappingSettings for both preview and
sync; no separate streaming flag or global exclusion preference is introduced.

## Available kinds and recognition limits

The neutral API reports geometric `kind` **point** / **spot**, for both standalone
and grouped sources. It does not supply semantic fire, magical-blue-light or
player-owner IDs. These two kind weights apply to grouped and standalone sources
alike. Missing or unknown kinds retain multiplier 1; the panel explains this when
untyped sources are present. An invalid optional direction cannot discard a
usable kind or invalidate the otherwise usable raw contribution.

The blue table lamp and the equipped lantern are both standalone spotlights.
Raising Spotlights raises both; Own lantern provides the separate reduction.
The raw on/off/on captures below identify a complete warm spot pair on build
**25477059**. `PlayerLanternSignature` requires that build, the all-around feed,
player-physics-root forward, both standalone spots with matching cone half angles
31.137793 / 26.885372 degrees, forward-aligned directions, a bounded body mount,
coincident positions and the 2.5 RGB ratio. Exactly one of each must be present.
No source slot or remembered world position is used as identity.

This is a conservative signature, not durable engine ownership or a general
attachment classifier. Missing metadata, unsupported builds, partial/ambiguous
matches retain their ordinary kind weight, even if Own lantern is zero. A world
pair matching the entire signature could be misclassified. Fast turns, other
equipment/animations and future builds are not established by these captures.
The panel's matched count describes the latest input frame, not lamp delivery.

## Passive live evidence

Only neutral HTTP reads and the bounded credential-free Hue state reader were
used. No process writes, game input, settings writes or physical output were
performed by the agent. Ignored captures remain under `artifacts/`:

| Capture | Owner lantern action | Matching pair | Fixed blue source |
| --- | --- | --- | --- |
| 2568 | on, first position | 2 | present, clear 9/9 |
| 7499 | off, new position | 0 | present, clear 9/9 |
| 11554 | on, same new position | 2, moved with player | present, clear 9/9 |

Files: `light-balance-20261018-current.json`, `player-lantern-off-20261018.json`,
`player-lantern-on-again-20261018.json`; captured wall geometry is in
`artifacts/hue-live-state/player-lantern-off-20261018.json`.

The earlier zero-lantern offline counterfactual using the captured Wall area
geometry raises the blue source's local-luminance share in capture 2568 from
6.14/7.91% to 18.09/37.99% on the two bars. Capture 11554 reaches 65.15/87.01%
with the pair excluded. Capture 7499 is unchanged. This verifies why a separate
own-lantern control matters; remaining fire sources can still warm the mix.
These are settled local-only calculations, separate from physical acceptance.
The blue source's raw intensity changes across captures, so on/off/on proves
presence, not a controlled emitted-brightness comparison.

## Persistence and horizontal washers

The three weights are numeric mixer values and persist separately in each mode.
MappingSettings' omitted-field defaults stay at 1 for all three, preserving old
Custom settings and version-1 presets. Existing/fresh Recommended preferences
use 1/1/.1. Switching modes restores their separate values. Advanced Output reset
uses the current product defaults (1/1/.1). There are now **31 numeric controls**.
Version-2 presets require all three weights and include them in **Output**.
Strict version-1 imports retain the old exact field set and use neutral weights;
new exports use version 2. Local pairing and room calibration remain separate.

The owner's two washers now lie horizontally beside the monitor. Existing Room
calibration supports this using distinct Side offsets and equal Height angles.
The label **Segment width** clarifies that width applies to each segment. Channel
identities and personal calibration were preserved. Physical segment order and
horizontal calibration were not inferred or changed; Test CH remains an explicit
owner action with sync stopped and demo off.

## Validation and delivery

Core **123/123** pass: point/spot separation for standalone/grouped sources,
untyped fallback, continuous multiplicative lantern strength, zero/full bounds,
world-blue-spot preservation, raw preservation, moved/rotated signature,
partial/ambiguous/unsupported cases, optional metadata, profile/settings isolation,
version-1/2 presets, and six independent horizontal washer zones.
The exact packaged controller smoke passes Recommended/Custom source-weight
commands, independent mode retention, invalid-weight rejection, 31 controls,
presets/calibration, hidden WPF and physical-output isolation. Both expanded/ZIP
validators pass. CDT **12/12** focused input/model/SDR/HDR/WebSocket regressions
pass. Exact packaged Hue.23 passes hidden shared D3D12 WARP smoke in Recommended
and Custom: command round-trips, pixel readback (748,434 visible pixels), hide/
reopen, resize and shutdown. Both screenshots were reviewed. Maintained/imported
panel sources and SOURCE-SHA256 match; AGENTS.md / CLAUDE.md remain byte-identical
in each repository.

Fresh Windows x64 pair, CDT production/research OFF, native build **25477059**:

- Hue ZIP: `C:\DEV\CrimsonHue\dist\CrimsonHue-0.4.0-asi.23-asi-win-x64.zip`.
  SHA256 `C88F87B6F1098A34AFE6E470B3EE412716910E5E8CE9DE7D3182E1DB247BB429`.
- Hue ASI SHA256 `CD3B3EADD472A0025DA756BA5B4CB67B9EAC7F18C1AEDC14409E26BF2977F309`.
- CDT ZIP: `C:\DEV\CrimsonHue\artifacts\cdt-shared-overlay\artifacts\mod-manager\CrimsonDesertTelemetry-v2.2.5-huepanel.13-ModManagers.zip`.
  SHA256 `7CF6BFEB11D344069D629DAA5E8EBBBE79469A7178DDFFC194F80634BB616D8E`.
- CDT ASI SHA256 `232A2904E419507F4033D197EA2D80C684A161E0D97966AEB0AE3A162EA707F6`.
- Tested renderer library SHA256
  `B7D6EA6AACBD25D687DEF061F591CDDB21E1915620F66EE75CD7AA82DE2A3D89`.
- Hue logs: `artifacts/source-weights-tests.log`, `source-weights-package.log`.
  CDT logs: `artifacts/source-weights-package.log`, `source-weights-native-tests.log`,
  `source-weights-shared-smoke.log`, `source-weights-custom-smoke.log`.
  Images: CDT `artifacts/source-weights/lighting.png` and `custom.png`.

No current package has been installed or accepted in game. Owner next step:
exit the game, install the matching pair and assess the three controls, then
calibrate the horizontal washer regions. Earlier input/cursor game acceptance
remains separate from synthetic validation.
