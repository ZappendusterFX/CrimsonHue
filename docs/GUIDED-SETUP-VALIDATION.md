# Compact Hue panel and guided bridge setup — 2026-10-16, Codex

Owner is now in-game with CDT `huepanel.3` and Hue `.asi.10`; startup succeeds.
PID 30092, started 20:16:18, responded during read-only inspection and deployed
ASI hashes matched both packages. Screenshots show live CDT data and both HUDs.
Owner reports initial mouse drift toward the upper-left while Hue was open,
now no longer occurring, and intermittent CrimsonHue flicker. These are retained
as open game/input observations; no speculative live hooks or input changes.

Owner requested CDT's existing typography/style, an explanation of the physical
bridge link-button step, and a clear connected confirmation. Screenshots also
show an empty area selector and a generic HTTPS failure inside the game.

## Final package pair

| Package | Profile | ZIP SHA-256 |
|---|---|---|
| `dist/CrimsonHue-0.4.0-asi.12-asi-win-x64.zip` | Headless .NET 8 Desktop x64 controller | `E3962850EF14117A2CAF8132EAE8B28C8BB5D1A9C12A5733574BD7292FA0462B` |
| `artifacts/cdt-shared-overlay/artifacts/mod-manager/CrimsonDesertTelemetry-v2.2.5-huepanel.5-ModManagers.zip` | Production, research OFF, build 25477059 | `031A6B837CA5A4A2A906320AF487E8652B030020CCA1D91093A08B7323D33AB2` |

ASI hashes: Hue `28947166FC2413B4F2E10A00B3C50618DCE361B2D5E3420E02263355A651FA52`;
CDT `5A1983104D16803B1C8302D2305EE241B0863217DEAA84D6E6EEE2878CDD441D`.
These packages must be installed together with the game closed. They are built
and validated, **not installed, published or game-tested**. Earlier previews,
including preliminary `.asi.11` / `huepanel.4`, remain immutable.

## Changes and checks

- Hue uses CDT's **16-pixel body text and the same HudScale**, instead of the
  24-pixel default font. A separate font in CDT's retained atlas leaves its own
  HUD unchanged. Blue/cyan colors, borders and scaled padding follow CDT.
- Bridge connection comes first in Setup. Shortcut and telemetry settings are
  collapsed below it. Find applies/validates the IP and begins discovery as one
  controller action; invalid input cannot fall through to an old bridge address.
- Found/link-button/pairing/connected/failed states are distinct. New pairing
  explicitly requires pressing the round button on the physical bridge and then
  **Trust bridge & pair**. Matching saved address/certificate reuse existing
  credentials. No credentials are sent to a changed identity without pairing.
- Green CONNECTED appears only after authenticated Entertainment-area retrieval,
  also in the header on every tab. An unconnected/empty list has an explanatory
  placeholder. Start sync and segment identify require a verified connection.
- Errors update bridge status instead of leaving Looking for bridge displayed.
  Collapsed diagnostics expose bounded type/HRESULT/native error codes, never
  exception messages, keys, API bodies or protected settings.
- Hue keeps the last complete UI snapshot when its mutex is briefly busy. The
  previous implementation skipped its panel for that frame. This removes one
  plausible flicker cause; GPU-busy frame skips and actual game flicker remain
  separate acceptance.
- **86/86** nonphysical Hue tests pass, including same-address/same-certificate
  reuse and changed-identity rejection. **13/13** focused CDT input/overlay/HDR
  regressions pass. Exact final controller package and shared GPU host pass in
  Room and unconnected Setup, including software cursor, area placeholders,
  calibration commands, hide/reopen and resize. Screenshots visually inspected:
  `artifacts/hue-guided-final/room.png`, `setup.png` (748580 visible pixels).
- Maintained package builders/validators pass both exact payload contracts and
  ZIP/staging byte comparisons. No running-game file/hook, saved settings or
  physical lamp was changed.

## Bridge evidence and remaining work

Read-only diagnostics outside the game successfully performed the exact Find
probe and authenticated area retrieval against the saved real bridge:
**Hue Bridge, certificate matches saved pairing; Spieltisch, six channels,
inactive**. Existing DPAPI credentials are valid. `--inspect-connection` and
`--inspect-layout` never drive lamps or print keys. Application firewall inspection
found inbound Allow rules and no game-specific outbound Block; no rules changed.

This does **not** prove the in-game HTTPS error is fixed. Its inner cause was not
available from the old generic message. The new explicit status/error codes are
needed if it recurs. There is no reason to recreate the Entertainment area or
invalidate the existing pairing based on current evidence.

Next: at a game shutdown, install the final pair and verify hashes. Use Find/
Reconnect or Refresh areas to confirm in-game CONNECTED and Spieltisch. If it
fails, capture Connection details before changing transport or security settings.
Then continue Room calibration; physical output remains explicitly initiated.
Do not repeat declined A/B restarts or auto-start physical sync. Keep the
transient mouse drift and flicker observations until actual follow-up acceptance.
