# CrimsonHue — spatial lighting companion

CrimsonHue is the independent Windows Hue Entertainment consumer in this repository.
Read docs/HANDOVER.md on takeover. Core code is in src/CrimsonHue.Core; WPF UI is in
src/CrimsonHue.App. Branch codex/crimsonhue-asi hosts the existing controller
unshown and draws the UI as an independent D3D12/ImGui in-game overlay in
native/CrimsonHue.Asi. It ships one ASI with embedded application files, no EXE
or loose runtime companions. Users install .NET 8 Desktop Runtime x64 separately.
The owner resumed implementation on 2026-10-02. Synthetic GPU/controller tests
are separate from pending game/input/DMM acceptance. Build/test/package commands
are in README.md; read docs/ASI.md for native hosting and process-lifetime limits.

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
