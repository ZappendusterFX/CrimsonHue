# Own-lantern motion — CrimsonHue 1.0.4

The owner reports that Hue 1.0.3 / CDT 2.2.7 is substantially calmer, but room
lighting grows bright when standing and changes during walking. The latest
outdoor recording isolates a nearby warm spotlight pair: it contributes over
99.9% of the raw local luminance and receives full Spotlight102% because the
existing recognizer finds zero matches despite Own lantern10% being selected.
The owner then identifies their equipped lantern, turns it off independently
and reports that everything is calm. Its swinging motion also explains why a
forward-facing attachment box is inappropriate.

## Evidence and attribution

The requested 30-second recording is preserved in
`artifacts/forward-motion-20261103-30s-go`: 1795 raw envelopes, 480 native captures,
2026-11-03 17:10:36.049854–17:11:06.048349 CET. It contains only an initial short
motion tail (XZ motion ends about 1.23 seconds in), followed by standing; it does
not establish three complete walk/stop cycles. The pair has 42 unknown envelopes
(`waiting-for-physics`), then 1753 clear, with no blocked pair or
`outside-physics-budget` event. Unknown input remains excluded.

Actual packaged 1.0.3 Core replay, using captured room geometry and unchanged
bracketing controls, gives Bar0 mean RGB (.6375,.6140,.6006) in the early motion
window versus (1,.7724,.6194) near rest. Ambient-only red rises .5939 to .6177;
removing just the pair gives .5956 to .6199. This local pair explains the large
warm increase better than Ambient. Visibility admission changes the mean input,
so more smoothing alone cannot remove that brightness difference.

The HUD file named `hue-state-during.json` was actually read at 17:11:32, after
the recording. The before/after controls agree, but they are not synchronized
lamp output samples. Replay assumes per-channel brightness1 and reconstructs
30Hz publication timing; initial filter history is unknown. The retained audit
contains cold and first-frame-settled variants. Results above use the latter.
Details: `artifacts/forward-motion-audit-20261103/README.md` and its JSON reports.

Loaded ASI hashes were passively verified at 17:11:32: Hue1.0.3
`248044DFD035DFC954B034F2906B4BA3E48A0CD8722FDDE243DD95360DD2332F`,
CDT2.2.7 `F1F5F3FF1781D70CABBF59A167075DA6BA97EC5988E49168CFB448BA13BF579D`.
After the owner's independent lantern-off action, a passive snapshot at
17:22:26.428 reports playing, both light feeds available, source age63ms and
zero upstream sources, with Ambient available. This supports the owner's
observation; it is not a newly requested controlled AN/AUS test or proof that
every earlier fluctuation came from the lantern.

## Correction and bounds

Only `PlayerLanternSignature` changes. It remains stateless and requires the
all-around feed, finite player/source data and exactly one eligible wide and
one eligible narrow standalone spotlight across both known profiles:

| Property | Required observation |
| --- | --- |
| Paired cone half angles | 31.137793 / 26.885372 or 33.571743 / 22.3392 degrees; tolerance .02 degrees |
| Wide linear RGB | (1.5816667, .63611203, .17278898), each component within 1% |
| Narrow linear RGB | (.6326667, .25444484, .069115594), each component within 1% |
| Pair relationship | 2.5 RGB ratio within the existing 1% vector tolerance; origins within .03 game units |
| Source directions | Unit length within .02; mutual normalized dot at least .999 |
| Player proximity | Distance at most 2 game units; relative height -.25 through 1.75 |

The two members must belong to the same cone profile. Extra eligible candidates,
mixed profiles, invalid annotations and missing members reject recognition.
Heading alignment, one-sided side/ahead coordinates and near-horizontal source
direction are removed. Measured positions across the current corridor, forward
and lightning recordings reach 1.8122 game units from the player, with relative
height -.0278 through 1.202. Their complete known emission pair stays inside the
new volume during recorded motion. Other colored effects using the same cone
angles do not satisfy the emission signature.

This is a bounded metadata heuristic, not engine ownership. An unrelated light
pair matching the entire signature near the player could still match. Different
equipment, emission changes or unobserved poses beyond the bounds fall back to
ordinary spotlight weight. Partial visible pairs also keep ordinary weight;
freshness/visibility are never overridden or remembered as clear.

Own lantern multiplies the existing spotlight weight after source exposure and
before spatial mapping. 0% excludes a recognized pair; 10% applies one tenth of
its bounded linear contribution; 100% leaves ordinary spotlight strength. These
are mixer weights, not percentages of perceived lamp brightness. Other sources,
Ambient, saved settings, two-stage smoothing, neutral API/schema validation and
game-build independence are unchanged. No CDT source or package change is needed.

## Validation and delivery

All 133 Core tests pass, including recorded swing poses, both cone profiles,
missing/irrelevant player headings, finite-value and ambiguity rejection,
Own0/10/100 isolation, unrelated points/world spots and a bright different-colored
pair using the same cone angles. Existing freshness, physics, mapping, Ambient,
transition, session cleanup and local DTLS checks pass.

The exact packaged Core (1.0.4.0, SHA256
`6FB94CB28E435DBB209188174E9F708BE27394351FAD4FC3C5CEBE5013D6A79D`)
recognizes the complete raw pair in all 4163 current recorded envelopes:

| Recording | Raw pair present and recognized | Both members actually clear |
| --- | ---: | ---: |
| Forward, 30 seconds | 1795 / 1795 | 1753 |
| Quiet corridor | 286 / 286 | 52 |
| Lightning, 5 seconds | 298 / 298 | 281 |
| Lightning, 30 seconds | 1784 / 1784 | 1141 |

Raw-pair checks include excluded sources only to assess the signature. Mapping
uses the real parser's clear subset; no unknown source is added. The independent
pair-eligibility audit finds no mixed clear/ineligible pair in these recordings:
the two members always have identical position, direction and visibility metadata.

The preserved legacy owner ON/OFF/ON snapshots produce 2/0/2 matches; unchanged
blue-lamp and caged-lamp snapshots produce zero. Across all 4168 replayed frames,
Own0 output exactly equals manually removing only the recognized pair with all
other settings retained (maximum output difference0). No raw source is mutated.
Derived world-only cases also pass, but they exclude pair-cone candidates by
construction; they are not an independent proof about lookalike lights.

With the captured Own10% settings, the final packaged-Core forward replay gives
Bar0 RGB (.60271,.59781,.59525) during the early window and
(.70433,.65426,.62790) near rest. The encoded red increase is .10162 instead of
.36254, about 72% smaller in this particular offline comparison. Actual output
exactly matches the independently constructed 10% spotlight counterfactual for
this pair-dominated recording, under both initial-history variants. Ambient-only
output and Ambient state are exactly unchanged from 1.0.3. This is not a 72%
physical brightness reduction or a guarantee of constant output at Own10%.
Numerical receipt: `artifacts/lantern-motion-1.0.4-replay/forward-output-summary.json`.

The earlier audit launch retained its frozen 1.0.3 DLL despite an attempted
reference override. Its failed current-pair expectations are retained as
`forward-motion-audit-20261103/recognition-oldcore-control.json`; it is not reported
as a candidate result. A fresh isolated replay project references the packaged
1.0.4 component and checks its actual loaded hash. Final evidence:
`artifacts/lantern-motion-1.0.4-replay/recognition-1.0.4.json`,
`series-recognized.json`, `info-recognized.json` and
`artifacts/lantern-pair-eligibility-audit-20261103/audit.json`.

The maintained ASI builder, exact packaged hidden-controller smoke and expanded
and ZIP validators pass. Seven package files, one ASI, five embedded component
files; no loose runtime or settings. Immutable output:
`dist/CrimsonHue-1.0.4-asi-win-x64.zip`, 3013523 bytes, SHA256
`B46816F666C583163D27BC348C2B5C4ACF8FBD263159EB276AF4BE51C530194F`.
Packaged ASI SHA256:
`4FAB7DA0D0F3964664092DA0E3B1D843E16B3C6CF653155A7B2F46B90D20EA22`.
Build/smoke evidence: `artifacts/lantern-motion-1.0.4-package.log`.

Synthetic/offline checks do not establish physical lamp appearance. No assistant
installation, game input, settings changes or physical output is performed.
CDT2.2.7 remains installed; the owner alone installs the new Hue package. The new
package is not installed, game-tested or published; older packages are immutable.
