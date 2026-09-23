# CrimsonHue — spatial lighting companion

CrimsonHue is the standalone Windows Hue Entertainment consumer in this repository.
Read docs/HANDOVER.md on takeover. Core code is in src/CrimsonHue.Core; WPF UI is in
src/CrimsonHue.App. Build/test/package commands are in README.md.

Consume only the neutral HTTP/WebSocket API from C:\DEV\CrimsonDesertTelemetry.
For telemetry, lighting research, camera or the merged ASI, work in that product
repository and read its AGENTS.md and docs/HANDOVER.md. Do not recreate telemetry
here. The general ambient-light feed is planned upstream; do not invent its schema.

Import existing Hue Entertainment areas and channel positions. Keep credentials
out of source, logs and releases; use the current-user protected settings store.
Use freshness checks, bounded DTLS and explicit stream ownership/cleanup. Demo
preview must never send physical output. Preserve all existing work and immutable
release packages. Separate synthetic, live telemetry and real-lamp evidence.

Test requested changes proportionately and commit completed work in this repository.
If user action or input is needed, end the turn immediately. Do not keep a
session open, poll, sleep, or spend tokens waiting; resume only on the owner's
next message. Keep handoffs short.
Keep this file and CLAUDE.md byte-identical. Previous workspace files remain preserved
in the telemetry repository's archive/crimsonhue-workspace-20260906.
