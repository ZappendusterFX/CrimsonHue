# OpenRGB development test

Branch: `codex/openrgb-parallel-preview`, based on `main` at `a137d83`.
This is a first working output prototype inside CrimsonHue Core, exercised by
the existing development test executable. It is not in the released ASI/menu,
not enabled by default, and does not change the app's synthetic Demo behavior.

The first placement is one virtual point **in front of the monitor**, `(0, 1, 0)`
in CrimsonHue/Hue room coordinates: X right, Y front, Z up. All selected RGB zones
receive that point's color. This uses the existing `LightMapper`, CDT local and
Ambient clients, freshness rules, smoothing and own-lantern handling. No new
telemetry reader or CDT schema is introduced. The saved lighting profile/custom
settings are loaded once; the test caps master RGB output at 25%.

Hue keeps its own area and mapping instance. OpenRGB devices are not appended to
the Hue area, so their presence cannot change Hue's spatial normalization. This
test opens no Hue connection, reads no Hue credentials and writes no settings.
It can run alongside the installed Hue consumer. Simultaneous physical Hue
appearance has not been independently measured during this test.

## Run from this repository

Read-only inventory, with OpenRGB's SDK server on loopback port 6742:

```powershell
dotnet run --project tests/CrimsonHue.Tests -c Release -- --openrgb-inspect
```

Explicit **physical output**, automatically limited to 20 seconds:

```powershell
dotnet run --project tests/CrimsonHue.Tests -c Release -- --openrgb-live-test --report artifacts/openrgb-live-test.json
```

For an explicitly requested **physical identification** independent of CDT:

```powershell
dotnet run --project tests/CrimsonHue.Tests -c Release -- --openrgb-identify --report artifacts/openrgb-identify.json
```

Identification sends red, green, blue and white for four seconds each at 25%,
then restores the prior state. It uses the same output ownership/cleanup session
and does not open a telemetry connection. This is separate from the app's Demo,
which still never sends physical output. It is useful when live mapping legitimately
produces black, so darkness alone cannot establish whether hardware output works.

The live command needs fresh CDT data before taking control. It targets 20 updates
per second, skips unchanged quantized colors, stops on unavailable telemetry,
and restores prior modes and color buffers in `finally`. Ctrl+C requests the same
cleanup. The report contains calculated RGB, capture sequences and SDK restoration
results, not credentials, serials or hardware paths. Choose a new report filename
for each capture. No package installation is involved in this development test.

## Devices and session limits

- Only devices with a global per-LED `Direct` mode are eligible. Modes marked as
  automatically saving to device storage are refused. Per-zone mode devices are
  parsed but deferred to a later implementation.
- Gamepads expose only their reported `Lightbar` zone to this initial test;
  player/status LEDs retain their original values. Matrix holes and duplicate
  cells are excluded. Empty addressable RGB headers are never resized.
- Mode properties and the full color buffer are captured before acquisition.
  Ownership is checked once per second and before restoration. An observed
  external mode/color edit stops output and is preserved by cleanup.
- The SDK has no exclusive lighting lease. Concurrent writers may interfere
  between checks; this is a development test, not a guarantee of exclusive access.
- Connections, requests, ACKs and writes are bounded. Device-list/detection
  notifications invalidate the connection and prevent reuse of its device
  snapshot. Cleanup is bounded to ten seconds. A disconnected server, crash or
  detected external change can prevent restoration and is reported explicitly.
- No profile, save, resize, rescan or device configuration commands are sent.
  SDK readback confirms the server's state, not optical delivery or refresh rate.

## SDK compatibility finding

The client negotiates SDK 6 when available and uses its opaque controller IDs,
write acknowledgements and updated device/mode layouts. SDK 4/5 remain supported
with their older layouts and indexes; their compatibility is covered synthetically.

The owner's OpenRGB 1.0 reports commit
`81bbe18a84c2e507006f19dd252e397e40a56bfe` (2026-09-11). Its legacy write worker
passes a controller ID into a function interpreting it as a list index for older
SDK clients. After a rescan, five devices had IDs 5–9 but indexes 0–4. The first
SDK-4 mode command was consequently rejected, with zero color updates sent and
all devices subsequently observed in their original modes. The initial diagnostic
called this an external state change; that wording was only a state mismatch,
not evidence of another application taking control.

The native SDK-6 path resolves this without changing the user's installation.
See the upstream [installed-version network server](https://github.com/CalcProgrammer1/OpenRGB/blob/81bbe18a84c2e507006f19dd252e397e40a56bfe/NetworkServer.cpp)
(`ControllerListenThread`, `index_from_id`) and the
[SDK protocol reference](https://github.com/CalcProgrammer1/OpenRGB/blob/master/Documentation/OpenRGBSDK.md).

## Evidence — 2026-11-03

Read-only Core inventory and the successful SDK-6 live test cover:

| Device | RGB entries selected |
| --- | ---: |
| MSI GeForce RTX 3080 Gaming X Trio | 3 |
| Sony DualSense Lightbar | 1 |
| Logitech G502 LIGHTSPEED | 2 |
| Razer Blackwidow V4 | 134: 116 mapped keyboard cells + 18 underglow |
| ASUS ROG STRIX X570-E GAMING | 8 |

The keyboard exposes 156 logical slots; these are not 156 physical keys.
The successful 20-second run mapped **398 frames / 296 distinct CDT captures**,
with 14–41 valid local sources per sampled frame. It sent **191 device color
updates**, and all **five prior modes and color buffers matched after restoration**.
The maximum calculated RGB component was only **0.040465** despite the 0.25 cap:
this scene had weak mapped front output. This proves the data/output/cleanup path;
visible brightness, latency, color quality and parallel Hue appearance still need
owner feedback.

Private evidence: `artifacts/openrgb-front-test-20261103/live-test-2.json`.
The first failed attempt is retained as `live-test-1.json`, with read-only follow-up
inventory in `after-start/`. No captured hardware identifiers enter the repository.
The synthetic suite covers fragmented SDK 4/5/6 replies, opaque non-index IDs,
mode/color restoration, status LEDs, external edits, invalidated device lists,
rejected ACKs, bounds/timeouts, cancellation and front-axis mapping.

## Next step after owner feedback

Owner subsequently closed the Razer applications. App processes disappeared;
Razer Experience/elevation services remained running. The 23:41 live rerun mapped
400 frames / 299 captures, reached the 25% cap and restored all five SDK states.
On the next explicitly observed live run at 23:46, all 400 calculated frames were
exactly black: 16 accepted local sources and no available Ambient. That run sent
only one color-buffer update; it does not demonstrate visible RGB response.

The explicit color identification at 23:48 completed all four stages and restored
all five devices according to SDK readback. The owner is watching keyboard, mouse,
DualSense and RTX 3080; their report of actual visible output remains pending.
Both commands were built/run successfully; the Core/session implementation is
unchanged from the 152-test pass. Private reports are in the same artifact folder:
`live-test-razer-closed-20261103-234133.json`,
`live-test-observed-20261103-234621.json`, and
`identify-observed-20261103-234831.json`. Do not claim Synapse interference is
excluded: direct hardware writes need not appear in OpenRGB's cached SDK state.

Add device selection and optional per-device room positions to CrimsonHue's
existing setup/menu, with a separate OpenRGB enable/stop state alongside Hue.
Start with the shared front preset; later place keyboard/front-low, mouse/front-
right, case/left-or-right and gamepad independently. Room positions are user
assignments, not something OpenRGB can discover. Persist stable hardware identity,
never a transient index or SDK connection ID. Keep output backends independent
when calculating spatial normalization and handling failures.

No CDT change, release package, installation, remote push or merge is part of
this prototype. Owner installation rules continue to apply when menu integration
eventually needs new packages.
