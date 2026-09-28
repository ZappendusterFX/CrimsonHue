# Draft listing — live lamp acceptance pending

Title: CrimsonHue - Spatial Room Lighting
Category: Crimson Desert / Utilities

Summary: An external lighting companion for CrimsonDesertTelemetry. Turn live
in-game light contributions into spatial Hue Entertainment output using your
existing Entertainment area.

Adjustable distance fade: choose where local lights begin to fade and where their
contribution ends. Settings affect preview and active sync without restarting it.

Required mod: CrimsonDesertTelemetry 2.2.1 with rendered and upstream ManyLights
capture and source visibility enabled for all-around, confirmed-clear output.
Other requirements: Windows x64, Hue/diyHue HTTPS API v2 bridge, color lights,
existing Entertainment area.

Install: extract the portable ZIP anywhere, run CrimsonHue.exe, pair the bridge,
select an area, then Start. Keep the telemetry mod installed separately. Do not
deploy this application into the game with a mod manager.

Version 0.1.3 uses the current all-around ManyLights input and emits only sources
with a fresh confirmed-clear physics verdict. Older CDT schema 1.4/1.5 uses the
legacy renderer-only path. General Ambient is a separate CDT feed and is not yet
mixed into output because its working RGB is not calibrated to local-light RGB.
No screen capture, complete lighting, physical-lumen or pixel-matching claim.
Mapping is an estimate of direction/proximity; the current version does not infer
surrounding brightness from the time of day.

Use HANDOVER.md for current test status and release hash. Physical Hue hardware
and larger areas require separate testing. Add a gameplay/real-lamp video after
the user accepts the behavior. Public branding/source-license choice and final
listing remain release tasks. No Nexus/GitHub upload is part of this build.
