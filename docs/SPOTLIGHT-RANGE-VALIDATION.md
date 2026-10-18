# Spotlight range 0–1000% — 2026-10-18

The owner reports that Spotlights at 200% only begins to have a useful effect,
and prefers a larger control range rather than dimming Point lights or changing
to relative/brightness-preserving mixing. Only the Spotlight maximum is raised:
2 to10 internally, 200% to1000% in Lighting and Advanced Output. Core mapping
validation, Recommended validation and the synchronized native slider agree.
The direct source multiplier, point0–200%, own-lantern0–100%, defaults1/1/.1,
raw input, smoothing and final output limits remain as before. This does not
turn the percentage into measured lamp brightness or bypass the output cap.

Point/spot and standalone/group are separate properties. The last neutral
capture8934 contains48 grouped and35 standalone point sources,31 grouped and5
standalone spot sources. These are all upstream records, rather than counts of
physical fixtures or a guarantee of fresh-clear visibility. The nearby fire
positions happened to be grouped points; other grouped spots are also warm.
The Spotlight control affects both standalone and grouped spots. There is no
semantic fire/blue-light classification. Existing group imbalance evidence is
in [SOURCE-WEIGHTS-VALIDATION.md](SOURCE-WEIGHTS-VALIDATION.md).

Version-1/2 preset field formats are unchanged. Values up to10 now round-trip in
version2 presets and in Recommended/Custom local settings. Earlier apps with
the old bound reject such high-value presets, so consumers need the new version.
Existing values/defaults are retained. No new mixing mode or classification rule.

Core **124/124** pass. The added regression verifies tenfold linear contribution
for a dim spotlight, unchanged point-only output when Spotlights increases,
Recommended acceptance at10, settings/preset round-trips and rejection above10.
Existing boundary cases were adjusted to the expanded upper limit. Controller
smoke exercises1000% Recommended,500% Custom, separate mode retention and invalid
1001% rejection. Exact packaged controller smoke passes, as do both expanded/ZIP
package validators. Exact Hue.24 passes hidden shared D3D12 WARP smoke:1000%
command plus exposed max, profile/custom restoration,31 controls, physical-output
isolation,748,434 visible pixels, hide/reopen, resize and shutdown. Image reviewed.
Maintained/imported panel source and SOURCE-SHA256 match. AGENTS.md/CLAUDE.md
remain byte-identical in both repositories. This scoped range change did not
repeat the unchanged input/HDR suite; its .13 evidence remains separately recorded.

Fresh versions: **Hue0.4.0-asi.24 + CDT2.2.5-huepanel.14**, Windows x64,
CDT production/research OFF, native game build25477059. Existing immutable
packages are preserved. No installation, real settings/input/process writes or
physical output were performed. Owner exits the game, installs
both matching ZIPs and adjusts Spotlights above200% while leaving Point lights
at the desired value. Physical acceptance of this new range remains pending.

- Hue ZIP: `C:\DEV\CrimsonHue\dist\CrimsonHue-0.4.0-asi.24-asi-win-x64.zip`.
  SHA256 `2943880DFA646870D8E20C6723BCF876BA7AB156C7E62EC600F208CCACC0CFD5`.
- Hue ASI SHA256 `52663E6EC04719E159401A64D4117717F91196701861224809F46A22EBED0F25`.
- CDT ZIP: `C:\DEV\CrimsonHue\artifacts\cdt-shared-overlay\artifacts\mod-manager\CrimsonDesertTelemetry-v2.2.5-huepanel.14-ModManagers.zip`.
  SHA256 `8AF50F2810A9093DCF87FAC953C2827CB7282A8837641DAC88B3635710C1D1C5`.
- CDT ASI SHA256 `32683DB18C8744BA33E70F32510D6C9DEE029C6256D9DB43FD7B843D370C3501`.
- Tested CDT renderer library SHA256
  `2483D4675BFB78ED76B20DFF8B0822972E123E9F5472FC02CC0D01BFB96FCD8F`.
- Hue logs: `artifacts/spotlight-range-tests.log`, `spotlight-range-package.log`.
  CDT logs: `artifacts/spotlight-range-package.log`, `spotlight-range-shared-smoke.log`;
  image `artifacts/spotlight-range/lighting.png`.
