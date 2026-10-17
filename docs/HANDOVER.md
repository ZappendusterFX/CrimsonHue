# Current checkpoint — held input across HUD transitions, 2026-10-17, Codex

Owner reports endless sword attacks after an uncertain HUD close sequence,
cleared by reopening/closing. Exact game route/order was not traced. Confirmed
adapter defect: capture could swallow releases for presses already given to the
game. Maintained panel now tracks legacy/raw mouse/keyboard separately and waits
for complete game-owned releases before capture. Hide releases active focus and
clip immediately, finishes HUD-owned pairs, and resets ImGui before reuse. Worker
maintains the handoff with every overlay hidden. Held-input reminder added.

Mocked actual-adapter regressions pass: queued/dual-route/raw-only/mixed packets,
keyboard/modifiers, hide while clicked, no-frame/worker completion, focus and old
cursor checks. A targeted mutant fails at the queued-release assertion. CDT12/12
focused tests and exact-Hue.20 hidden shared D3D12 WARP/pixel/hide/resize smoke
pass; screenshot reviewed. No synthetic game key-ups or input API/device changes.

New immutable **CDT2.2.5-huepanel.10**, production/researchOFF/build25477059; keep
**Hue0.4.0-asi.20** unchanged. Package validators and source-manifest match pass.
Evidence/hashes: [INPUT-HANDOFF-VALIDATION.md](INPUT-HANDOFF-VALIDATION.md) and CDT
worktree docs/HUE_INPUT_HANDOFF.md. No real input, installation, settings, process
write or lamp output. Next: owner exits game, installs CDT.10 only, then checks
sword/HUD transitions. This is proven for the adapter's Win32 routes; physical
acceptance and independently polled/buffered game paths are not established.

# Previous checkpoint — detect separate Hue placement changes, 2026-10-17, Codex

Owner repositioned the washers in Hue; no layout-change warning appeared. Live
GETs show separate service placement at X=-1/+1, Y=0.18595/0.12872, while the six
washer channel coordinates stay X=-0.4/+0.4,Y=0.8,Z=-0.4/0/0.4. Passive immutable
HUD snapshot confirms Streaming with those unchanged channel coordinates. Earlier
service-position values were not captured, so do not claim a measured delta.
The confirmed consumer blind spot: ParseArea discarded service position(s) and
the stream compared only channel geometry/membership/combined brightness.

Now preserve ordered service positions and individual brightness factors; validate
current positions/legacy position; compare both representations consistently at
Start, periodic stream checks and identify. Ignore mere enumeration/display state
and signed-zero formatting. --inspect-layout prints both sets. Point mapping still
uses the bridge's channel positions; no invented endpoint-to-segment interpolation
or automatic wall calibration. Actual wall geometry remains under Room.

Core115/115 (five new groups incl. service-only loopback stream stop/owned restore),
exact .asi.20 controller smoke, package validators and hidden paired CDT.9 D3D12
WARP/pixel/hide/resize smoke pass. Latest immutable pair: Hue0.4.0-asi.20 + existing
CDT2.2.5-huepanel.9. Includes the X fix below. Evidence/hashes:
[SERVICE-LAYOUT-VALIDATION.md](SERVICE-LAYOUT-VALIDATION.md). No install, real settings
change or physical output. Next: owner installs after game exit and checks service
placement detection; Point-vs-service geometry remains explicit, not silently fixed.

# Previous checkpoint — HUD X keeps sync running, 2026-10-17, Codex

Owner reports mouse X stops lighting while Ctrl+Shift+H keeps it running. Confirmed:
the panel hides and sends close; the controller explicitly stopped before saving.
close now saves without cancelling/detaching the session. Shortcut and Hide retain
their behavior; explicit Stop and host/error cleanup are unchanged. Updated the
maintained panel footer and user guides; synchronized the existing CDT worktree.

Core110/110, exact packaged-controller smoke (including close/Stop cancellation
regression), package validators, CDT12/12focused input/overlay checks and one
hidden paired D3D12 WARP/pixel/hide/resize run pass. Screenshot reviewed. Final
immutable pair: Hue0.4.0-asi.19 + CDT2.2.5-huepanel.9, production/researchOFF,
build25477059. Paths/hashes/evidence: [HUD-CLOSE-VALIDATION.md](HUD-CLOSE-VALIDATION.md).
No installation, real settings change, physical stream or game input this turn.
Next: install with game closed, then owner checks X, shortcut reopen and continued
lamp output. This fixes the explicit X stop, not every possible stream failure.

Room context: owner added two Play Bars; GET-only area Spieltisch now imports
four lights/eight channels (two bars + two three-channel Wall Washers), all in
front. The washers' bridge names are both Back; that name does not mean behind
the owner. Layout changes still stop a running stream: Stop, edit/save in Hue,
Refresh areas, Start; no game restart needed for an area refresh. Wall area uses
the separate Room calibration, while Point uses imported Hue positions.

# Previous checkpoint — recommended lighting and simpler HUD, 2026-10-16, Codex

Owner prioritizes out-of-box behavior and rationalizing the many interdependent
sliders. Implemented Lighting / Room / Setup / Advanced in CDT's hosted panel.
Normal controls: Overall brightness, Local light effects, Background light, and
Quick/Balanced/Calm response. Existing 28 detailed fields remain in Advanced.
Recommended uses a coherent neutral profile with no cutoff/artificial floor;
preserved Custom settings are independent. New installs use Recommended; existing
settings stay Custom until explicit Use Recommended. Save/Start/normal close save
both modes. Partial preset imports and legacy advanced commands preserve unrelated
Custom fields. Diagnostic replays must resolve Lighting preferences as well as
Mapping; the maintained read-only live mapping test now does this.

The HUD explains known missing/zero input and settings that remove background.
Lamp bars show calculated channel commands, grouping segmented lights; positive
sub-1% output remains visibly nonzero. Custom mode hides inactive recommended
values. Segment identification has a separate status. Raw data remains Advanced.

Noon read after owner reported12:05: working Ambient18.38286, fill0.395092 of0.4
(98.77%), all6channel peaks~66.15% encoded; 20941sent,0cleared,0missing. Owner says
clear weather. This is the running Hue16 controller's calculated state, not measured
lamp brightness. Clock time is optional plausibility context, not necessary for
mapping. Feed remains sky times camera visibility, not all scene illumination.
No Computer Use, screenshots of the game, process writes, or real settings changes.

Core110/110 and exact packaged controller tests pass, including preservation
regressions. Both final packages are built and validated: Hue0.4.0-asi.18 and
CDT2.2.5-huepanel.8 (production, researchOFF, build25477059). CDT13/13focused tests
and six hidden GPU views against packagedHue18 pass; source copies match. See
[RECOMMENDED-LIGHTING-VALIDATION.md](RECOMMENDED-LIGHTING-VALIDATION.md) for exact
package evidence/hashes and remaining physical acceptance. Intermediate Hue17
and older packages remain immutable. No game deployment was performed.

Remaining: physical brightness/profile/cursor acceptance, independent stream-stop
cause, and reliable own-lantern source identity. This change is not a claimed fix
for missing camera sky visibility or complete environment-light measurement.

# Previous checkpoint — live controller darkness diagnosis and cursor fix, 2026-10-16, Codex

Owner reports repeated stream stops and dark washers in a visibly lit dawn scene;
then moves game time to noon. No Computer Use: owner explicitly declined it.
Only @oai/sky import happened before refusal; no window listing/capture/input.

Read-only five-second replay with saved settings (artifacts/lighting-dawn-asi16.json)
found fresh Ambient around0.6167, producing0.15885 encoded white on all6channels.
This is NOT actual output: live settings differ materially from saved settings.
A narrowly scoped passive CLRMD reader, artifacts/hue-live-state/, reads ONLY
AsiEntry.state immutable credential-free byte[] (double-read/parse validation),
without suspension, heap scan, injected calls or writes. Live snapshot live-1.json
23:05:39/PID9400: Streaming,12743sent,0local-cleared,0Ambient-missing; brightness100%,
AmbientSensitivity0.96675, AmbientCutoff28.488 (!), AmbientReference4, mix40%.
Thus any reported Ambient level <=28.488 is deliberately removed in Hue before
transmission. At this particular later snapshot CDT also reports skyvisibility0,
so local RGB/working level/fill are0 despite skyRGB20.77/26.66/42.32. Do not infer
that visibility was0 in the earlier dawn replay or that resetting cutoff alone
fixes this distinct upstream condition. No current controls/settings changed.

Read-only bridge GET: selected6channel area active; both Back washers mode
streaming, reported dimming.min_dim_level0.05 (%). This normal-dimming capability
is NOT a measured Entertainment cutoff. Other bulbs report2.0. No physical
threshold test and no automatic floor/brightness boost is justified yet.

Cursor source now isolates established raw mouse input from OS cursor warps,
scales client/render coordinates, and keeps capture on repeated SetInputWindow.
Latches only after an actual valid WM_INPUT mouse packet (registration alone can
be buffered and is insufficient); preserves legacy fallback, reset on hide/focus.
Maintained source is native/CrimsonHue.Panel, NOT historical Asi/overlay_input.cpp.
CDT worktree agent validated13/13focused tests and exactHue16sharedGPU smokes;
production huepanel.7 package/docs/commit are finishing separately there.
Installed game remains unchanged. Physical cursor acceptance pending.

Stream-stop audit: one failed periodic REST check or DTLS Send ends the stream;
cleanup can obscure the original exception. Neither is proven to be the observed
cause. Current live snapshot shows a running stream. Next owner action: reset
Ambient black threshold to0, then read actual live state at noon again; separately
investigate zero camera-sky visibility with CDT's proven source, not guessed fill.
# Current checkpoint — owner identifies swinging personal lantern, 2026-10-16, Codex

Hue .asi.16 and CDT huepanel.6 are now installed: read-only hashes match the
packages; game PID9400 started22:45:23, supported build25477059. Owner confirms
stationary camera rotation is now smooth, but walking still strobed. Owner then
identified their swinging personal lantern as the cause and switched it off.
They ask for ignoring it or a better treatment. This is owner-observed causal
evidence; no new physical output or game/settings changes were made here.

Before owner said STOP, bounded read-only CDT recording completed at
artifacts/motion-streams-asi16/:1188 envelopes,303 GPU captures,20seconds. The
separate mapped-output recorder was interrupted on STOP and has no completed
report. Do not present that run as actual outgoing Hue packet measurement.

The motion recording cannot identify the lantern: no bright source within3gu,
and bright sources within4gu appear in only27frames, remaining almost fixed in
world space while the player passes3.89gu. Earlier standing capture's two nearby
spot contributions are absent. Do not infer when the lantern was toggled from
this record or use those unrelated near fires as a lantern signature.

Current CDT rendered/upstream contracts contain no owner/attachment/entity ID;
sampleIndex is per-capture, not stable identity. Existing upstream research found
the player's equip-lantern scene object but has no proven join to GPU light
contributions (LIGHT_IDENTITY_RESEARCH.md, controlled moving-source section).
No hardcoded slot/color signature or automatic proximity exclusion is justified.

Recommended product behavior, NOT implemented: own-lantern contribution defaults
to excluded from spatial mapping; optional low, steady fill preserves its presence
without following every swing, plus original behavior if desired. Put treatment
in Hue's HUD, with neutral verified ownership metadata supplied by CDT. A generic
near-player attenuation control is possible immediately but would also attenuate
nearby fires/lamps and must not be labelled reliable own-lantern recognition.
Next work is targeted producer identification before a lantern-only exclusion.
Ambient response remains separate and unresolved. No new package this turn.

# Previous checkpoint — spherical flow replaces hard axis cuts, 2026-10-16, Codex

Owner reports strong motion flicker, one washer dark except occasional reciprocal
left/right switching, and explicitly requests smooth flow across ALL directions.
They clarified that "interpolation on a sphere" expresses the intended behavior,
not a mandated algorithm. Implemented angular-distance weighting on the unit
sphere, replacing X/Y sign cuts and the tangent-plane hemisphere rejection.
Point lights and calibrated wall regions use the same spherical kernel. Full-room
geometric affinity determines compensation independently of cap membership, so
entering/leaving a footprint cannot switch its denominator. Wall contributions
are integrated over the calibrated region; full-region rather than covered-patch
normalization can change intensity. No invented physical calibration.

HUD has 28 numeric controls, no axis-cut switches; Source influence radius now
supports 1..180 degrees. Existing numeric settings are retained. The old Separate*
keys remain readable in settings/presets but are ignored by mapping. Default
edge softness remains 25%; explicitly choosing 0 still gives a sharp cap edge.
Temporal smoothing remains independent and was disabled for continuity checks.

**102/102 nonphysical tests passed.** Exact packaged headless-controller smoke
and seven-file ZIP/staging validation passed. Offline old/new actual-DLL replay
of the imported six-point geometry: at 0.01-degree steps through center the worst
channel jump fell from 0.536791 to 0.000074. A 90-degree rotation of source AND
room preserved new output exactly; old mapping differed by up to 0.529427.
These are synthetic channel commands, not physical-lamp measurements.
Evidence and hashes: [SPHERICAL-FLOW-VALIDATION.md](SPHERICAL-FLOW-VALIDATION.md).

New immutable Hue **0.4.0-asi.16** is built, NOT installed or game/lamp-tested:
dist/CrimsonHue-0.4.0-asi.16-asi-win-x64.zip
SHA256 6CC951F2CC0BFFD315A2FC0525089DF8783CA2CD52A2FC1A94D69190A6DD3EDC
Keep installed CDT **2.2.5-huepanel.6**; only Hue needs replacing with game closed.
No game files, real settings, credentials or physical stream were changed.

Ambient is still unresolved. Read-only stationary capture: 708/708 fresh local
and Ambient envelopes, no camera orientation changes, no side swap reproduced.
Working Ambient level was only 0.003675..0.005619. Independent mapper using saved
settings produced roughly 0.3..0.45% encoded Ambient on ALL channels, while local
orange sources lit one side. This explains weak fill, not a side-specific Ambient
failure. The raw feed is explicitly not exposure-normalized; do not invent a
schema, boost brightness silently, or claim that the sphere fix fixes Ambient.

Also corrected OUR earlier audit regression: physics-ray-fan visibility age is
age at envelope PUBLICATION, despite its legacy field name. Adding GPU capture
age counted it twice. Now add only transport/display age, preserving observed
delay across clock changes. The earlier checkpoint's contrary interpretation is
wrong and superseded. This bug dropped zero extra sources in the stationary
recording, so it is not evidence explaining that observation.

Next: install Hue .asi.16 after game exit; physical flow acceptance and proper
Ambient response remain pending. Do not resume old strict-axis requirements.

# Previous checkpoint — Point/Wall setup ready, 2026-10-16, Codex

Owner clarified the Hue term was ColorCast and asked whether implementation
needed more input. Implemented the software setup autonomously. Physical wall
direction/extent still needs the owner's later observation; no saved real room
configuration, credentials, bridge output or game files were changed.

Room now offers Point (Hue position) / Wall area per light, imported segment count,
ColorCast guidance and only the controls relevant to the selected mode. Point
mode preserves wall settings for switching back; explicit Reset removes them.
New segment indices in an existing calibration are marked NeedsReview, show an
explicit placement-confirm button, and contribute no local light until confirmed
instead of silently falling back to points. Existing segments and Ambient keep
working; shared channels still combine their members. Flags persist locally,
legacy saved segments default to accepted, presets exclude all room calibration.
Removed segments are reported. Merely saving/editing another angle does not
confirm a new segment. Hue channel identities and imported positions stay intact.

**98/98 nonphysical tests passed**, including mixed rooms, changed membership,
review/confirmation, Ambient independence, point fallback and settings migration.
Exact final packaged controller smoke verifies segment summaries and Point/Wall
switches preserving placement. Matching CDT production-renderer GPU smoke passed
software cursor, readback (748580 pixels), hide/reopen, resize and cleanup; image
artifacts/room-modes-final/room.png visually checked. No live-lamp acceptance.

Final immutable pair, built and validated, **NOT installed/game-tested**:
- Hue **0.4.0-asi.15**, headless .NET 8 Desktop x64:
  dist/CrimsonHue-0.4.0-asi.15-asi-win-x64.zip
  SHA256 CBD03CA5F901811370DFF0F9D359738DB8F8E614FF94BBAF6371D8F1CB8F6107
- CDT **2.2.5-huepanel.6**, production/research OFF, native build 25477059:
  artifacts/cdt-shared-overlay/artifacts/mod-manager/CrimsonDesertTelemetry-v2.2.5-huepanel.6-ModManagers.zip
  SHA256 204C5BD2B50008F71428BFAA2C1FF5E8A2929D8F4217F4BF12030141242F481C

Includes the previous motion smoothing/stream Ambient diagnostics and both audit
freshness fixes. Builders validated exact payloads and ZIP/staging bytes. The
preliminary .asi.14 is preserved, superseded by .15 correcting a remaining old
CDT-version reference in its packaged user guide. Logs: room-modes-tests.log,
room-modes-hue-final-package.log, room-modes-cdt-package.log, room-modes-gpu.log.
All ZIP/ASI hashes: artifacts/room-modes-package-sha256.txt. AGENTS/CLAUDE match.

Next: with game closed install BOTH matching packages, then calibrate visible
wall regions using the explicit segment tests/preview. Owner observations needed
only for actual wall direction, span and segment patches. Physical Ambient and
motion acceptance remain pending. No installation or physical test was performed.

# Previous checkpoint — mixed-light review and freshness correction, 2026-10-16, Codex

Owner requested a short audit of CrimsonHue and CDT while away, specifically
ordinary lights, wall washers, and Hue's native Entertainment features. Review
retains raw CDT input and smoothing after per-channel room mapping. No game,
bridge settings, physical stream, installed ASI or immutable ZIP was changed.

Confirmed consumer bug fixed: physics verdict age is measured at the light
capture. TelemetryState.Read previously added only time since the newer envelope,
so a republished 200 ms-old capture could keep a 350 ms-old-at-capture clear verdict
alive beyond the 500 ms limit. It now adds capture age with producer/monotonic
fallback. Regression covers fresh, expired and clock-skew cases. Added a mixed
ordinary-bulb/three-segment-washer regression retaining channel IDs and side
separation. **95/95 nonphysical tests passed**; log:
artifacts/review-visibility-freshness-tests.log. No lamp acceptance claimed.

CDT audit found a separate optional-smoothed-feed bug: repeated GPU captures with
only changing physics metadata were rejected as repeated-source-changed. A narrow
fix and regression are in its existing isolated product worktree; this never
explained CrimsonHue's raw-input motion flicker. See that worktree's HANDOVER.

Room model already supports mixed types per service: no enabled calibration means
the imported channel point; an enabled calibration averages over a visible wall
region with independent segment offsets/heights. USER-GUIDE now explicitly
explains mixed areas. Hue channel IDs, member segment indices and brightness
balance are imported; model/archetype/gradient capability is not yet imported for
friendly device classification. Segment count alone must not imply wall-washer
geometry. Confirmed follow-up bug: after changed Hue segmentation, GetOrCreate
can show a new segment under an enabled wall setup while Samples still consults
only the saved segment list and falls back to point mapping. Reconcile imported
membership on refresh and mark new segments uncalibrated; this UI/controller
change is not part of today's narrow freshness fix.
The owner's prior bridge import found two lights with three channels
each, not six independent physical lights.

Next product work, not implemented in this audit: explicit Point / Wall area
selection and detected segment count per light, hide inactive wall controls, then
guided identify/preview/calibration. Use device capability metadata to suggest a
mode while retaining user choice; even an ordinary lamp can illuminate a wall.
Per-light transition/brightness overrides would help mixed hardware; currently
transition is global and imported brightness balance is honored. Profile before
optimizing repeated wall-sample allocations or hidden-HUD snapshot serialization.
New-user setup still seeds a development bridge IP; Find probes that address,
not a network search, so address entry/discovery should be made explicit.

Hue reference checked: the official API-v2 announcement explicitly describes
gradient Entertainment and independently addressable segments:
https://developers.meethue.com/new-hue-api/
Signify describes ColorCast, placement-dependent projection area and Hue-app 3D
position/direction controls; do not claim Hue has no direction capability. The
public API reference was unavailable in this review, so no automatic footprint
or aim field is assumed beyond the actual imported channel data:
https://www.signify.com/global/our-company/news/press-releases/2025/20250617-immersive-entertainment-and-a-new-ai-assistant-from-philips-hue
The user's recalled feature name is not yet certain; segmentation/gradient is
the relevant verified capability, not proof of the exact term previously used.

The earlier .asi.13 ZIP remains immutable and does NOT contain this new freshness
fix. No new package produced in this audit. Physical Ambient appearance and .13
motion acceptance remain pending; do not present source tests as lamp acceptance.

# Previous checkpoint — motion smoothing and stream diagnostics ready, 2026-10-16, Codex

Owner cancelled the Claude handoff and resumed here. Owner deliberately ran and
turned the camera during our read-only sample. With installed Hue .asi.12 + CDT
huepanel.5, the eight-second sample has 169/169 fresh local AND Ambient reads after
two connection-startup reads, 117 light captures and 16 sky captures. No full-frame
dropout occurred while moving. Confirmed-clear count changed 132 times (52..83);
Ambient stayed positive (1.06..18.91). This measures a separate consumer, not the
actual in-game Hue controller or physical lamps. Do not claim Ambient fixed.

Completed the committed smoothing draft: SmoothingMs now applies to all feeds and
strict boundary modes. Local RGB is smoothed in sRGB space before decoding and
mixing independent Ambient. New spatial targets still respect boundaries; old
color fades briefly across a side/visibility change. Zero transition is immediate.
Stream timing uses real elapsed time. Missing local telemetry clears/reset both
layers, including a post-handshake freshness failure; master/channel limits apply
immediately after smoothing. Existing saved settings/default 100 ms are preserved;
250..400 ms is an optional slower washer response, not a measured room calibration.

Added actual-stream Ambient inclusion to the existing HUD text and last mapped
Ambient level/fill + sent/cleared/missing counters in CDT raw input. This uses
samples passed to the mapper, published after Send, not the separate UI reader.
Fill is before local mixing/gamma/master and never claims measured lamp output.

**93/93 nonphysical tests passed**, including six motion/transition checks and a
mock-stream test matching diagnostics to actual packet colors. Three spatial
fixtures now explicitly request zero transition. Managed/native build, exact
packaged controller smoke and seven-file ZIP/staging validator passed. No new GPU
or live-lamp acceptance claimed; CDT renderer/panel code was unchanged.

New **CrimsonHue 0.4.0-asi.13** ZIP built, NOT installed. Keep CDT huepanel.5.
ZIP: dist/CrimsonHue-0.4.0-asi.13-asi-win-x64.zip
SHA-256: 4736E01A06D20E083767C2E13119BAE9F1C6C4BE7BAB22548479BA7C98EFACCF
Details, ASI hash and evidence: [MOTION-SMOOTHING-VALIDATION.md](MOTION-SMOOTHING-VALIDATION.md).
All previous ZIPs, real credentials/settings and game files preserved. CDT's
isolated worktree remains clean. AGENTS/CLAUDE byte-identical.

**Next:** game shutdown, install only Hue .asi.13, then check movement with
Output -> Local light transition (100 ms saved; try 250 ms if desired). If Ambient
still seems absent, inspect its inclusion status and raw stream diagnostics;
neutral Ambient tint saturation 0% means white fill, not inferred sky color.
Do not repeat broad A/B tests or restart telemetry research. Input is released
when the HUD hides (owner confirmed); earlier cursor drift is a separate observation.

# Previous checkpoint — motion flicker / Ambient handoff to Claude, 2026-10-16, Codex

Owner requests an immediate handoff to Claude and a clean committed worktree.
Stop implementation here; the smoothing draft below is NOT a release candidate.

Owner is now running the final **Hue 0.4.0-asi.12 + CDT 2.2.5-huepanel.5** pair.
Read-only installed SHA-256 checks match GUIDED-SETUP-VALIDATION.md; game PID
26424 started 21:06:19 and responds. Owner reports real wall-washer output, with
severe strobing ONLY while moving; stationary output is stable. Owner also says
Ambient is missing. Input clarification: hiding Hue restores mouse/keyboard;
capture while the interactive HUD is open is expected. Do not pursue a stuck-input
bug from that report. Earlier cursor drift remains a separate observation.

**Found cause:** LightMapper bypasses SmoothingMs whenever ConfirmedVisibleOnly
or either strict spatial boundary is active. All three apply to the owner's setup;
saved SmoothingMs=100 therefore has NO effect in the installed version. Visibility
or mapped-direction changes are transmitted immediately at about 30 updates/sec.
Whether producer gaps or boundary crossings dominate the actual motion flicker
has not been measured. Do not change CDT physics visibility on that assumption.

**Ambient evidence:** the actual CDT /v1/ambient GET is available/fresh. Existing
read-only --live-ambient --live-mapping checks pass: 40/40 fresh reads, 10 sky
captures in four seconds, relative level about 2.33, and calculated Ambient-only
RGB about (0.2743,0.2743,0.2743) on every channel with saved 60% master brightness.
This proves standalone reception/mapping, NOT the in-game Hue controller's state
or physical Ambient output. In-game missing Ambient is still unresolved. Review
StreamingSession's current full-black behavior when local telemetry is unavailable;
do not invent Ambient data or assume this is the proven cause.

**Committed draft:** LightMapper now smooths the local layer in encoded brightness
space for every feed/boundary mode, decodes it before mixing independent Ambient,
and applies the current master limit afterwards. Zero transition remains immediate;
full missing telemetry still resets/clears in StreamingSession. A brief old-color
tail across a strict boundary is intentional for the proposed anti-flash behavior.
UI renames Legacy smoothing to Local light transition and explains the tail.
The old immediate-hidden test now explicitly uses SmoothingMs=0.

Draft compiles; **83/86 tests pass**. Three existing tests still require immediate
boundary/origin switching with nonzero smoothing: "Strict boundaries keep every
opposite quadrant exactly black while the camera turns", "Recorded nearby fires
switch front or rear with the chosen player-camera origin at 15 GU", and "Boundary
switches and room offset act immediately without altering Ambient or raw input".
These are an unfinished contract/test update, not accepted regressions. Add real
motion/dropout smoothing tests, retain spatial target checks with zero transition,
verify stale/reset/master-limit behavior, and update the old README/Ambient docs
that still describe bypassed smoothing. No new default or owner setting was written.

Evidence (untracked artifacts): motion-before-readonly.log (88/88 before draft,
including the two live read-only checks), motion-smoothing-draft-tests.log (83/86).
No new package, installation, game mutation, physical output or upstream edit.
Existing .12/.5 ZIPs remain immutable. CDT worktree artifacts/cdt-shared-overlay
remains clean on codex/hue-shared-overlay; original CDT research is untouched.

**Next for Claude:** finish the local smoothing behavior/tests and diagnose actual
in-game Ambient state. Package a fresh Hue version only after verification; replace
ASIs only with the game closed. Owner explicitly wants this handoff now.

# Previous checkpoint — compact CDT-style Hue setup ready, 2026-10-16, Codex

Owner is in-game with `.asi.10` + `huepanel.3`; startup works. PID 30092 was
responsive, deployed hashes matched, screenshots show live CDT and Hue UI.
Remember owner reports: cursor initially pulled upper-left with Hue open, now
stopped; intermittent Hue flicker. No full input/flicker acceptance claimed.

Owner wanted CDT's typography/style, physical bridge-button guidance and a
connected confirmation. Implemented CDT 16px body text/screen scale, blue/cyan
palette, bridge-first Setup with shortcut/telemetry collapsed; atomic Find
applies the IP and discovers the bridge. Matching saved pairing reused, new
pairing explicitly prompts round bridge button + Trust bridge & pair. Green
CONNECTED requires authenticated areas and shows on every tab. Empty/disconnected
area selectors explain why; failures show status and safe diagnostic codes.
Sync/identify require connection. Cached last snapshot removes a panel-only
frame skip under mutex contention (possible flicker cause, game confirmation pending).

Final pair: **Hue 0.4.0-asi.12 + CDT 2.2.5-huepanel.5**, production/research OFF,
build 25477059. Built/validated, not installed or game-tested. 86/86 Hue, 13/13
focused CDT checks; exact final GPU Room/Setup/cursor/resize smoke passed.
ZIP/ASI hashes, source/evidence and screenshots: [GUIDED-SETUP-VALIDATION.md](GUIDED-SETUP-VALIDATION.md).
All old packages preserved; original CDT main/research untouched.

Real bridge's unauthenticated probe and authenticated area GET both succeed
outside the game with saved protected credentials: pin matches, Spieltisch,
6 channels, inactive. Owner's current game screenshots show generic HTTPS
failure and no areas. Its in-game cause is **not yet established/fixed**; don't
mistake standalone success or new UI for game connection acceptance. No firewall,
profile, game files/hooks or lamps were changed. No need to recreate the area.

**Next:** at game shutdown install both final packages, verify hashes, confirm
CONNECTED + Spieltisch in-game. If HTTPS still fails, use Connection details
before transport/security changes. Then calibrate washers in Room. No declined
A/B restarts or implicit physical output. Retain mouse drift/flicker observations.

# Previous checkpoint — startup cursor crash fixed, 2026-10-16, Codex

Owner installed `.asi.10` + `huepanel.2`; PID 8240 hung at startup. Exact deployed
hashes matched. Live stacks showed a native exception in CDT's ImGui software
cursor, followed by Streamline MiniDumpWriteDump waiting with 113 threads suspended.
Matching `.text`/link map resolves exception RVA 0xbc306 to
ImFontAtlasGetMouseCursorTexData. ClearInputData had deleted the cursor rectangle;
old hidden-window smoke never drew the software cursor. This was our missed test
case; `.2` failed actual startup acceptance. No live file/hook/process was changed.

Fixed CDT retains font-source bytes and cursor metadata; only ClearTexData runs.
Forced software-cursor test fails before fix with 0xC0000005 and passes after,
including resize/hide/reopen. Owner also requested reliable mouse/keyboard return:
Hide/Close clears capture/pending events, restores cursor clipping outside the
WndProc mutex, and hidden frames disable ImGui input/focus. Mocked User32 tests
verify keyboard/mouse/raw-input passthrough, reentrant clipping, reopen/focus loss.
13/13 focused input/overlay/HDR regressions passed; package code matches tested objects.

Replacement: **CDT 2.2.5-huepanel.3 only**; keep installed Hue **0.4.0-asi.10**.
ZIP is under `artifacts\cdt-shared-overlay\artifacts\mod-manager`, SHA-256
`A401EA8E0AE8C12136E2816AB774ED9AAF66B9B4A86C90CCD67AB3F18447D1F1`.
ASI SHA-256 `51EDA7502E5E2384112084C8C25A2A2A9A9564FF8E7027D15ADBD8C3DDD860B8`.
Maintained builder/validator passed. Not installed/game-tested yet. Full evidence:
`artifacts\cdt-shared-overlay\docs\HUE_CURSOR_HANG.md`; captures `artifacts\hang-8240`.
Native stack reader source is in CDT tools/HangStacks, never packaged.

**Next:** owner ends the already crashed/hung game; replace CDT with `.3` while
closed, verify hash, then one startup/Hue hide/game-input check without ReShade.
Do not resume preexisting suspended threads, patch the live fault, repeat declined
A/B restarts, or auto-start physical lighting. Physical washer acceptance remains pending.

# Previous checkpoint — CDT-hosted Hue panel ready, 2026-10-16, Codex

Owner explicitly authorized the UI-hosting change while playing. Implemented:
CDT owns the sole renderer/ImGui context/input; CrimsonHue is a headless ASI
with protocol-1 bounded snapshot/command exports. Hue mapping, protected settings,
wall/segment calibration, preview and stream ownership/cleanup remain separate.
No graphics imports/hooks remain in the production Hue ASI. Telemetry API unchanged.
A final review fixed a worker-shortcut/render visibility race before the final pair.

Final pair: **CrimsonHue 0.4.0-asi.10** and **CDT 2.2.5-huepanel.2** (production,
research OFF, build 25477059). Both ZIPs validated, not installed/published/game-tested.
Hue 85/85, CDT managed 79, native 39/39, exact Hue controller + CDT shared GPU
smoke passed (850889 pixels, independent hide/Hue-only reopen/resize/queue cleanup).
Source paths, ZIP/ASI hashes, screenshot and evidence limits:
[SHARED-OVERLAY-VALIDATION.md](SHARED-OVERLAY-VALIDATION.md).

CDT changes are in its isolated worktree at
`C:\DEV\CrimsonHue\artifacts\cdt-shared-overlay`, branch `codex/hue-shared-overlay`
from `3284601`; preserve that checkout. Original CDT research/untracked files untouched.
Panel source is maintained in `native/CrimsonHue.Panel`, synchronized into CDT
using `scripts/Sync-CdtPanel.ps1`. Historical independent-overlay sources remain
for developer reproductions; production no longer links them. Old packages preserved.

No running-game module, game hook or physical lamp was changed. ReShade remains
out of this test. **Next:** owner closes game, then install both final packages
and verify deployed hashes for one combined game/input check. No more declined A/B
restarts. Physical washer acceptance is still pending; do not auto-start sync.

# Previous checkpoint — overlay hook race reproduced, 2026-10-16, Codex

Owner installed `.asi.8` and entered the game, PID 11096, build 25477059. Both
CDT HUD/markers and CrimsonHue UI are reported invisible. Read-only module checks
confirmed the exact `.asi.8` hash and CDT 2.2.4 ASI hash; Windows System32 DXGI is
loaded, not the removed game-directory ReShade proxy. CDT health reports playing,
supported/tested build and no error. Snapshot sequence 6141 -> 6144 progressed;
rendered/upstream captures were available (111/137 sources in that sample).
CDT's graphics log remains waiting for a swapchain; CrimsonHue's log reports
D3D12/SDR ready and one submitted frame. Submission does not prove visibility.

Owner refuses repeated manual A/B starts and explicitly authorizes live testing.
Do not ask for the declined disable-CrimsonHue comparison again. No installed
file, live hook, game process or physical light was changed during this diagnosis.

Both ASIs install independent static MinHook detours from worker threads on the
same DXGI functions. Their module-local locks do not coordinate CreateHook with
EnableHook across DLLs. New developer-only HookRaceTests deterministically
reproduces Create A / Create B / Enable A / Enable B: first hook receives 0 calls,
second 1. Serialized Create/Enable pairs receive 1 call each. The original return
value is preserved in both cases. This proves a lost-hook mechanism that the old
foreign-before/after tests cannot catch; it does not establish the exact live
interleaving or fully explain CrimsonHue's unseen submitted frame. Test uses two
separate MinHook DLLs and an isolated local function; no GPU/network/game/lamps.

Owner asks whether the Hue HUD should live in mandatory CDT. Recommendation,
not yet implemented/accepted: CDT owns the only graphics renderer and exposes
an optional CrimsonHue panel; the independent Hue controller retains bridge,
mapping, protected settings and lifecycle, supplies UI state and receives commands.
Its ASI installs no graphics/input hooks. This removes the competing hook sets
without merging telemetry or moving Hue credentials into CDT. Actual integration
belongs in the telemetry product repository per AGENTS. Keep unrelated untracked
CDT research files intact. Next: settle this UI-hosting change with the owner;
do not make speculative live hook changes or overwrite existing packages.

# Previous checkpoint — wall-washer mapping candidate, 2026-10-16, Codex

Owner corrected the sequence: implement washer functionality together before
testing the old point-mapping ASI. Added optional local per-area/light/segment
wall-area calibration, separate from shared mixer presets. Direction and width
describe the illuminated wall from the seated viewer; each segment has its own
side offset, elevation and vertical span. Imported Hue positions, IDs and
membership remain intact, including bridge layout/ownership checks. Legacy or
disabled calibration retains the existing point mapper. No owner settings changed.

Mapping integrates over the calibrated angular area. Source-focused bounded
quadrature preserves the overlap fraction under strongest-channel compensation,
avoids holes for one-degree sources on wide walls, and handles the +/-180 seam.
Distance fade, source disc, strict boundaries, freshness and Ambient remain in
their existing roles. The native Room tab previews wall extents separately from
Hue dots. Demo now uses the selected actual area when available; an adjustable
single-source preview never sends lamp output. Left/right starting points are
explicit editable suggestions, not measured room geometry.

Explicit Test CH starts a separate two-second, 10% white identification session
for one independently addressable segment, without needing telemetry. It is
mutually exclusive with sync, blocked in demo/smoke, refuses active areas, clears
at the time limit before HTTP cleanup, and releases/restores only when still owned.
Tests cover cancellation during activation, failed handshake and another app's
takeover. Physical identification has not yet been run on the owner's Hue Bridge.
The final review tightened the synthetic transport to reject sends before DTLS
connection, exposing a cleanup bug. Both ordinary sync and identification now
skip a clear packet until connected, so failed handshakes still release/restore.
Subprecision wall extents also behave as points rather than dividing by zero.

Verification: 85/85 nonphysical tests passed, including calibrated streaming,
area/segment mapping, overlap, narrow-source sweeps, seam, persistence/preset
exclusion and identification safety. Windows-sandbox temp/DPAPI/loopback failures
were rerun successfully outside the sandbox; NuGet audit was disabled for these
build commands because its network feed was unavailable (no dependency change).
Managed Release and native /W4 /WX builds passed. Native BridgeTests passed.
The exact packaged candidate passed wall/segment and preview commands, invalid
calibration rejection, strict side separation and identify rejection in smoke,
plus all 30 mixer controls, D3D12/WARP pixel readback (828,534 visible pixels),
hide/reopen, resize and host-preserving shutdown. Its Room-tab image was inspected:
`artifacts/asi-package-0.4.0-asi.8/ingame.png` (synthetic, not the game).

Independent ASI / win-x64 / 0.4.0-asi.8: seven-file staging/ZIP validation passed.
ZIP `dist/CrimsonHue-0.4.0-asi.8-asi-win-x64.zip`, 3,368,113 bytes, SHA-256
80A671E91BFBF391E02141925390E126F0FE96AA98B85622CA6A9E874F42EB00.
ASI 3,909,632 bytes, SHA-256
CCAA566EC3CAE8AF0E2DE659E1DBDE17246722D7B54A118540CF258E1EF0254B.
The preliminary `.asi.7` package is preserved but superseded by this cleanup fix.
No installation, game launch, real lamp output, live telemetry, ReShade changes
or upstream changes. Existing packages are immutable; installed `.asi.6` remains.
No game-build compatibility claim; actual game/input/DMM/lamp acceptance is open.
AGENTS/CLAUDE still match. Next owner step: import the new candidate with the game
closed, then inspect Room without ReShade and identify one channel at a time,
reporting which washer and visible wall patch lights before choosing final angles.

# Previous checkpoint — real Hue Bridge paired, 2026-10-16, Codex

Owner now has a physical Hue Bridge BSB002 and two Hue Play wall washers in
an existing Entertainment area. The existing CrimsonHue core successfully
probed HTTPS, paired after the owner's link-button press, pinned the observed
certificate, and imported the area through API v2. No bridge/client code changed.

Area `Spieltisch` has six channels: three segments per lamp, at X -0.4/+0.4,
Y 0.8 and Z -0.4/0/0.4. Both lamps currently have the imported label `Back`.
The area was inactive. Read-only capture of both physical light states passed.
This verifies real-bridge pairing, protected storage and area/state parsing;
it does not verify Entertainment DTLS output or visible lamp behavior.

New credentials and area selection are saved in the actual owner's current-user
DPAPI profile. Existing mixer, telemetry and shortcut settings were preserved;
previous settings and encrypted diyHue credentials were backed up under the
profile's `backups/20261016-before-hue-bridge-139/`. The temporary encrypted
workspace pairing file was removed. No keys or API responses were logged.

No physical stream, live telemetry, game launch, ASI replacement or package change.
No game/desktop consumer was running at verification. AGENTS/CLAUDE still match.

Owner's test constraint: test CrimsonHue without ReShade. ReShade is reserved
for the separate in-game lightshow and its FPS cost is unwanted here. No ReShade
installation was modified in this session; verify it is not loaded at game test.

Owner reports the wall washers project outward to the left/right, so their
physical Hue points do not describe the visible illuminated wall areas. Existing
mapping uses `channel.Position.Unit`; the actual area's returned channel/location
fields contain positions and membership but no explicit orientation field.
Proposed, not implemented: local per-lamp/per-segment wall-area calibration,
describing visible target direction and angular extent from the seated viewer,
while preserving imported Hue positions, channel IDs and segment membership.
Keep this local room setup separate from shared mixer presets.

Owner clarified the geometry: both washers are fully aimed outward, left/right,
near the walls. Their broad projections mainly illuminate the corresponding
side walls and also the left/right portions of the wall behind the monitor;
the central area behind the monitor is unlit. Model the visible output as broad
left and right regions extending towards front-left/front-right. The owner's
rough 45-degree beam estimate is not a calibrated viewer-relative angular span.
Keep emitter aim distinct from the visible wall directions at the seated viewer.
Segment-to-wall-area correspondence still needs an observed segment test; do
not assume it from the imported height coordinates alone. No geometry or mapping
defaults were changed, and no physical test was performed.

During the owner's ASI recap, a newer local diagnostic log dated 2026-10-14
reported D3D12/SDR overlay ready and a first submitted frame. Its host/version
were not verified; it does not establish visible UI or input acceptance.
Next: use the clarified wall geometry to design local output-area calibration;
verify the installed ASI and test the mixer without ReShade. Real lamp output
and game acceptance remain open.

# Previous checkpoint — 0.4.0-asi.6 letter shortcut, 2026-10-03, Codex

Owner reports F7 is fly mode and all F4–F12 are occupied. Default is now
Ctrl+Shift+H (VK 72) in both managed and native code, including startup fallback,
UI display and smoke expectations. Setup still freely selects a key/modifiers;
explicit saved bindings are preserved. Read-only inspection found no saved
shortcut for this owner, so the new default applies. No settings were written.

Managed/native Release builds, exact candidate OverlaySmoke and seven-file
staging/ZIP validation passed. Smoke verified H as default, valid/invalid
binding commands, all mixer controls, 828,535 visible WARP pixels, hide/reopen,
resize and host-preserving shutdown. BridgeTests passed with the new default.
Full 74-test and foreign-hook results below are from `.asi.5`, not rerun for
this default-only change. AGENTS/CLAUDE hashes still match.

Independent ASI / win-x64 / 0.4.0-asi.6:
ZIP dist/CrimsonHue-0.4.0-asi.6-asi-win-x64.zip (3,337,560 bytes), SHA-256
5729B7325A2D1241CD41557480558D63429B177C497E4CECE8F310AF2D3C3CE4.
ASI 3,863,040 bytes, SHA-256
D7862B1EE8CA5E2FAAB7670BC811E9219C393C2BF2E823E99B004839DE03F650.
Older ZIPs remain immutable. No install, game control, physical output or upload.
The `.asi.4` invisible-overlay failure remains undiagnosed. The `.asi.6` candidate
is not game-tested and retains `.asi.5` native diagnostics. Next: owner closes
the game, installs `.asi.6` through DMM, restarts and tests Ctrl+Shift+H; inspect
the generated native log on the owner's return. No Computer Use or waiting.

# Previous checkpoint — 0.4.0-asi.5 shortcut/graphics diagnostics, 2026-10-03, Codex

Owner entered Crimson Desert with the exact `.asi.4` ASI loaded (SHA-256
96C2FFA09FF63588573A4DE00E4ADB76901B27D8E757A176B4D9DED83FD22320). Module inspection
also found installed CoreCLR/WindowsDesktop 8.0.31 and extracted CrimsonHue
assemblies. The owner reported no visible mixer; Ctrl+Shift+F10 only toggled CDT
light markers. This is a failed real-game UI acceptance, not a runtime failure
or proof of a particular graphics cause. ReShade and Streamline were loaded;
CDT's overlay log reached ready. No CrimsonHue graphics log existed. TCP listener
inspection was access-denied, so CDT API progress was not established this turn.
Owner explicitly declined Computer Use: game actions remain manual.

Owner requested a configurable shortcut. Setup now selects a keyboard key and
Ctrl/Shift/Alt modifiers; Save shortcut applies/persists it in existing local
settings. Default is Ctrl+Shift+F7, avoiding CDT's F8–F11. Missing or invalid
key bindings use the default. All save/preset-import paths preserve the binding;
mixer exports still contain no local setup. Main-key edges require exact modifiers
and foreground ownership; holding, refocusing and rebinding a held key do not
re-toggle. The graphics worker polls the shortcut even if capture/rendering failed.

New native logs live in LocalAppData/CrimsonHue/diagnostics (PID/tick filenames,
64-KiB bound). Only fixed statuses and error codes are written: hook slots/failure,
swapchain rejection, D3D12 initialization HRESULT, first submitted frame and
shortcut events. Controller ready is also recorded. Hook installation failure
gets at most ten attempts, 500 ms apart. This supplies evidence for the remaining
game failure; it is not a confirmed fix for that failure. No game-file replacement,
UI automation, physical stream or upstream source change was performed.

Verification: managed/native Release builds passed (/W4 /WX). 74/74 nonphysical
tests passed including legacy-settings/shortcut persistence and preset exclusion.
BridgeTests passed shortcut modifiers, edges, repeat, focus and rebind controls,
plus existing FIFO/cleanup/snapshot checks. OverlaySmoke exercised valid/invalid
shortcut commands and all 30 mixer controls with the hidden controller. Two
synthetic modes loaded a separate MinHook DLL before/after CrimsonHue; factory
and Present hooks in both DLLs received calls while CrimsonHue drew/resized.
Both passed with 828,535 visible pixels. These are generic-hook controls, not
CDT/ReShade/Streamline game acceptance. The exact final packaged candidate passed
ordinary D3D12/WARP render, command validation, hide/reopen, resize and shutdown.
Its log showed controller/graphics ready and first submitted frame. Evidence:
artifacts/asi-package-0.4.0-asi.5/ and artifacts/overlay-shortcut-foreign-{before,after}/.

Package profile/version: independent ASI, win-x64, 0.4.0-asi.5. Validator confirmed
seven exact files in one CrimsonHue/ root, no loose DLL/JSON/CFG/EXE, no runtime,
settings or credentials. The older immutable candidates are preserved.
ZIP: dist/CrimsonHue-0.4.0-asi.5-asi-win-x64.zip (3,337,582 bytes), SHA-256
B5C1DFA65C2861E30B58C2E9691FA7C8444C788EDE88D3B777760B582D73FD98.
ASI: 3,863,040 bytes, SHA-256
675B8C72701D6DCB7ACFC4BF6180DAA1ADD547E3EA5527789F194EB99E928052.
This candidate is not installed/game-tested; physical input, DMM removal and
real lamps remain pending. Branch codex/crimsonhue-asi; no push/upload.

Next owner action: close the game, import the new ZIP through DMM and restart;
try Ctrl+Shift+F7 and report whether the mixer appears. Read the new local native
diagnostics before proposing another graphics fix. End whenever owner action is
needed, with no waiting/polling. AGENTS.md and CLAUDE.md remain byte-identical.

# Previous checkpoint — 0.4.0-asi.4 in-game overlay candidate, 2026-10-02, Codex

Owner resumed the independent-ASI work. The visible mixer now renders through
ImGui into the game's D3D12 backbuffer; production never shows the WPF window
or launches a separate EXE/process. The existing controller remains unshown on
its own STA and reuses the C# core, telemetry API clients, imported layout,
pairing, DPAPI settings and ownership-aware DTLS cleanup. Native snapshot and
command queues are bounded; the render thread does not call CoreCLR or devices.
CDT source and telemetry contracts were not changed or recreated here.

All 28 numeric mixer controls and two boundaries are exposed. Output/Ambient/
Color/Space, Setup, room/channel output, source preview, distance curve, raw
input/freeze and preset section previews are in-game. Presets use an absolute
.json path in the overlay rather than external file/dialog windows. Imports
still default to Output/Ambient/Color, with Space off. Setup requires Apply
addresses before Find/reconnect. Loading and demo never start physical output.
Ctrl+Shift+F10 toggles the overlay while the game has focus; Hide keeps sync
running, X stops/saves/hides. Stop before game exit remains required.

The native renderer adapts generic MIT DXGI/HDR code from CDT, with its license
preserved in licenses/CDT-MIT.txt and packaged notices. It tracks the actual
presentation queue, defers initialization after graphics-wrapper creation,
uses private ImGui state, fences GPU resources, and handles resize/output-space
changes. SDR, scRGB and HDR10 have explicit paths (200-nit UI paper white).
Foreground window input is chained/captured while open. Rendering failure
releases input and requests stream Stop. FIFO Start/Stop ordering is preserved;
cleanup is retried if the STA queue temporarily refuses it. Other CoreCLR/WPF
mods, actual game input and CDT/graphics-wrapper coexistence remain unverified.
ASI/process pinning and game-exit cleanup limits from docs/ASI.md still apply.

Users install .NET 8 Desktop Runtime x64 separately. Build-Asi embeds exactly
five application/dependency files, including the BouncyCastle assembly and
runtime metadata, but no .NET runtime. SHA-verified extraction stays in the
current user's LocalAppData cache. One ASI plus six documentation/license files
ship under one CrimsonHue/ ZIP root; no loose DLL/JSON/CFG/EXE companions.
Publish-Asi now tests the exact candidate and validates all seven ZIP entries
byte-for-byte against staging. Existing releases/candidates remain immutable.

Verification: native /W4 /WX and managed Release builds passed. 73/73 existing
nonphysical tests passed (mock HTTPS/WS, loopback OpenSSL DTLS, synthetic DPAPI).
The exact .asi.4 packaged ASI passed hidden-controller checks, all 30 controls,
valid/invalid command exchange, actual D3D12/WARP pixel readback (828,535 visible
pixels), hide/reopen, ResizeBuffers rebuild and host-preserving shutdown.
Evidence: artifacts/asi-package-0.4.0-asi.4/ingame.png (synthetic, not the game).
The earlier equivalent .asi.3 render was visually inspected. Offscreen HDR
goldens passed for scRGB, PQ/BT.2020, alpha/background preservation and SDR.
BridgeTests passed bounded queue, FIFO Start/Stop, cleanup retry and snapshots.
Cache cold/reuse/repair and missing runtime/resource/early-stop/ready-stop
passed on the .asi.3 candidate before the final version/queue correction.
The .asi.4 validator passed staging/ZIP equality and a negative control rejecting
an extra loose JSON companion. No live telemetry or lamp session was run.

Package profile/version: independent ASI, win-x64, 0.4.0-asi.4.
ZIP: dist/CrimsonHue-0.4.0-asi.4-asi-win-x64.zip (3,294,107 bytes).
SHA-256: 5431DC4F98A047C970C6DC8146C0F1E5CA35ACDF011F923AE3B808EE0CD0D3C2.
ASI: 3,815,424 bytes; SHA-256
96C2FFA09FF63588573A4DE00E4ADB76901B27D8E757A176B4D9DED83FD22320.
The intermediate .asi.3 ZIP and old bundled-runtime .asi.2 ZIP are preserved.
README/ASI/user guide now describe this in-game layout. AGENTS and CLAUDE are
byte-identical. Work is saved on codex/crimsonhue-asi; no push/upload/install.

Next owner action: with the game closed and old desktop sync stopped, import
the new ZIP through DMM and start the game. Validate real overlay opening,
mouse/keyboard, hide/reopen, Setup and Start/Stop alongside CDT; check exact
deployed ASI hash and DMM removal. This candidate has not been game/DMM installed
or accepted with physical lamps/HDR display. Do not infer that from WARP smoke.
Stop and wait before game exit. End the turn when owner action is needed.

# Previous checkpoint — ASI prototype saved; in-game UI required, 2026-10-01, Codex

Owner's latest instruction: **do not finish the implementation now; record it
and stop.** The required result is an independent CrimsonHue ASI with an actual
in-game overlay. A WPF window outside the game's rendered image does not meet
that requirement, even when it runs inside the game process. Do not integrate
the consumer into CDT. Owner also chose an installed **.NET 8 Desktop Runtime
x64** as a requirement instead of redistributing the runtime.

Branch remains `codex/crimsonhue-asi`. The saved `.asi.3` prototype uses static
nethost to discover installed .NET and embeds only five application/dependency
files in a compressed cabinet inside the ASI. It verifies and extracts these
to `%LOCALAPPDATA%\CrimsonHue\asi-cache\<cabinet-sha256>`; no loose runtime or
application DLLs need deployment to the game directory. The current production
UI is still the earlier WPF window. **No in-game overlay has been implemented.**
The small-file/runtime changes are an intermediate source checkpoint, not a
finished release or accepted implementation of the owner's UI requirement.

Reason for single-ASI deployment: CDT's
`docs/MOD_MANAGER_VALIDATION.md` records DMM 1.9.4/2.8.1/3.1.1 leaving DLL/CFG
companions after removal and retaining old metadata on upgrades. Its package
rules also avoid loose JSON, which DMM can classify as game patches. This is
evidence of a manager lifecycle issue, not a general Crimson Desert DLL ban.
The new native host ignores helper processes such as crashpad_handler.exe and
initializes only in CrimsonDesert.exe or the explicit developer smoke harness.

Latest prototype evidence: native Release /W4 /WX build passed; the actual
3,134,976-byte ASI loaded installed CoreCLR/WindowsDesktop 8.0.31 in the native
harness, extracted a fresh verified cache (state 1), rendered all six synthetic
WPF panels, exercised mixer controls/hide/reopen/close and shut down the UI
without ending its host. Evidence: `artifacts/asi-small-fixed/cold*.png`.
A diagnosed stack overflow came from a large DllMain stack frame on small-stack
WPF render threads; the process-path buffer now lives in a non-inlined helper
called only on process attach. The installed dotnet root was also corrected.
Earlier diagnostic runs covered missing runtime/resource, ignored helper,
startup stop and ready stop; only the complete cold smoke was rerun after the
final fix. Warm-cache/repair checks and new package validation remain pending.
The previous 73/73 nonphysical test result below is from `.asi.2`, not a new
test run after this checkpoint. No physical output or real game test occurred.

**Packaging is unfinished.** Build-Asi defaults to `.asi.3`; Publish-Asi/Publish
and the installation guides below still describe the historical `.asi.2`
bundled-runtime folder layout. Update them before creating another ZIP. No
`.asi.3` release ZIP was created. Existing immutable ZIPs remain preserved.

Next work, only when the owner resumes: implement the real in-game UI in this
independent ASI, preserving all mixer/setup/preset and stream ownership behavior;
then finish packaging and proportionate validation. End-user prerequisite is
.NET 8 Desktop Runtime x64. Keep game-exit cleanup limits explicit. No push,
upload, installation or game/bridge action was performed. AGENTS and CLAUDE
remain byte-identical.

# Previous checkpoint — CrimsonHue 0.4.0-asi.2 branch preview, 2026-10-01, Codex

Owner requested an independent CrimsonHue ASI instead of the desktop EXE,
primarily to remove EXE content from Nexus uploads. First committed the existing
untracked Media files and historical HDR object on main (`d504b7c`), then created
`codex/crimsonhue-asi`. All existing releases remain preserved; no push, public
release, game deployment or physical stream was performed.

`native/CrimsonHue.Asi` is a native x64 DLL with an ASI suffix. Its worker hosts
the app-local .NET/WPF runtime through hostfxr and calls UnmanagedCallersOnly
entry points. The previous C# UI is now a library and runs on a dedicated managed
STA inside the native host process. No EXE is launched or packaged; the existing
core, telemetry API consumption, mixer/presets, DPAPI store and ownership-aware
stream cleanup are reused. CDT source and API are unchanged. This is a hosted
managed consumer, not a full C++ port or a renderer overlay.

The mixer opens when loaded. Ctrl+Shift+F10 shows/hides it while its host or mixer
has focus; Hide settings saves and hides without stopping sync. Close stops sync,
saves and hides; the shortcut reopens the same window. A conflicting hotkey
switches Hide/Close to minimize so the taskbar remains usable. Loading never
automatically starts physical sync. Native Stop queues bounded existing cleanup
and shuts down the plugin UI without exiting its host. The ASI is pinned until
process exit; hot unload/reload is unsupported.

Important lifecycle limit: ending the game's process also kills CrimsonHue.
This preview has no game exit hook and cannot promise restoration on normal
game exit, crash or termination. Owner must Stop and wait for stopped status
before quitting the game. Other CoreCLR-hosting mods remain unverified.
Installation/lifecycle details are in `docs/ASI.md`; README and user guide now
describe this branch. AGENTS.md and CLAUDE.md remain byte-identical.

Verification: native Release /W4 /WX and managed Release build succeeded.
73/73 existing nonphysical tests passed, including mock HTTPS/WS, real loopback
OpenSSL DTLS and synthetic DPAPI. The native harness loaded the actual ASI and
local runtime in an ordinary native process with a different working directory;
all six synthetic WPF panels rendered and mixer control verification passed.
Hide/reopen/close and final UI shutdown succeeded without ending the host.
Separate startup-stop, ready-stop and missing-payload runs passed. The rendered
main view was visually inspected. These are synthetic/native-host results,
not evidence of in-game ASI-loader compatibility or physical lamp appearance.

`scripts/Publish.ps1` now delegates to `Publish-Asi.ps1`. It refuses existing
versioned output, checks the exact candidate through the native harness, rejects
EXEs/settings/secrets/developer artifacts, writes normalized ZIP entry paths and
hash-compares every archive file to the candidate. Optional .NET createdump.exe
is excluded; runtime licenses/notices are included. First .asi.1 candidate is
preserved (its checker failed on Windows ZIP separators); an incomplete .asi.2
staging directory is preserved under artifacts. The finished .asi.2 ZIP passed
all 471 file comparisons and its native UI smoke.

Package: `dist/CrimsonHue-0.4.0-asi.2-asi-win-x64.zip` (72,628,534 bytes), SHA-256
`DB7F954F5459A4CDE3B5D5EE570AC62B3A449A7F49F7CC1CDB3083EA03855D4A`.
The native `CrimsonHue.asi` is 125,952 bytes; the whole uncompressed candidate is
173,107,220 bytes, primarily bundled runtime DLLs. The previous 0.3.3 ZIP still
matches `90867C06261C7DBCC9925768E0E70F6AC1C804D3FF0294C58EB7A816A3B29A61`.
No Nexus acceptance or antivirus claim follows from removing EXEs.

Next owner action: with the game closed and old desktop sync stopped, place the
ASI plus its complete sibling CrimsonHue folder in the existing CDT ASI-loader
directory, then test opening/hiding the mixer and Start/Stop in game. Stop before
game exit. Real-game/real-lamp acceptance is pending; do not infer it from smoke.

# Previous checkpoint — CrimsonHue 0.3.3, 2026-09-29, Codex

Shareable mixer presets are implemented in the standalone app. Export writes a
strict, readable `CrimsonHuePreset` v1 JSON containing the 30 mapping controls
and a name. It excludes bridge/telemetry addresses, Entertainment area ID,
channel positions and protected credentials. Import checks the format, version,
complete field set, valid ranges and size before showing a change preview.
Output, Ambient and Color start selected; room-dependent Space starts off. The
selected values save locally and update active mapping. Cancel or invalid files
leave current settings intact. The complete user guide is included in the ZIP.

Verification: Release build zero warnings/errors; 73/73 nonphysical tests pass,
including full preset round-trip, selective import and malformed-file rejection.
Source and packaged WPF UI smoke ran in synthetic mode. Package contents were
hash-compared with the seven ZIP entries; no CDT, local settings, secrets or
nested archive was present. No physical stream was started. AGENTS.md and
CLAUDE.md remain byte-identical.

Package: `dist/CrimsonHue-0.3.3-win-x64.zip` (68,230,539 bytes), SHA-256
`90867C06261C7DBCC9925768E0E70F6AC1C804D3FF0294C58EB7A816A3B29A61`.
The 0.3.2 package remains immutable. Tag `v0.3.3` points to
`24f39fb3cc5a4423cdb92e06f7e4c6e863e491fe`. GitHub release
https://github.com/ZappendusterFX/CrimsonHue/releases/tag/v0.3.3 is published
as Latest, not a pre-release. The public ZIP download returned HTTP 200 and
GitHub reported the same size and SHA-256 digest. Nexus publication belongs to
the owner.

# Release publication — 2026-09-28

GitHub repository: https://github.com/ZappendusterFX/CrimsonHue (public).
GitHub latest release: https://github.com/ZappendusterFX/CrimsonHue/releases/tag/v0.3.2.
Tag `v0.3.2` points to `c34245c287d3d7301f991c885d3d56c0db0b3225`,
which includes `docs/USER-GUIDE.md` and README links. The immutable ZIP was
uploaded with 70,326,922 bytes; GitHub reports SHA-256
`6A1C4C5D39BB90136925A112439AA245B59348A71B5A63A85D7BAE3F6EE01DC6`,
matching the local file. GitHub release notes state prerequisites and link the
complete guide. CDT is separate and not packaged. Nexus publication belongs to
the owner; `docs/NEXUS.md` is the prepared listing copy. No Nexus upload by Codex.
On 2026-09-29 the release was promoted from Pre-Release to Latest Release so the
existing uploaded build appears in GitHub's main Releases view. The public ZIP
download returned HTTP 200; its GitHub asset digest remains unchanged.

# Current checkpoint — CrimsonHue 0.3.2, 2026-09-28, Codex

Owner found nearby fires behind the character lighting the front Hues while CH0
rear stayed dark. A read-only CDT capture confirmed the geometry: player
(-10606.647, 607.5385, -4421.96), camera
(-10612.477, 610.5437, -4423.1084), sources about 6.06–6.10 GU from the player.
Those sources are behind the player but in front of the third-person camera lens.
The owner explicitly chose an adjustable percentage between those two reference
points instead of declaring either one universally correct.

Space now exposes Direction origin, 0% raw player position to 100% paired camera
position, with 50% as the visible default. It interpolates all three coordinates
along their connecting line; there is no additional eye-height offset. Camera
right/forward/up still supplies orientation at every percentage. Distance fade
remains player-relative, and the strict boundaries/disc use the selected origin.
Mapper and RoomView share SourceDirection; preview status labels the percentage.
The origin has slider/numeric/reset/save support and defaults to 50% for older
settings that do not contain it. No existing numeric settings were rewritten.

CH0 is at imported room XYZ (+0.3129, -0.9982, +0.5053): rear-right, about 26 degrees
above horizontal. It is not a sky-only channel. Its height participates in the
finite disc test. In the recorded scene, origin 0% reaches CH0 with the rear-right
fire and leaves both front channels at exact local zero. At 100%, the same nearby
fires reach the fronts. At 50%, their combined side/height angle can miss the
60-degree disc around CH0, so the midpoint is not promised as a universal fix.
Origin 25% also reaches the rear in the recorded regression. Strict left/right
still excludes the rear-left fire from the only rear-right lamp.

Owner reports an active Off beyond of 15 GU; disk settings still said 20 GU at
inspection, so in-app unsaved settings must not be equated with saved ones. The
recorded scene regression explicitly uses 15 GU. Normal close should save the
owner's current choices before switching packages. LEDVANCE minimum brightness
was also suspected by the owner for tiny nonzero output; the existing Ambient
black threshold was recommended. No hardware dimming limit was measured and no
new output threshold was added in this release.

Verification: Release build zero warnings/errors; 71/71 non-live tests passed,
including exact 0/25/50/100% origin geometry, camera axes, endpoint persistence,
the recorded 15-GU scene, and camera translation independence at 0%. Read-only
live saved-layout mapping passed 72/72. With the saved 20-GU cutoff, origin 0%
gave CH0 local RGB approx (0.6000, 0.3541, 0.1832) and front local RGB zero.
At 100%, both front lamps had red output 0.6000. At 50%, fronts were zero and
CH0 had only a small contribution from farther sources (~0.00383 red). The
separate Ambient baseline was ~0.000823 per output channel. These are calculated
outputs, not physical lamp measurements. No physical stream was started.

Source and packaged WPF smoke runs exited 0, with six panels rendered; Space was
visually inspected. UI coverage includes all 28 numeric and two boolean settings,
and origin endpoint changes update mapping and preview text. All five ZIP entries
match package files. AGENTS.md and CLAUDE.md are byte-identical.

Package: `dist/CrimsonHue-0.3.2-win-x64/CrimsonHue.exe` (166969627 bytes),
`dist/CrimsonHue-0.3.2-win-x64.zip`, SHA-256
`6A1C4C5D39BB90136925A112439AA245B59348A71B5A63A85D7BAE3F6EE01DC6`.
Prior immutable packages and pre-existing codex_check_hdr.obj are preserved.
No credentials/settings/game files are packaged and no upload occurred.

Next: owner closes 0.3.1 normally, opens 0.3.2, and compares origin percentages
in the same scene. Camera position, player position and imported lamp height
are distinct factors; do not change Hue positions or invent a hardware limit.

# Previous checkpoint — CrimsonHue 0.3.1, 2026-09-28, Codex

Owner requires strict spatial separation: a source on one side must never light
lamps on the opposite side. Off-screen sources remain relevant to the room. The
owner clarified that source enlargement must be a 2D circle, not a world-space
sphere. The old exponential distribution had an infinite tail: at the reported
left brazier (roughly 22 degrees left), focus 2 assigned ~66% angular weight to
the front-right channel. That was broad directional mixing, not a reflection.

The mapper now applies visible Strict left / right and Strict front / rear
switches, both On by default (including old settings). Opposite-sign camera/room
X or Y contributions are exactly zero before normalization. A source with no
matching lamp is dropped, never redirected across a boundary. Positions exactly
on a center line belong to both adjacent halves. Room direction offset rotates
the source first. Height still participates in matching, with no height boundary.

Each source has a finite circular footprint in the plane normal to its camera
bearing. Channel rays intersect that 2D plane; Source disc radius controls its
angular radius (60 degrees default, 1–89 range), and Source disc soft edge controls
the outer fading fraction (25% default, 0–100%). The footprint is clipped at
enabled side boundaries. Outside the circle is zero; its edge is applied after
angular compensation so compensation cannot revive it. Off-screen and rear
sources use the same mapping without a screen-frustum filter. All four new
controls are visible in Space with reset/persistence. Preview shows boundaries
and their states. Raw CDT input and the independently controlled Ambient layer
keep their meanings; local spill does not stand in for reflected illumination.

Strict boundaries also bypass the old channel EMA, including the legacy feed,
so a camera turn cannot retain color on the wrong side. The immediate path now
assigns the target RGB directly, avoiding floating-point residue from an EMA
formula with alpha one. This restriction is labelled beside Legacy smoothing.

Verification: Release build zero warnings/errors; 69/69 non-live tests passed.
Tests cover the recorded brazier/three-lamp layout, exact black across quadrants
through camera rotations, unmatched layouts, center lines, independent switches,
offset, finite circular footprint in both dimensions, radius/edge adjustment,
off-screen coverage, Ambient independence and persistence of all new settings.
Read-only live saved-layout mapping passed 70/70. At the owner's forest scene,
40 clear sources and raw Ambient W=0.000968932153 produced local-only RGB zero
for CH0 rear and CH1 front-right; CH2 front-left was approximately
(0.6000, 0.3548, 0.1834). Combined right/rear output equalled Ambient alone,
approximately (0.000742932, 0.000742932, 0.000742932), at the saved 60% cap.
A simulated 180-degree camera turn moved the fire contribution to CH0 rear.
These are computed outputs, not observations of physical lamp appearance.

Source and packaged WPF smoke runs exited 0. All 27 numeric controls and both
boolean controls are checked for UI coverage and live state changes; six panels
were rendered. All five ZIP entries match package files. AGENTS.md and CLAUDE.md
are byte-identical. No physical output session was started or taken over.

Package: `dist/CrimsonHue-0.3.1-win-x64/CrimsonHue.exe` (166969115 bytes),
`dist/CrimsonHue-0.3.1-win-x64.zip`, SHA-256
`F74658AC6316E947ECF8689069D28080840E00AF73F11098E91493543D16FAFC`.
All previous packages and pre-existing untracked codex_check_hdr.obj are preserved.
No credentials/settings/game files are packaged and no upload occurred.

Next: owner switches from the still-running 0.3.0 to this exact 0.3.1 package,
then starts sync and checks the left brazier and camera turns. Strict switches
start enabled; source radius and edge can be tuned in Space. Tiny genuine Ambient
remains independently adjustable with the existing Ambient black threshold.

# Previous checkpoint — CrimsonHue 0.3.0, 2026-09-28, Codex

Owner rejected 0.2.2's artificial night fill: live CDT had zero confirmed-clear
sources and practically zero Ambient, yet that version generated ~0.188 neutral
RGB at their saved 60% cap. The owner explicitly requires visible controls and
raw input, rather than hardcoded tuning changes. An initial 0.2.3 fix was never
packaged; this feature release supersedes that work.

The WPF UI now has Output, Ambient, Color, Space and Setup tabs. All 25 mapping
parameters have a slider, numeric entry, explanation and individual reset. Each
tab can be reset; Save settings stores the current choices. The Space tab has
Fade starts, Off beyond and the requested Distance falloff exponent, with a
curve preview drawn from the mapper's actual distance function. Camera offset
also rotates source dots consistently with lamp assignment.

The fixed night floor, special orange/fire recoloring, doubled direction
exponent and silent saved-brightness migration are removed. Extra background
light and Ambient black threshold default to zero. Hue shift, saturation, RGB
balance, gamma, Ambient exposure/reference/mix/transition/tint, local contrast,
distance curve and angular compensation are exposed. Technical conversion and
bounded-output formulas are documented in docs/AMBIENT-INTEGRATION.md.

CDT raw input shows unmodified local and sky working RGB, sky visibility and
the local mean, plus the raw confirmed-clear source RGB/positions and counts.
Freeze readout holds only the display for inspection/copying; incoming telemetry
and physical output continue normally. Channel cards show mapped output RGB.
Demo preview stays synthetic and no longer mixes live Ambient into its preview.
No CDT or diyHue implementation was changed.

Verification: Release build zero warnings/errors; 62/62 non-live tests pass,
including neutral preservation of orange source ratios, exact black at zero,
all controls, distance falloff boundaries, persistence and ownership cleanup.
Read-only saved-layout live mapping passed 63/63 with zero clear sources:
Ambient W=0.000247726451 produced RGB≈0.000189962018 on every channel at the
preserved 60% limit (~0.019% output component). Tiny raw input is retained by
default; the visible Ambient black threshold can suppress it if desired. This
is computed output, not physical lamp evidence. No physical session was started.

Source and packaged WPF smoke runs exited 0. The UI gate verifies every
MappingSettings double has a visible control, each control changes mapping,
numeric entry rejects invalid values, and distance limits remain consistent.
Output/Ambient/Color/Space/Setup/raw panels were rendered; raw and mixer views
were visually inspected. All five ZIP entries match package files.

Package: `dist/CrimsonHue-0.3.0-win-x64/CrimsonHue.exe` (166958875 bytes),
`dist/CrimsonHue-0.3.0-win-x64.zip`, SHA-256
`71F4AD244421AE97F2D7C140778AC1AB540DEF80D40E23BBA10FD6EF272DD18C`.
Earlier packages and the pre-existing untracked codex_check_hdr.obj are preserved.
No credentials/settings/game files are packaged. AGENTS.md and CLAUDE.md are
byte-identical. No public upload occurred.

Next: owner tunes 0.3.0 in-app, especially Distance falloff and Ambient black
threshold, while comparing raw values and mapped output. Actual physical lamp
appearance remains owner-observed evidence and must not be inferred from tests.

# Previous checkpoint — CrimsonHue 0.2.2, 2026-09-28, Codex

Owner tested 0.2.1 in game and found the lamps too dark, with weak camera-turn
response and an orangered lamp beside a visibly yellow fire. Ingame HUD and
read-only CDT API agreed on nearby linear engine-light RGB around
`(1.582, 0.636, 0.173)` and `(1.122, 0.340, 0.085)`: the source data are
orange/red even though the rendered flame looks yellower. No CDT code or API
was changed. The correction here is artistic, not evidence of a telemetry bug.

0.2.2 keeps the clear-only upstream filter. Each source now focuses on the
strongest imported Hue channel and uses twice the previous angular exponent;
this makes camera yaw materially change a sparse room layout. Orange-family
source RGB raises green smoothly toward 0.8 of red; pure red, blue, green and
near-white stay unchanged. The valid Ambient signal now supplies
`0.08 × presence + 0.4 × level` neutral linear fill, while local contribution
retains `1 - 0.3 × level` contrast. Missing/stale Ambient fades back to the
local-only mapper. The untouched old 60% brightness preset migrates once to
85%; custom brightness and later deliberate 60% settings are preserved.

Verification: Release app build zero warnings/errors; 57/57 non-live tests pass,
including fire hue, three-channel camera yaw, dark Ambient fill, old preset
migration and preservation of deliberate brightness choices. Read-only live
saved-layout mapping passed 58/58: 50 clear sources, Ambient W≈0.001 and
computed RGB for actual/yaw+180°/no-local cases. At that moment rear channel
red moved 0.43→0.85 on the simulated turn; one front channel moved 0.85→0.37;
no-local neutral output was about 0.27 per component. These are calculated
colors, not physical lamp observations. The packaged WPF demo smoke exited 0,
its preview rendered, and all five ZIP entries match package files. The real
area was active during the read-only inspection, so no 0.2.2 physical run was
attempted and no active stream was interrupted.

Package: `dist/CrimsonHue-0.2.2-win-x64/CrimsonHue.exe` (166917915 bytes),
`dist/CrimsonHue-0.2.2-win-x64.zip`, SHA-256
`812F6FA6E59F762E86AF22A998DE3C1CA11330CB90F0712331BFD387C19EB07C`.
Prior packages are preserved. No credentials/settings/game files are in the ZIP
and no public upload occurred. AGENTS.md and CLAUDE.md remain byte-identical.

Next: owner stops the 0.2.1 stream, opens the exact 0.2.2 package and checks
the left fire lamp's hue, brightness, and response to camera turning at the same
game point. Also compare a bright outdoor scene and blocked source. Record
actual visible lamp behavior separately from the computed values.

# Current checkpoint — CrimsonHue 0.2.1, 2026-09-28, Codex

The owner observed all three lamps turning red together outdoors in bright
daylight with 0.1.3, matching that version's local-only mapping. Screenshot/CDT
diagnostics showed available sky and camera visibility; live read-only Ambient
values gave a local working estimate around 7–11. CrimsonHue now subscribes to
CDT's separate `/v1/ambient/stream` on the configured local port. It requires
fresh sky, measured camera sky visibility and a valid local product, expiring
sky and visibility independently after 1500 ms including client time. Missing
Ambient is explicitly labelled and fades to the local-only fallback.

The working RGB is AP1-like and not calibrated to local-light or display RGB.
0.2.1 therefore uses it only as a relative brightness signal: a smoothed neutral
room baseline increases and confirmed local-light contrast decreases as camera-
local Ambient grows. The new Ambient influence control (default 1×, 0 disables)
adjusts this artistic response. No time-of-day or invented directional Ambient
was added. The exact model and limits are in `docs/AMBIENT-INTEGRATION.md`.

Verification: Release build zero warnings/errors; 55/55 non-live tests pass,
including bright-day/dark-interior mapping, immediate blocked-light removal,
independent sky/visibility expiry, fallback and legacy-settings migration.
Read-only live Bridge, all-around lights and Ambient WebSockets passed 58/58:
40 progressing light captures and nine progressing sky captures in four seconds.
Before packaging, a bounded source-built physical run started the free area,
sent nonzero near-neutral mapped colors, stopped and restored states; it did not
establish what the owner saw on the lamps. Source and packaged UI demo smoke
runs exited 0 with no physical output. All five ZIP entries match package files.

Package: `dist/CrimsonHue-0.2.1-win-x64/CrimsonHue.exe` (166916379 bytes),
`dist/CrimsonHue-0.2.1-win-x64.zip`, SHA-256
`FDEE30A69E2F60DA38264815A9803665539D7AD5BEE0097FFC8190B2090B71D5`.
The earlier local 0.2.0 package remains preserved but is superseded because its
normal missing-visibility status was misleading. No credentials/settings/game
files are in the ZIP and no public upload occurred.

Next: owner tests the exact 0.2.1 package in the same bright outdoor scene and
reports lamp colors/brightness. Then compare a dark interior and night, including
a visible and blocked source; adjust the response based on real-lamp evidence.

# Previous checkpoint — CrimsonHue 0.1.3, 2026-09-28, Codex

CrimsonHue now consumes CDT's schema 1.6 all-around `lights.upstream` input from
the neutral `/v1/stream` API. It requires the input's capture sequence, frame and
timestamp to match the rendered sample, then uses that sample's paired camera.
Each input source must have `sourceVisibility.status=clear`, method
`physics-ray-fan`, and a measurement no older than 500 ms including client time.
Blocked, unknown, missing and stale verdicts produce no Hue contribution.
When upstream is advertised but unavailable, output clears instead of switching
to a partial rendered view. With upstream disabled, schema 1.6 applies the same
clear-only rule to rendered sources. Schema 1.4/1.5 retains the old rendered path.
The confirmed-visible mapper bypasses the old channel-level EMA, which otherwise
retained color after a light became blocked or unknown; legacy smoothing remains.
Demo preview remains synthetic and never sends physical output. No telemetry code
was added here; CDT and its packages were unchanged.

Verification: Release app build had zero warnings/errors; 52/52 non-live tests
passed, including paired-capture, behind-camera, blocked/unknown, unavailable and
visibility-expiry checks and immediate removal of hidden color. A read-only CDT
WebSocket run during live gameplay passed 53/53 with 40 progressing all-around
captures and up to six confirmed-clear
sources. This proves input parsing and finite mapping, not real-lamp appearance or
visibility accuracy. After diyHue was restarted, a read-only bridge plus CDT run
passed 54/54 and confirmed the saved area layout. The area was initially active;
the owner stopped the other stream before physical output was attempted.
An explicit, bounded `--live-lamps` run then started Entertainment, observed
nonzero mapped color, stopped after five seconds, released the area and completed
the previous-light-state restoration path (53/53 tests passed). This proves the
CrimsonHue DTLS/bridge control path sent data; actual lamp appearance and
visible/blocked behavior still need the owner's observation.
Source and packaged WPF demo smoke modes exited 0 with no bridge connection or
lamp output. The ZIP's five entries match the on-disk package hashes. Existing
0.1.0 and 0.1.1 packages remain untouched. An earlier local 0.1.2 package from
this work is retained but superseded because it still smoothed hidden color.

Package: `dist/CrimsonHue-0.1.3-win-x64/CrimsonHue.exe` (166902555 bytes),
`dist/CrimsonHue-0.1.3-win-x64.zip`, SHA-256
`9109E559B445980E1F9619F84CD3AF6BD43A2239A7F31490CBE0C55D310BBF02`.
No credentials, settings, ASI or game files are included; no public upload.
CDT's separate Ambient API exists, but its working RGB has no calibrated relation
to local-light RGB or Hue output, so ambient mixing remains pending.

Next: ask the owner what the three lamps visibly did during the bounded stream.
Then check visible and blocked lights, including a source behind the camera, in
the packaged UI before claiming physical acceptance.

# Previous checkpoint — CrimsonHue 0.1.1, 2026-09-07, Codex

The new distance-fade release is built, tested, packaged and open for user testing.
Product lives entirely in this repository. Telemetry remains unchanged; its ambient
feed is still being developed. Historical 0.1.0 and diyHue work is retained below.

## Distance fade — 0.1.1

User reports real lamps reacting to distant in-game light colors and asks for
adjustable fade distances. There was already a radius falloff, but its attenuation
preceded HDR compression. Extremely bright sources could therefore saturate output
despite a tiny distance weight. The new mapping bounds each source's HDR intensity
before distance/direction weights. Weighted linear sources are summed and only
overflow is scaled down; there is no post-fade amplification. Multiple overlapping
sources still add together. This is an artistic model, not physical photometry.

- **Fade starts / Off beyond** sliders control a player-relative full-strength
  zone and a smooth fade to zero contribution. The same squared-smoothstep curve
  applies approaching/leaving. Existing smoothing releases residual old output.
- Both controls affect preview and active sync immediately. WPF events keep the
  interval valid; settings are saved on Start/normal close. Stored `Radius` remains
  the end distance for backward compatibility; new `FadeStart` defaults to zero.
  Existing user cutoff 35 was retained. Distances are game units, not known metres.
- Preview dots use the same distance envelope; live capture status reports the
  number of contributions inside the cutoff.
- Future ambient-relative contrast requirements are in
  `docs/AMBIENT-INTEGRATION.md`. Actual environment brightness should suppress
  ordinary local effects in bright daylight but retain them in dark daytime
  interiors. No ambient field, fake time-of-day rule or upstream modification was
  introduced. Ambient mixing is not implemented until its contract is available.

Verification for 0.1.1:

- Release build: zero warnings/errors. **46/46 non-live tests pass**, including ten
  new distance/HDR/boundary/player-vs-camera/smoothing/settings regressions and the
  existing real loopback OpenSSL DTLS, mock HTTPS/WS and DPAPI checks.
- Actual WPF slider event self-tests cover both crossed bounds and control limits
  in isolated smoke mode. Source-build and packaged EXE smoke runs exit 0. Render
  inspected at `artifacts/ui-distance-fade-packaged.png`; both fade controls and
  their explanation are visible at the default window size. Smoke uses synthetic
  data and does not load credentials or contact devices.
- Optional `--live` run: **46/47 passed**. Bridge HTTPS probe succeeded; zero live
  frames arrived. A read-only OS check found no listener on port 27311 at that time,
  so current live-input and real-lamp appearance acceptance remain outstanding.
  Do not mistake earlier 0.1.0 live evidence below for 0.1.1 lamp acceptance.
- Old package process 28084 was closed normally. New package opened as PID 31784;
  path, window handle/title and responsiveness verified. Protected credential file
  hash stayed unchanged and saved area selection was retained. No new physical
  sync was started; the user must press Start when telemetry is available.

Package: `dist/CrimsonHue-0.1.1-win-x64/CrimsonHue.exe`, 166899483 bytes.
ZIP `dist/CrimsonHue-0.1.1-win-x64.zip`, 68180244 bytes.
SHA256 `6FDBD7D93A3AFA25AC8591F333E051509DF3524203BFFE827FD07311FC92073C`.
All five ZIP entries match on-disk package hashes; no settings, secrets or game
files are included. The old 0.1.0 ZIP hash remains unchanged. No public upload.

Next: user adjusts both distances and walks towards/away from local sources in the
game. Record real-lamp results separately; calibration and future ambient-relative
mixing still require live evidence. Preserve both immutable packages.

## Resolved — diyHue placement save/recovery (2026-09-07)

User authorized fixing the neighboring diyHue project. The running container's
two affected Python files matched the old checkout byte-for-byte. Its log showed
the exact ignored Hue-app PUT, confirming the cause (no longer only an inference).
The repair now applies validated v2 placements, saves groups synchronously and
publishes the updated layout. Twelve isolated placement tests pass; the key
regression fails against the original container image.

The development container was gracefully restarted with the two fixed files.
Its original, still-inactive layout was checked before replaying the exact discarded
user request. Corrected positions were verified through API and disk, then verified
again after a second graceful restart. The paired CrimsonHue diagnostic independently
read these values over pinned HTTPS:

| Channel | Light | X | Y | Z |
| --- | --- | ---: | ---: | ---: |
| 0 | LEDVANCE lamp_001 | 0.31291532926158294 | -0.9982087856556487 | 0.5052717571578222 |
| 1 | LEDVANCE lamp_002 | 0.6081610035675666 | 1 | -0.3940484143872305 |
| 2 | LEDVANCE lamp_003 | -0.9011899756747147 | 1 | -0.8866089323712685 |

This gives rear, front-right and front-left respectively, matching the screenshot's
floor arrangement. Pairings, client keys, certificate and lamp backend settings
were checked unchanged against the ignored local backup. No physical output was
sent. The open CrimsonHue window needs **Refresh areas** to replace its cached
old layout; no CrimsonHue code or package change was required.

Implementation/deployment details and recreate-image caveat are in
`C:\DEV\DiyHue\docs\ENTERTAINMENT-PLACEMENT-FIX.md`.
The local fixed image is `diyhue-ledvance-tuya:placement-fix-20260907`; the existing
container was patched in place. Old images/packages are unchanged. User asked
whether upstream contribution is possible; answered yes via their existing fork
and a focused PR. The user subsequently authorized submission:
[diyHue PR #1119](https://github.com/diyhue/diyHue/pull/1119) is open against `dev`.
Fork branch `fabianviol:fix/entertainment-placement-v2`, commit
`e631fd767518e5f89b7079e2d353e87535527d8b`, contains one focused commit/four files.
It was prepared in `C:\DEV\DiyHue-pr-entertainment-placement` from upstream dev
`daf917799850dfee1bf94719756f80dfface83a0`, without unrelated fork changes.

The dev port uses that branch's existing save API and includes a device-ID lookup
fix in the Entertainment configuration reload path. Twelve tests pass against the
PR source; independent PUT and real Config.load_config regressions both fail on
the unmodified dev base. This submission did not replace the local working bridge
with dev code. Merge/maintainer review remains pending; no monitoring was scheduled.

## Follow-up — Hue app placement mismatch (2026-09-07)

User's Hue screenshot shows lamp_003 left of the monitor, lamp_002 right of it,
and lamp_001 behind the seat. User confirmed the arrangement was saved with Done.
An authenticated, certificate-pinned **GET-only** query of the paired bridge's
area "Spieltisch" instead returned the following raw channel positions:

| Channel | Light | X | Y | Z |
| --- | --- | ---: | ---: | ---: |
| 0 | LEDVANCE lamp_001 | 0.5404710514918292 | -1 | 0.5052717571578196 |
| 1 | LEDVANCE lamp_002 | 0.22516372352075176 | 1 | -0.3940484143872304 |
| 2 | LEDVANCE lamp_003 | -0.3793345270304169 | -1 | 0.7054737741448802 |

The area was inactive. These agree with diyHue's on-disk group configuration, but
put lamp_003 behind the viewer instead of next to the screen. CrimsonHue imports
them verbatim. Its convention (+X right, +Y front, +Z up) agrees with the primary
[HueApi coordinate implementation](https://github.com/michielpost/Q42.HueApi/blob/master/src/HueApi/Models/HuePosition.cs).
No axes were flipped and no physical output was sent during this investigation.

Concrete defect in the neighboring source:
`C:\DEV\DiyHue\BridgeEmulator\flaskUI\v2restapi.py`,
`ClipV2ResourceId.put`, entertainment_configuration branch (line 634), handles
only action start/stop. A PUT with locations/service_locations does nothing but
still returns the resource as a successful response. The POST branch does import
positions. This is a strong explanation for edits appearing saved in the Hue app
while the bridge retains the area's original positions. No request capture from
the phone or deployed-source comparison was performed, so the exact phone request
path remains unverified. A fix belongs in diyHue, not a compensating mirror here;
request user authorization before editing/deploying that neighboring project.

Added `--inspect-layout` to the test executable for repeatable, sanitized, read-only
diagnostics using the paired account's DPAPI store, plus a six-cardinal-direction
regression covering front/back and height independently. **36/36 non-live tests
pass** after these additions (the earlier 36-test run below included `--live`).
No new app package was needed; immutable 0.1.0 remains unchanged. Physical-lamp
acceptance and the upstream ambient-light feed remain pending.

## Result

- WPF desktop UI: HTTPS bridge identity probe, link-button pairing requesting both
  keys, explicit certificate trust, current-user DPAPI storage, area discovery,
  imported positions/channels/names, top-down room preview and synthetic demo.
- Native managed DTLS 1.2 PSK with BouncyCastle 2.7.0, AES-128-GCM-SHA256, HueStream
  v2 RGB packets. No external OpenSSL executable is needed by the application.
- WebSocket reader: fragmented/full messages, 16 MiB bound, schema 1.4/additive 1.x,
  capabilities/axes/basis/state checks, monotonic and source-timestamp freshness,
  duplicate rejection, reconnect/reset and no historical light identities.
- Renderer contributions only, paired-camera directions, player-relative radius,
  soft spatial weighting, ratio-preserving HDR compression, sRGB output, brightness
  cap and smoothing. Hue channel IDs/segments and available brightness balance kept.
- Start/Stop session ownership checks, bounded handshake, stale output clearing,
  release/normal-state restoration after one second without fresh telemetry,
  automatic resumption while armed, explicit restart on bridge failure/takeover.
  Selected lights' on/off, brightness, color/temperature and gradient points are
  restored where available. Running scenes/effects are not resumed.

## Evidence

- Release builds completed with zero warnings/errors.
- **36/36 tests pass**, including real OpenSSL DTLS interoperability, HTTPS
  certificate pinning and link-button pairing against a loopback mock, Windows
  DPAPI, partial WebSocket messages over 64 KiB, cleanup/failure/takeover and Stop
  during bridge activation. Tests use synthetic credentials only.
- Real read-only check: development bridge `192.168.2.109` reports DiyHue Bridge and
  responds over HTTPS. Native application probe succeeds with a certificate.
- Real game WebSocket control: 40 distinct envelopes and 40 distinct rendered
  captures during four seconds. First run up to 44 contributions; later run up to
  19. All parsed/mapped without invalid output. This proves a progressing input
  path, not complete source coverage or accepted real-lamp appearance.
- Actual WPF render inspected at `artifacts/ui-packaged.png` (synthetic demo).
  Packaged executable smoke mode exits 0. Five ZIP files match disk hashes; no
  secrets, settings, ASI or game files included.
- Interactive package started as PID 28084; window title/handle and responsiveness
  verified. Local settings/DPAPI files were subsequently created at 21:30 CEST,
  consistent with the user pairing through the UI. Their contents were not read.
  User explicitly confirmed: "Gekoppelt, Bereich wird angezeigt". Actual bridge
  pairing and Entertainment area selection therefore pass user acceptance.
  Physical lamp output/Start-Stop appearance acceptance remains pending.

## Package

`dist/CrimsonHue-0.1.0-win-x64/CrimsonHue.exe` (self-contained, 166898459 bytes).
ZIP `dist/CrimsonHue-0.1.0-win-x64.zip`, 70292385 bytes.
SHA256 `A7C759FBB47A58127F801AB753A9C70EA2EB3DA9BAA8BC24EB6F540C61F2396E`.
Keep versioned packages immutable. `scripts/Publish.ps1` refuses existing outputs.
No remote repository, GitHub release or Nexus upload was created. Nexus listing
draft is `docs/NEXUS.md`. Source-license/branding choices remain publication work.

## Known limits / next step

User is adding **general ambient lighting upstream in CrimsonDesertTelemetry**.
Do not invent its contract. Consume it separately once its fields, freshness and
color semantics are defined. Current output has only local light contributions.

No depth/occlusion, spotlight attenuation, physical falloff/units, persistent IDs,
sun/sky/emissive completeness or exposure normalization. Camera direction determines
room mapping; player position determines proximity. Hue placement is normalized,
not measured room metres. Renderer culling limits rear visibility. The consumer's
160-channel datagram cap is not a claim the Hue app or physical bridge supports it.
Real Hue hardware and large areas remain untested. Physical backend refresh rate
is not established by the 30/s outbound target. Crash/unreachable bridge restoration
cannot be guaranteed; detected cleanup failures are reported instead of hidden.

Next: user verifies the paired Entertainment area and runs Start/Stop while moving
near game lights. Record visual acceptance and any actionable mismatch; preserve
the current package and issue a new version if code changes are needed.
