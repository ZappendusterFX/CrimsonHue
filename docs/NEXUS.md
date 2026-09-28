# Nexus listing copy — CrimsonHue 0.3.2

GitHub release and download:
https://github.com/ZappendusterFX/CrimsonHue/releases/tag/v0.3.2

Complete setup and tuning guide:
https://github.com/ZappendusterFX/CrimsonHue/blob/v0.3.2/docs/USER-GUIDE.md

Required separate CDT download:
https://github.com/ZappendusterFX/CrimsonDesertTelemetry/releases/tag/v2.2.1

Use the same immutable `CrimsonHue-0.3.2-win-x64.zip` for Nexus. Its SHA-256 is
`6A1C4C5D39BB90136925A112439AA245B59348A71B5A63A85D7BAE3F6EE01DC6`.
CDT and the game are not included.

Title: CrimsonHue - Spatial Room Lighting
Category: Crimson Desert / Utilities

Summary: An external lighting companion for CrimsonDesertTelemetry. Turn live
in-game light contributions into spatial Hue Entertainment output using your
existing Entertainment area.

Inspect raw CDT light and Ambient values beside the calculated channel colors.
Tabs provide sliders and numeric inputs for output brightness, Ambient response,
color balance and hue shifts, camera direction and distance fade. The distance
curve shows contribution in percent over game-unit distance. Settings affect
preview and active sync immediately; use Save settings or Reset this tab.
Freeze readout holds the raw display for copying while streaming continues.

Required mod: CrimsonDesertTelemetry 2.2.1 with rendered and upstream ManyLights
capture and source visibility enabled for all-around, confirmed-clear output.
Other requirements: Windows x64, Hue/diyHue HTTPS API v2 bridge, color lights,
existing Entertainment area.

Install: extract the portable ZIP anywhere, run CrimsonHue.exe, pair the bridge,
select an area, then Start. Keep the telemetry mod installed separately. Do not
deploy this application into the game with a mod manager.

Version 0.3.2 adds a Direction origin slider from player position (0%) to camera
lens (100%), starting halfway at 50%. Camera axes supply orientation throughout;
the preview shows the chosen percentage. This makes the third-person positional
reference explicit. Distance cutoff remains player-relative.

It uses a bounded 2D source disc with editable radius and soft edge.
Strict left/right and front/rear boundaries default to On and clip that disc;
a local source never reaches the opposite side of an enabled boundary.
Off-screen sources can reach matching room lamps. Room center lines are shown
in the preview. Ambient remains a separately controlled, non-directional layer.

The current all-around ManyLights input emits only sources
with a fresh confirmed-clear physics verdict. Older CDT schema 1.4/1.5 uses the
legacy renderer-only path. The separate CDT Ambient feed supplies a relative
brightness signal. Extra background light defaults to 0% and requires valid,
enabled Ambient. Ambient black threshold controls which small input values map
to black; zero Ambient plus no clear local lights gives black after the chosen
Ambient transition. Ambient exposure, Ambient mix, tint and Local light mix are
editable. Color adjustments default to neutral and apply generally to all
local-light colors. Raw input stays visible and unchanged by those adjustments.
Ambient working RGB is not calibrated to local-light RGB or physical output.
Missing Ambient removes its fill and falls back to local lights.
No screen capture, complete lighting, physical-lumen or pixel-matching claim.
Mapping is an estimate of direction/proximity; the current version does not infer
surrounding brightness from the time of day.

Use HANDOVER.md for current test status and release hash. Physical Hue hardware
and larger areas require separate testing. Physical lamp appearance was checked
by the owner with diyHue/LEDVANCE but was not measured or verified on a Philips
Hue bridge. The GitHub release is published; Nexus publishing remains with the
owner. The public source repository currently has no explicit source license.
