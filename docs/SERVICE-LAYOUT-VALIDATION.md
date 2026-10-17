# Service placement changes — 2026-10-17

The owner changed the Wall Washers' placement in the Hue app while CrimsonHue
was running, and observed no layout-change message. Read-only inspection found
two distinct sets of positions in the same Entertainment configuration:

- Channel positions remained X=-0.4/+0.4, Y=0.8, Z=-0.4/0/0.4 for the washers.
- The washers' separate `locations.service_locations.positions` contained two
  positions each. Left: (-1,0.18595,-0.54) and (-1,0.18595,1). Right:
  (1,0.12872,-0.57494) and (1,0.12872,0.96504).
- A passive, double-read-validated immutable HUD snapshot at18:56:09 reported
  Streaming and retained the same channel geometry. A pinned GET at18:56:41
  reported the area active; collection and per-area channel layouts agreed.
  These are separate read times, not an atomic bridge/controller recording.

No earlier service-position capture was retained, so the exact before/after
service delta is not measured. The confirmed consumer defect is that service
positions were discarded and absent from every layout comparison. A change
confined to those fields could therefore never be detected.

The primary [aiohue API model](https://github.com/home-assistant-libs/aiohue/blob/main/aiohue/v2/models/entertainment_configuration.py)
also defines service positions separately from per-channel positions and describes
the latter as averages of channel members. The official API reference requires
developer login and was unavailable. Neither this model nor the observed two
positions proves a segment interpolation or projected wall-footprint algorithm.

The importer now preserves ordered service positions and individual equalization
factors independently of the bridge's channel geometry, using legacy `position`
only when `positions` is absent. Coordinates and duplicate entries are validated.
Layout comparison includes channels, memberships, brightness and service placement,
while ignoring channel/service/member enumeration order, names, active state and
signed-zero formatting. Position order within one service remains meaningful.
Start, periodic stream checks and segment identification share this comparison.

This fixes recognition of service-only placement changes. Point mapping continues
to use the bridge's channel positions; it does not construct new segment positions
from the two service points. Room's existing visible-wall calibration remains
separate. The diagnostic `--inspect-layout` now prints both sets of positions.

Validation: **115/115** tests passed (`artifacts/service-layout-tests.log`). Five
new groups cover separate import, ordering/equality, legacy and invalid input,
pre-start/identify rejection before acquisition, and a service-only edit during
a loopback stream with owned lease release and prior-state restoration. No test
connects to real lamps. Existing HUD-close cancellation regression remains in
the exact packaged controller smoke.

New controller: **Hue0.4.0-asi.20**, paired with existing **CDT2.2.5-huepanel.9**.
This includes the X fix. Hue .asi.19 and all earlier release packages are immutable.

- ZIP: `dist/CrimsonHue-0.4.0-asi.20-asi-win-x64.zip`.
  SHA256 `02DAE64D672E64E4BA79AAE0E586C3F0E6A3D9B1AA177B0DAA13D0E7AC241665`.
- ASI SHA256 `D858B78136D5F7926017E261796A96951D6C58899BDCADAB97EE6A86715C3511`.
- Exact packaged-controller smoke and expanded/ZIP validators passed; log
  `artifacts/service-layout-package.log`.
- One hidden shared D3D12 WARP test passed against the exact packaged Hue .asi.20
  and the previously tested CDT.9 renderer, including748,434 visible pixels,
  independent hide/reopen, resize and shutdown. Log in the CDT worktree:
  `artifacts/service-layout-shared-smoke.log`.
- Updated GET-only diagnostic confirms the real bridge's separate service points:
  `artifacts/service-layout-live-inspection.log`. It performs no physical output.

No real settings, bridge state, process memory, game input or installed ASI was
changed. The only live game access was the passive known UI-snapshot read.
Installation requires the game to be closed; physical recognition/room acceptance
remains for the owner after installing the new controller.
