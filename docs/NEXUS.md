# Draft listing — live lamp acceptance pending

Title: CrimsonHue - Spatial Room Lighting
Category: Crimson Desert / Utilities

Summary: An external lighting companion for CrimsonDesertTelemetry. Turn live
in-game light contributions into spatial Hue Entertainment output using your
existing Entertainment area.

Adjustable distance fade: choose where local lights begin to fade and where their
contribution ends. Settings affect preview and active sync without restarting it.

Required mod: CrimsonDesertTelemetry 2.0.0, rendered lights enabled.
Other requirements: Windows x64, Hue/diyHue HTTPS API v2 bridge, color lights,
existing Entertainment area.

Install: extract the portable ZIP anywhere, run CrimsonHue.exe, pair the bridge,
select an area, then Start. Keep the telemetry mod installed separately. Do not
deploy this application into the game with a mod manager.

The initial version uses local renderer light contributions. General ambient
lighting is planned upstream and not yet consumed. No screen capture, complete
lighting, physical-lumen or pixel-matching claim. Rear coverage depends on the
renderer retaining those sources. Mapping is an estimate of direction/proximity.
Ambient-aware daytime suppression will follow the upstream lighting contract;
the current version does not infer surrounding brightness from the time of day.

Use HANDOVER.md for current test status and release hash. Physical Hue hardware
and larger areas require separate testing. Add a gameplay/real-lamp video after
the user accepts the behavior. Public branding/source-license choice and final
listing remain release tasks. No Nexus/GitHub upload is part of this build.
