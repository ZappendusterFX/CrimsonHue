# CDT-owned build compatibility and lantern recognition — 2026-11-03

This records the 1.0.1 change. Hue 1.0.4 retains CDT-owned build validation and
updates the complete-pair signature for current cones and swinging motion; see
[LANTERN-MOTION-20261103.md](LANTERN-MOTION-20261103.md).

The owner asks to remove CrimsonHue's old game-build dependency: all game data
comes from CDT's neutral API, and CDT validates the executable/engine layout.
CrimsonHue1.0.1 therefore removes the GameBuild==25477059 condition from
PlayerLanternSignature.Find. The build field remains diagnostic metadata. This
architectural rule is also recorded identically in AGENTS.md and CLAUDE.md.

CDT does not report a player-owner ID for lights. Hue still conservatively infers
the equipped lantern from the complete pair: all-around feed, valid horizontal
player-physics-root forward, two unique standalone spotlights,31.137793/26.885372
cone half angles, forward-aligned normalized directions, bounded player-relative
mount, coincident origins and2.5 RGB ratio. The full matching body is unchanged.
Missing/changed/ambiguous metadata keeps ordinary spotlight strength. Raw input,
kind weighting, Recommended/Custom values, presets, visibility filtering, schema,
capture-pairing and freshness rules are unchanged. No game-memory reader,
telemetry reconstruction or new CDT ownership/schema field was introduced.

## Verification

-125/125 Core regressions pass, including real local DTLS interoperability.
-Complete-pair recognition and own-lantern weighting agree for old25477059,
 current26361232, future and missing build labels. Zero lantern strength excludes
 only the pair; unrelated spots and raw frames stay intact.
-All previous missing/partial/ambiguous/non-player/wrong-feed cases still reject
 across those same labels. Parser metadata test uses current26361232.
-Actual new published component Core parses the preserved neutral new-build CDT
 snapshot into38 clear all-around contributions. This is OFFLINE replay only;
 it finds no lantern pair in that uncontrolled saved frame and does not prove
 a real new-build lantern match. No AN/AUS repetition was requested/performed.
-Maintained Publish-Asi.ps1 builds one ASI with5 embedded files. Exact packaged
 controller smoke passes Recommended/Custom, source/lantern weights,31 controls,
 invalid commands, placement/preview isolation and host-preserving shutdown.
-Expanded and ZIP validators pass:7 exact files, single CrimsonHue root, no loose
 DLL/JSON/CFG/EXE, settings, credentials, loader or bundled runtime.
-Manifest checks bind every embedded file's length/hash to the new component;
 packaged ASI equals the built ASI. No new renderer or full-game suite required.

The initial restricted build could not read the Windows SDK directory. The same
maintained test command succeeds with ordinary SDK access outside that restriction;
no compiler/test guard was disabled and no product-code workaround was added.

## Immutable package

ZIP:dist/CrimsonHue-1.0.1-asi-win-x64.zip (3011765 bytes).
SHA256:00630C8B32BBFF769CF9D2D2DA712B68484332C901694DCE688B8014D8A4AC13.
ASI SHA256:05F355883BD0C5AD07BAB0E5D1C29E1BDDA076DD33CB778FCD2B011336D87DEA.
Embedded Core SHA256:F5D2993491F60B7764F930ECD9C49C6746C6769903FDC2FF91F40015938DD9F0.
Builder:scripts/Publish-Asi.ps1 -Version1.0.1. All old packages remain immutable.
Use CDT2.2.6 for gamepatch2.04.05; older compatible CDT schemas remain accepted.
Only source/docs/build defaults changed. No settings, installation, game input,
bridge connection or physical output was changed. Not game-tested; publication is recorded below.
Installation is exclusively the owner’s responsibility with the game closed.

Private evidence:artifacts/lantern-cdt-compatibility-tests-rechecked-20261103.log,
lantern-cdt-compatibility-package-20261103.log,
lantern-cdt-compatibility-package-check-20261103.json and
lantern-cdt-compatibility-offline-replay-20261103.json.

## GitHub publication

The owner requested GitHub push/publication. Source was fast-forwarded to main;
release/tag afc99e7 includes source fix5aa18ab. Stable
[Latest1.0.1](https://github.com/ZappendusterFX/CrimsonHue/releases/tag/v1.0.1)
published2026-11-03 12:55:37UTC. Uploaded asset digest and independently downloaded
public ZIP both match the immutable local SHA256/size above. No configured
repository Actions workflows/runs; local125 Core/controller/package checks above
are the validation evidence, not a claimed new CI/game/physical test. About text
stays accurate; Nexus remains manual. No installation was performed.
Private publication receipt:artifacts/github-1.0.1-publication-20261103.json.
