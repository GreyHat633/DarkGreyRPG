# 0.3.2.1 development and real-machine evidence

Date: 2026-09-10. Branch: `codex/0.3.2.1`. Base: `effce10e48beb694904627a9284a930cd0e3db9c`.

M1–M5 are implemented. The Minecraft client was exercised in an isolated real Forge 1.7.10 + CustomNPC+ process, including the integrated server. This is a local acceptance candidate, not a published Release or user acceptance.

- `0.3.2.1_USER_ACCEPTED=NO`
- `REAL_MACHINE_GATES=PASS` (agent-driven game-native actions; evidence limits below)
- `STUDIO_DIFF=0`
- `FROZEN_RUNTIME_SCHEMA_DIFF=0`
- `FREEZE_GUARD=PASS`
- Standard unrestricted build closure remains open because of the pre-existing frozen-file Spotless issue described below.

## Implemented scope

- Bottom-quarter dialogue panel, empty portrait slot, wrapped Chinese text and scrolling; only panel clicks advance lines. Separate paged vanilla choice buttons retain prior context for promptless choices. Existing session transport, node identity, continuation and Escape semantics remain intact.
- Shared two-column Entity/Item Nominator: packages at left, unified typed resources at right, global ID/name/tag search with source package retained per result. Existing server validation, packet contracts, revisions and actual inventory container/slot mechanics remain in use.
- `/dgr inspect`: default OFF, separate overworld saved data keyed by player UUID. Effective state is persistent preference OR equipped goggles. Goggles occupy the head slot with zero armor and no durability.
- Nearby living-entity IDs are provided by the server; rendering excludes identityless entities and dropped items. Ordinary GUI item tooltips use the server-synchronized existing item matcher catalog and a bounded stack/revision cache. No presentation-only identity is written into item NBT.
- Default rebindable I key opens the Canonical Task screen. It displays all active objectives, removes completed objectives, supports multiple tasks, and removes settled tasks. Requests and snapshots are player-specific and correlated; no permanent HUD or legacy journal DTO is used.
- Story chooser emphasizes display name and shows status separately, preserving server token/index arbitration.
- Two bounded presentation message registrations (17 CLIENT, 18 SERVER); creator snapshot compressed/decompressed limits and exact task request size are checked.

## Automated evidence

The baseline had 51 registered JavaExec probes. Final verification adds `creatorUxProbe` and `creatorNetworkDiscriminatorProbe` (53 total). All 53 passed (`BUILD SUCCESSFUL`). Evidence: `.tooling/0.3.2.1/final-regression-r2.log`. Final packaging after a trailing-space cleanup also passed: `.tooling/0.3.2.1/delivery-build.log`.

New coverage includes dialogue context/action identity/geometry, UUID preference persistence and goggles truth table, package-scoped global search, item catalog transport and revision, malformed/oversized network input, discriminator uniqueness, parallel active task objectives, transition and settlement projection. `CreatorUxProbe live` additionally exercised actual vanilla ItemStacks inside the Minecraft classloader (`ITEM_INSPECT_LIVE_MATCH_PROBE=PASS`).

One old surface probe asserted removed prototype `NPC_ID`/`Group_ID` label literals. Its presentation assertion now verifies separate NPC and Group binding fields in the unified list. All other assertions in that probe and all frozen runtime tests are retained. The freeze guard explicitly permits only this one additional test file.

Reproduction (E-drive JDK and caches):

```powershell
$env:JAVA_HOME='E:/Java/jdk-25.0.1'
$env:GRADLE_USER_HOME="$PWD/.gradle-user-home"
$probes = Get-Content .tooling/0.3.2.1/baseline-tasks.txt
./gradlew.bat build @probes creatorUxProbe creatorNetworkDiscriminatorProbe -I scripts/0321-client-format.gradle '-PdgrsPath=E:/Java/MinecraftMod/DarkGrey_RPG/.tooling/0.3.2.0_B2/real-minecraft-20260903/story-packages/kill_slimes.dgrs' --offline --no-daemon --no-configuration-cache --max-workers=2 --console=plain
./scripts/verify-0321-freeze.ps1
```

### Existing unrestricted formatting failure

The unmodified B4 baseline fails ordinary `build` on existing Spotless formatting in `CanonicalGraphResourceLoader.java` and `CanonicalGraphResourceLoaderProbe.java`. Both remain frozen and unchanged. The explicit `scripts/0321-client-format.gradle` init script scopes Java formatting to reviewed 0.3.2.1 paths; compilation, packaging, other checks and regression probes still run. A build with this script is not a claim that the unrestricted baseline formatting failure was repaired. Baseline evidence: `baseline.log`, `baseline-functional.log` in `.tooling/0.3.2.1`.

## Real-machine gates

All paths below are relative to `.tooling/0.3.2.1/`. Screenshots are real Minecraft framebuffer captures under `live-client/screenshots/`. Timestamped requests/results are in `live-actions.jsonl`; game/server output is in `live-run-*.log`.

| Gate | Observed result | Evidence |
| --- | --- | --- |
| A Dialogue | LINE → LINE → promptless CHOICE → branch LINE → END; background click retains node, panel click advances, Escape retains choice; Chinese context visible at 640×480 / effective GUI 320×240 | `26-lowres-line1.png`, `27-lowres-choice-context.png`, `28-lowres-branch-line.png`, run 7 |
| B Entity Nominator | NPC + Group list; individual/group bindings accepted; global full ID and tag results retain both Consumer and Provider scopes | `02-entity-browser.png`, `10-global-search.png`, `33-entity-global-tag.png`, server bind success logs |
| C Item Nominator | Actual inventory pickup → target slot → bind; Item and two Item Groups accepted; global tag query shows both kinds and packages; all slots visible at low resolution | `17-item-ready.png`, `20-multiple-groups-tooltip.png`, `29-lowres-item-slots.png`, `34-item-global-tag.png` |
| D Inspect persistence | Initial OFF; ON survives world rejoin and full client/integrated-server process restart; OFF survives rejoin; UUID persistence independently probed | run 5–8, actions; run 8 startup status ON/revision 4 |
| E Goggles | With preference OFF: equip ON, remove OFF. With preference ON: equip/remove both ON | timestamped actions and status responses in runs 7–8 |
| F Identity | Individual, group-only, both, identityless exclusion; vanilla tooltip shows Item + two Groups; actual-stack no-match test passes | `11-inspect-fixed.png`, `25-individual-plus-group.png`, `19-item-tooltip-hover.png`, `20-multiple-groups-tooltip.png`, live matcher marker |
| G Task | I key-binding event opens Canonical screen; real player-attributed kills yield 0/3 → 1/3 → 2/3 → next interaction objective; interaction settles task and removes entry; reward succeeds and Story terminates | `31-final-task-1.png`, `31-final-task-2.png`, `31-final-task-3-next-objective.png`, `32-final-settlement-success.png`, run 7 |
| H Chooser | Two available Stories; choosing Consumer terminates Consumer while Provider remains unstarted | `24-story-chooser.png`, run 7 at 19:12:08–09 |

Completed objectives are intentionally removed, so the completed 3/3 row transitions directly to the next objective; it is not retained as task history. The fixture's second objective uses an authored kill-like description despite being an interaction objective; UI preserves that source text.

### B4 samples and fixture corrections

- Repeatable Provider completed three fresh runs with activation times `1789039155619`, `1789039156320`, `1789039156971`; Consumer's ONCE activation remained `1789038728460` (run 8). ONCE kill_slimes remained terminated after another interaction. ACTIVE kill_slimes interaction preserved the same task placement and progress instead of restarting.
- Actual Canonical Session continuation, task death events, package UPDATED/UNCHANGED reload and retirement, actor chooser arbitration and `Provider:guard` / item full IDs were exercised. Broader lifecycle/repeat/rollback coverage is in the full baseline regression suite.
- Original Studio-produced B2/B4 packages were copied into the isolated test directory. Only these copies gained extra item/group/tag fixtures, a second dialogue line, and repeatable policy for Provider; no authoring schema or original package changed.
- The first full task's reward failed because the isolated world lacked a concrete `copper_coin` item binding. After binding an iron ingot with the existing debug Nominator path, the task was rerun through real kills and interaction: reward stack increased from 1 to 11, Story reached TERMINATED, and task list was empty.
- Early Inspect rendering was corrected after discovering that Minecraft 1.7 does not synchronize living-entity UUIDs to clients. The final transport uses transient server entity IDs scoped to the current world/dimension. Early failed screenshots are retained as diagnostic history, not passing evidence.
- Early harness clicks lacked mouse release and one kill loop selected dead entities. The harness was corrected to dispatch complete GUI click/release and select living bound targets; passing item/task evidence comes after those corrections.

### Evidence limits

The Windows native screenshot API failed on this Windows 10 host. Real-machine automation used an opt-in, localhost-only test mod to invoke the actual game GUI callbacks, container packets, server interactions, player-attributed damage, and key-binding events; screenshots came from Minecraft's own framebuffer. This proves game behavior, not physical keyboard/mouse hardware or user acceptance. I was verified through the actual registered key-binding/FML handler, not by claiming a successful physical key press. No multiplayer dedicated-server session was performed.

The harness, test worlds, edited test packages and localhost endpoint live only under `.tooling/0.3.2.1`. They are excluded from the delivered mod JAR. The test client/server was shut down after testing. User worlds and the original `run` directory were not modified.

## Delivery

See `ARTIFACT.json` beside this document for the final local JAR path, version, size and SHA-256. The JAR contains no acceptance harness or test probe classes. Studio is frozen, so its EXE was not rebuilt or promoted. No tag or GitHub Release was made; pre-existing dirty workspace content was preserved.
