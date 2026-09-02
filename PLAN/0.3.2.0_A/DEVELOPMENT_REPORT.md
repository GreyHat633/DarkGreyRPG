# DarkGrey_RPG 0.3.2.0_A — DEVELOPMENT REPORT

## Final status

```text
0.3.2.0_A Release Candidate
IMPLEMENTED
AGENT_VERIFIED
Studio NOT FROZEN
USER_ACCEPTED = NO
```

本报告不代表用户验收，也不授权冻结 Studio、开始 `0.3.2.0_B`、打标、发布或上传 Release。用户已在后续对话中单独授权把 0.3.2.0_A 施工提交并推送到 GitHub；该授权不扩展为 Tag、GitHub Release、合并或二进制资产发布。

## Baseline and worktree boundary

- Construction branch: `codex/0.3.2.0_A`
- Starting HEAD: `becc28c97ae62dbdd314050ef15348f4c05d9125`
- Freeze-blocker continuation baseline: `4e31f180217187c51d61a329fce0f278e13e48b9`
- Baseline authoritative EXE:
  - ProductVersion: `0.3.1.5-rc`
  - Size: `141441750` bytes
  - SHA-256: `9EC1466A...` (baseline record)
- Baseline Studio Core: `365/365` PASS
- Baseline Studio WPF: `414/414` PASS
- The checkout already contained a very large unrelated dirty/deleted set before this construction. It was preserved. Construction and verification performed no reset or clean; GitHub publication was authorized only after verification converged, with unrelated paths excluded.

## Implemented work packages

### WP-A — Session End visible input label

- Renaming an End node now synchronizes the visible Flow Input label.
- Stable port identity remains `flow_in`; existing wire identity is not changed.
- Save/reopen preserves the End display and its visible input label.
- Final live model proof after rename:
  - DisplayName: `接受委托_验收`
  - PortId: `flow_in`
  - PortDisplay: `接受委托_验收`
  - ConnectionCount: `1`
- Evidence: [session-end-display-sync.png](../../.tooling/0.3.2.0_A/final-live/session-end-display-sync.png), [save-reopen-session-persistence.png](../../.tooling/0.3.2.0_A/final-live/save-reopen-session-persistence.png)

### WP-B — Dialogue Inspector order

- Dialogue Inspector is consistently ordered as actor first, line text second.
- Final dark UI Automation geometry: actor top `482`, text top `560`.
- Evidence: [dialogue-inspector-actor-above-text.png](../../.tooling/0.3.2.0_A/final-live/dialogue-inspector-actor-above-text.png), [dialogue-inspector-light.png](../../.tooling/0.3.2.0_A/final-live/dialogue-inspector-light.png)

### WP-C — Flow Judgment scope and menu order

- Flow Judgment is authorable in Story Flow and Session.
- Flow Judgment is not authorable in Task.
- Story and Session final menu sequence was read through UI Automation as:
  `与 → 或 → 非 → 逻辑输入 → 逻辑输出 → 条件判断 → 流程判断`.
- The required ending sequence is therefore exactly `逻辑输出 → 条件判断 → 流程判断`.
- Story and Session nodes were placed, saved and reloaded; Story runtime state is sticky and persists through snapshot/NBT.
- Evidence: [story-logic-menu.png](../../.tooling/0.3.2.0_A/final-live/story-logic-menu.png), [session-logic-menu.png](../../.tooling/0.3.2.0_A/final-live/session-logic-menu.png), [task-logic-menu-no-flow-judgment.png](../../.tooling/0.3.2.0_A/final-live/task-logic-menu-no-flow-judgment.png), [save-reopen-session-persistence.png](../../.tooling/0.3.2.0_A/final-live/save-reopen-session-persistence.png)

### WP-D — DGRS v1 single-file package

- User-facing export now commits exactly one `.dgrs` file, not a same-name directory.
- Construction uses staging plus a temporary file, reopens and validates the produced package, then atomically promotes it.
- Failure cleans temporary/staging data and does not replace an existing valid package.
- ZIP path safety, corruption rejection and overwrite preservation are covered by automated tests.
- Actual format documentation: `studio/docs/DGRS_v1.md`.

Representative final package:

- Path: `.tooling/0.3.2.0_A/final-live/representative-project/build/story_packages/kill_slimes.dgrs`
- Size: `4644` bytes
- SHA-256: `531C86C9B61173FF0AD135BCDF19FAD517CDB361F1BA8665C889FDE0055F7BBF`
- Output directory item count: `1`
- Same-name output folder: absent
- Temporary/staging residual: absent
- Validator result: `PACKAGE_VALIDATION_PASS`
- Evidence: [representative-export-success.png](../../.tooling/0.3.2.0_A/final-live/representative-export-success.png)

### WP-E — Objective prerequisite activation gate

- Canonical property: `prerequisite_enabled`.
- Stable Logic input port ID: `prerequisite`; visible label: `前置条件`.
- Legacy/default state is OFF with no helper and no prerequisite port.
- ON state shows exactly `前置条件为 True 时激活` and creates the Logic input port.
- Toggling OFF removes attached prerequisite wires as one undoable edit; Undo restored the switch, port and wire during final live acceptance.
- Task runtime activation is sticky: false remains inactive, true activates, later true-to-false does not deactivate an already activated objective.
- Runtime/snapshot/NBT and package round-trip paths persist the semantic state.
- The Objective Inspector checkbox text explicitly follows `TextFillColorPrimaryBrush`; the previously black `前置条件` label is readable in the dark theme and remains theme-correct in the light theme.
- Evidence: [objective-prerequisite-off.png](../../.tooling/0.3.2.0_A/final-live/objective-prerequisite-off.png), [objective-prerequisite-on-wired-undo.png](../../.tooling/0.3.2.0_A/final-live/objective-prerequisite-on-wired-undo.png), [save-reopen-objective-persistence.png](../../.tooling/0.3.2.0_A/final-live/save-reopen-objective-persistence.png), [objective-prerequisite-on-light.png](../../.tooling/0.3.2.0_A/final-live/objective-prerequisite-on-light.png)

### WP-F — Export P0 repair

The canonical-only selection/export failure is repaired. Export now resolves the selected canonical Story before applying any legacy-repository path, while invalid canonical content still fails closed.

## 0.3.1.5 为什么合法项目导出失败

### First failing stage

The failure occurred before package construction. `StoryPackageExporter.Build` first called the legacy `StoryRepository.LoadStory` path for the selected ID. The selected project Story `kill_slimes` existed only in canonical resources, so the exporter stopped at legacy Story lookup and emitted:

```text
Story 'kill_slimes' was not found.
```

No DGRS writer or final-output transaction was reached.

### Root cause

The export pipeline resolved a canonical project selection through a legacy-only repository before canonical resource resolution. The selection model and exporter lookup model were inconsistent; this was not a filesystem extension problem and could not be correctly repaired by suppressing validation or renaming a folder to `.dgrs`.

### Repair

- Resolve the selected canonical Story and canonical membership/resource closure first.
- Retain legacy compatibility where a real legacy Story exists.
- Build the package payload, write DGRS staging/temp output, reopen and validate it, then atomically commit one `.dgrs`.
- Keep strict canonical validation enabled.

The representative canonical project now exports successfully from the final Release EXE and validates after reopen.

## DGRS v1 actual contract

Manifest identity:

```text
format = dgrs
format_version = 1
producer = DarkGreyRPGStudio
producer_version = 0.3.2.0
schema_version = 1
package_id = kill_slimes
package_version = 0.3.2.0
story_id = kill_slimes
story_schema_version = 1
```

Representative package entries:

```text
actors/slimes.json
actors/tarven_boss.json
items/copper_coin.json
manifest.json
project.json
resources/canonical/memberships/kill_slimes.json
resources/canonical/sessions/tarven_session.json
resources/canonical/stories/kill_slimes.json
resources/canonical/tasks/kill_slimes.json
```

Reader boundary for this A release is package reopen/validation; Minecraft-side consumption is not silently claimed.

### WP-G — Graph selection and Shift-splice ergonomics

- Existing marquee selection remains the source of temporary node selection; no permanent Group resource or replacement selection framework was added.
- A selected-node drag preserves the complete temporary selection and applies one shared pointer delta to every selected node.
- One layout transaction covers the complete multi-node drag; one Undo restores every moved node.
- Multi-selection Shift-drag cannot create a splice candidate or change graph topology; single-node Shift-splice remains available.
- Single-node Shift-splice accepts both-sided (`1` matching input + `1` matching output), input-only (`1 + 0`) and output-only (`0 + 1`) candidates for both Flow and Logic wires.
- The preview renders exactly two replacement ghosts for a both-sided splice and one ghost for either one-sided splice. No matching ports, ambiguous matching ports, occupied capacity and interface-kind mismatches remain fail-closed.
- Commit replaces only the original wire with the planned replacement set in one graph edit; one Undo restores the original wire.
- Right-clicking a selected node preserves the selection; right-clicking an unselected node replaces it.
- Multi-selection Edit is a no-op.

### WP-H — Atomic multi-delete and layout persistence

- Keyboard Delete and context-menu Delete call the same selection deletion command.
- Deletable nodes and all incident wires are removed in one graph transaction; one Undo restores nodes, wires, and layout.
- Required/non-deletable nodes can move with the selection but survive mixed batch deletion.
- Session and Task placement deletion retains the resource objects.
- TextBox, ComboBox, and editable controls retain Delete-key ownership.
- Multi-node positions persist through Save, complete Studio shutdown, and reopen.

## Automated verification

| Gate | Result | Evidence |
|---|---:|---|
| Studio Core full | `378/378` PASS | final Release test invocation |
| Studio WPF full | `441/441` PASS | final Release test invocation |
| Solution Release build | PASS, `0` warnings, `0` errors | final build output |
| Java JDK 8 formatting/compile/probes | PASS | `spotlessCheck`, compile, `42` existing/new probes; `58` Gradle tasks (`40` executed, `18` up-to-date) |
| DGRS reopen validator | `PACKAGE_VALIDATION_PASS` | representative `.dgrs` listed above |
| Diff whitespace check | PASS | `git diff --check` (CRLF warnings only) |

Java coverage includes Task prerequisite runtime/instance/event persistence/journal/Forge paths and Story Flow Judgment runtime/instance/snapshot/NBT/Forge paths.

## Final Release EXE and acceptance lineage

Authoritative artifact:

```text
E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe
Build: self-contained Windows x64 Release
ProductVersion: 0.3.2.0-rc
FileVersion: 0.3.2.0
Size: 141522134 bytes
SHA-256: BE0DC0C738342DD7843A5CC063A21594300F7ADACF27BBBC1819591A1E14AA14
Directory file count: 1
```

The final Multi-Selection live-window sequence in `.tooling/0.3.2.0_A/multiselection-live/evidence` was produced from the authoritative EXE hash above and finished `AGENT_VERIFIED`. Earlier WP-A through WP-F screenshots remain tied to their recorded earlier RC hash; they are historical evidence, not falsely relabeled as same-hash final evidence. The complete Core/WPF and Gradle regression suites were rerun after final source convergence.

Final Release EXE live coverage includes:

- WP-A rename, visible End input label, preserved wire and save/reopen.
- WP-B actor-above-text Inspector order.
- WP-C Story/Session/Task menu scope, exact order, placed Story/Session nodes and save/reopen.
- WP-E OFF/ON states, exact helper text, port creation, physical wire drag, OFF cleanup, Undo restoration and save/reopen.
- WP-D/F representative non-empty Story export, one-file output, validator reopen and manifest/entry listing.
- One workspace-level unsaved-close dialog with `保存 / 不保存 / 取消`: [unsaved-close-single-dialog.png](../../.tooling/0.3.2.0_A/final-live/unsaved-close-single-dialog.png).
- Dark/Light readability for changed Objective, Dialogue and Session/Flow surfaces: [changed-surface-light.png](../../.tooling/0.3.2.0_A/final-live/changed-surface-light.png), [objective-prerequisite-on-light.png](../../.tooling/0.3.2.0_A/final-live/objective-prerequisite-on-light.png), [dialogue-inspector-light.png](../../.tooling/0.3.2.0_A/final-live/dialogue-inspector-light.png).
- Four-node marquee selection, real-time equal-delta movement, one-step move Undo, Shift-splice suppression, keyboard/context atomic delete, context Edit no-op, required-node mixed movement/deletion, editable-control Delete focus protection, and Save/full-close/reopen layout persistence.
- Flow and Logic Shift-splice live runs for both-sided/input-only/output-only shapes, exact ghost counts, one-step Undo, Shift-release cancellation and multi-selection suppression.
- Exact-project `interact_actor` Inspector readability and export through the real Windows save-path dialog from the authoritative EXE hash above.

## Exact user project boundary

Exact project tested without modification:

```text
E:\Java\MinecraftMod\RPGProject\project_test
```

At the final freeze-blocker recheck, the current on-disk Task resource no longer matched the earlier validation-failure snapshot. Its second Objective is now explicitly `objective_type = interact_actor` with `actor_id = tarven_boss`; it does not use an `entity` target. The Task JSON was treated as user-owned input and was not edited during acceptance:

```text
SHA-256 before = CC154B9D4BC33F426888DE534A6A9294B131080D4A894006096641020251951D
SHA-256 after  = CC154B9D4BC33F426888DE534A6A9294B131080D4A894006096641020251951D
```

The authoritative final EXE displayed `角色交互 / 酒馆老板`, opened the real Windows `导出故事包` save-path dialog, and generated:

```text
E:\Java\MinecraftMod\RPGProject\project_test\build\story_packages\kill_slimes.dgrs
Size: 5559 bytes
SHA-256: 9D8DA59B83CA61F47E9CB98B2C969F053EB3EBDF80D764AB9C5A542AA64089EC
Archive entries: 10
```

Reopening the package payload confirms the kill Objective has `entity = slimes` and the interaction Objective has `actor_id = tarven_boss` plus `objective_type = interact_actor`. Gate F4 is therefore `AGENT_VERIFIED` for the exact current project state. Evidence: [exact-task-interact-actor-target.png](../../.tooling/0.3.2.0_A/freeze-blockers/exact-live-evidence/01-exact-task-interact-actor-target.png), [exact-project-final-exe-export-success.png](../../.tooling/0.3.2.0_A/freeze-blockers/exact-live-evidence/02-exact-project-final-exe-export-success.png).

## Original audit final reread

`PLAN/0.3.1.5审计.docx` was reread in full at final acceptance through its OOXML text and all six embedded images. The reread reconfirmed the six A items and the B boundary. LibreOffice was unavailable, so no claim is made that a fresh page-rendered PDF was produced; paragraph content and embedded visual evidence were both inspected.

The following remain explicitly outside `0.3.2.0_A` and were not implemented:

- Minecraft task/objective UI redesign.
- Copper coin / gold-nugget content cleanup.
- Storage chest HUD/toggle-tip changes.

## Acceptance matrix

| ID | Gate | Status | Evidence summary |
|---|---|---|---|
| A1 | End Flow Input sync DisplayName | AGENT_VERIFIED | final EXE live screenshot/model |
| A2 | End rename preserves wire | AGENT_VERIFIED | `flow_in`, connection count 1 |
| A3 | Save/reopen End name | AGENT_VERIFIED | final EXE process restart |
| B1 | Dialogue actor above | AGENT_VERIFIED | dark/light screenshot + UIA geometry |
| B2 | Dialogue text below | AGENT_VERIFIED | dark/light screenshot + UIA geometry |
| C1 | Story can add Flow Judgment | AGENT_VERIFIED | final EXE menu + placed node |
| C2 | Session can add Flow Judgment | AGENT_VERIFIED | final EXE menu + placed node |
| C3 | Task cannot add Flow Judgment | AGENT_VERIFIED | final EXE menu + model |
| C4 | Exact menu ending order | AGENT_VERIFIED | UIA sequence + screenshots |
| C5 | Story runtime | AGENT_VERIFIED | Java runtime/instance/snapshot/NBT probes |
| C6 | Session regression | AGENT_VERIFIED | full regression + final EXE live |
| D1 | One `.dgrs` output | AGENT_VERIFIED | filesystem item count 1 |
| D2 | Manifest/version | AGENT_VERIFIED | validator manifest output |
| D3 | Reopen/validate | AGENT_VERIFIED | `PACKAGE_VALIDATION_PASS` |
| D4 | Corrupt package rejected | AGENT_VERIFIED | automated package tests |
| D5 | Failed overwrite preserves old | AGENT_VERIFIED | automated transaction tests |
| E1 | Legacy default OFF | AGENT_VERIFIED | Core/WPF tests + final EXE OFF state |
| E2 | ON helper + Logic input | AGENT_VERIFIED | final EXE live |
| E3 | false remains inactive | AGENT_VERIFIED | Java probe |
| E4 | true activates | AGENT_VERIFIED | Java probe |
| E5 | true→false stays active | AGENT_VERIFIED | Java probe |
| E6 | DGRS round-trip | AGENT_VERIFIED | package validator/tests |
| F1 | 0.3.1.5 failure reproduced | AGENT_VERIFIED | exact prior error captured |
| F2 | Root cause documented | AGENT_VERIFIED | dedicated report section |
| F3 | Representative valid export | AGENT_VERIFIED | final EXE + validator |
| F4 | Exact user project export | AGENT_VERIFIED | real save-path dialog, 10-entry DGRS, package payload rechecked |
| G1 | Temporary multi-selection move | AGENT_VERIFIED | equal-delta real-window drag |
| G2 | Multi-move one Undo | AGENT_VERIFIED | one-step real-window restore |
| G3 | Multi-selection Shift safety | AGENT_VERIFIED | topology unchanged under Shift drag |
| G4 | Flow splice both/input/output | AGENT_VERIFIED | final EXE ghosts `2/1/1`, commit and Undo |
| G5 | Logic splice both/input/output | AGENT_VERIFIED | final EXE ghosts `2/1/1`, commit and Undo |
| H1 | Keyboard/context atomic delete | AGENT_VERIFIED | shared command + one-step restore |
| H2 | Required-node mixed selection | AGENT_VERIFIED | moves with peers and survives delete |
| H3 | Placement/resource boundary | AGENT_VERIFIED | WPF lifecycle regression |
| H4 | Editable Delete focus guard | AGENT_VERIFIED | real-window + WPF regression |
| H5 | Multi-layout save/reopen | AGENT_VERIFIED | complete process restart |
| R1 | Unsaved close | AGENT_VERIFIED | one final-EXE workspace dialog |
| R2 | Actor/Item Inspector | AGENT_VERIFIED | full WPF regression; no P0/P1 failure |
| R3 | Wire center | AGENT_VERIFIED | WPF geometry regression + final live Objective wire |
| R4 | Single reconnect | AGENT_VERIFIED | full WPF regression |
| R5 | Multi/Ctrl reconnect | AGENT_VERIFIED | full WPF regression |
| R6 | Choice Flow-only | AGENT_VERIFIED | Core/WPF regression |
| R7 | Draft/Commit | AGENT_VERIFIED | WPF regression + final save/export flush |
| R8 | Node layout restart | AGENT_VERIFIED | WPF regression + final process restart |
| R9 | Resource lifecycle | AGENT_VERIFIED | Core/WPF regression |
| R10 | Light/Dark changed surfaces | AGENT_VERIFIED | final-EXE screenshots + WPF theme regression |
| R11 | Objective prerequisite label contrast | AGENT_VERIFIED | final dark-window screenshot + dynamic theme test |

## User acceptance remaining

The implementation is ready for user acceptance at the authoritative EXE path. Only the user may:

1. record `USER_ACCEPTED`;
2. freeze Studio;
3. authorize `0.3.2.0_B`, Tag, GitHub Release, merge, or binary-asset publication.
