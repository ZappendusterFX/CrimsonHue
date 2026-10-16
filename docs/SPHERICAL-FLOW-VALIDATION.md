# Spherical flow — 2026-10-16

The owner requested continuous motion across left/right, front/rear, above/below
and diagonals. The implementation uses spherical angular weights, rather than
interpolating individual coordinate components or applying sign cuts. Point
lamps sample one direction; calibrated washers average the same field over their
visible wall region. Temporal smoothing remains a separate control.

## Verified behavior

- **102/102 nonphysical tests pass**: full 360-degree sweeps in all three planes
  with temporal smoothing disabled, rotation invariance, monotonic point/wall
  handoff, mixed ordinary bulbs and six washer segments, Ambient independence,
  1..180-degree influence radius and preset round-trip.
- Exact packaged controller smoke passes: 28 numeric controls, spherical radius
  commands/defaults/range validation, Point/Wall placement preservation, isolated
  preview, hidden WPF controller and orderly shutdown. No physical output.
- Maintained package validator passes for expanded staging and ZIP: seven exact
  files under one CrimsonHue root, one x64 ASI with five embedded component files,
  no loose executable/runtime/settings/credentials.
- No new GPU renderer test was required: the installed CDT renderer is unchanged;
  dynamic controls and axis-guide flags come from the tested Hue controller.

Logs: `artifacts/spherical-flow-tests.log`,
`artifacts/spherical-flow-package.log`. The initial test run is preserved as
`artifacts/spherical-flow-tests-first.log`; its only failure was the old exact
one-third wall-area expectation with nonuniform directional focus. The geometric
area test now explicitly uses uniform focus and retains the analytical bound.

## Offline reality check against actual assemblies

Synthetic six-channel geometry matches the imported positions X=+/-0.4, Y=0.8,
Z=-0.4/0/+0.4. Source distance 10 game units; gain 1.5, master 0.6, focus 2,
radius 60 degrees, edge softness 25%. Ambient and temporal smoothing disabled.
Separate processes load the old .asi.15 Core and newly built Core assemblies.
No network, game, lamps or real settings are accessed.

| Measurement, encoded channel RGB | Old .asi.15 | Spherical mapping |
| --- | ---: | ---: |
| Largest channel change, +/-1 degree center sweep in 0.01-degree steps | 0.536791 | 0.000074 |
| Largest channel change, full 360 degrees in 0.25-degree steps | 0.536791 | 0.020323 |
| Maximum discrepancy after rotating source and room together 90 degrees | 0.529427 | 0 |

This reproduces the old center cut and verifies continuous angular behavior in
the replacement. It does not prove that every reported physical flash has the
same cause. Source visibility changes and missing telemetry are independent.
Saved edge softness zero still explicitly selects a sharp circular cutoff.

Reproduction: `artifacts/Check-SphericalReality.ps1`; outputs
`artifacts/spherical-reality-before-asi15.json` and
`artifacts/spherical-reality-after-current.json` include assembly hashes/settings.

## Read-only live evidence and remaining Ambient issue

Before replacement, installed Hue .asi.15 and CDT huepanel.6 hashes matched their
packages; the supported game build was 25477059. All six imported Entertainment
channels had brightness balance 1.0. No bridge stream was started by diagnostics.

The stationary 12-second CDT capture contains 708 local envelopes / 179 GPU
captures and 708 fresh Ambient envelopes / 25 sky captures. Player position and
camera orientation were constant; camera height bobbed by 0.0093 game units.
No reciprocal side swap was reproduced in this interval. Working Ambient level
was 0.003675..0.005619, with camera sky visibility 0.383..0.456. A separate mapper
using saved settings generated about 0.3..0.45% encoded Ambient on all channels.
These are calculated commands, not measured light output or the actual in-game
controller's unsaved settings. Ambient sensitivity/reference remain unchanged.

Capture artifacts: `artifacts/standing-streams-asi15/` and
`artifacts/lighting-standing-asi15.json`. The latter has recursive Vec3-derived
serialization truncation warnings; scalar RGB, source counts and Ambient values
used here are intact. Neither capture proves what a later moving scene did.

The prior audit's physics-age interpretation was wrong. CDT's
`docs/SOURCE_VISIBILITY.md` defines the legacy physics age field at publication,
not GPU capture. Our .asi.15 consumer added GPU age again and could expire valid
verdicts early. Corrected transport/display aging now has explicit regressions.
Zero extra sources were dropped by that bug in this stationary recording.

## Immutable package and next step

- Version/profile: **CrimsonHue 0.4.0-asi.16**, headless Windows x64 ASI,
  framework-dependent .NET 8 Desktop Runtime x64.
- ZIP: `dist/CrimsonHue-0.4.0-asi.16-asi-win-x64.zip`
- ZIP SHA256: `6CC951F2CC0BFFD315A2FC0525089DF8783CA2CD52A2FC1A94D69190A6DD3EDC`
- ASI SHA256: `8F738B13DCB9C8AA994CFD95AD32F281097D6930081320484AC907E0BB3B3564`
- Keep installed **CDT 2.2.5-huepanel.6**. No new CDT package or source change.
- **Not installed or game/lamp-tested.** Close the game before replacing Hue.

Physical flow acceptance and exposure-aware Ambient response remain unresolved.
Existing release packages, real room calibration and credentials were preserved.
