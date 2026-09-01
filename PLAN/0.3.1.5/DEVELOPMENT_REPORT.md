# DarkGrey_RPG 0.3.1.5 — Studio Finalization Construction Report

状态：`CONSTRUCTION_ACTIVE / IMPLEMENTED / AUTOMATED_PASS / PARTIAL_FINAL_HASH_LIVE_PASS / SOURCE_BRANCH_PUBLISHED / USER_ACCEPTED=NO`

日期：2026-09-02

## 1. Authority and baseline

- Construction basis: `PLAN/DarkGrey_RPG_0.3.1.5_Studio_Finalization_Regression_Construction_Plan.md`.
- Human-audit basis: `PLAN/0.3.1.4审计.docx`; the audit text and its embedded screenshots were inspected before implementation.
- Baseline commit: `a3338e0bfe85f59f61fd242750d5e9e760924b8e` (`0.3.1.4`).
- Working branch: `codex/0.3.1.5`.
- The pre-existing dirty files `.codex/config.toml`, `AGENTS.md`, and `PLAN/0.3.1.2B/DEVELOPMENT_REPORT.md`, as well as unrelated legacy PLAN documents/caches, were not adopted into this construction package.
- The audited source package, current PLAN, and `0.3.1.4审计.docx` were committed as `eb163ec907da7465b0055ac83a571a7281c3208c` and pushed to `origin/codex/0.3.1.5`.
- No merge, tag, GitHub Release, or binary asset upload was performed.

## 2. Implemented work packages

### WP-A — Unsaved close protection

- Replaced per-editor close resolution with one workspace-level dirty decision.
- Added a dedicated WPF close dialog with the exact actions `保存 / 不保存 / 取消`.
- The close path flushes the active UI draft before dirty evaluation.
- `保存` routes through the existing save-all boundary and closes only after success.
- `不保存` closes without writing outstanding edits; `取消` keeps the process and in-memory state open.
- Save failure remains open and is routed through the existing error path.
- Automated coverage includes multiple simultaneously dirty documents, one prompt, save-all, cancel, discard, and save-failure behavior.

### WP-B — Actor / Item Inspector simplification

- Removed the `资源属性` section and the separate `显示名称` field.
- Removed ownership/reference-state presentation from the affected Inspector surface.
- Actor, Actor Group, Item, and Item Group now show one identity line and one `标签：` row.
- Tags use a separate value column so wrapped lines align under the tag value instead of under the label.

### WP-C — wire anchor correctness

- The visual port remains 9–11 DIP while the interaction slot is 20 DIP.
- Wire endpoints now resolve to the center of the visible port glyph, not the label/slot center.
- Input and output anchors, single reconnect, multi-port ordinary drag, Ctrl bundle reconnect, hover/glow clearing, and no-phantom click behavior were revalidated against the packaged EXE.

### WP-D — Action authoring terminology

- Canonical authoring names are now `物品给予`, `经验给予`, and `消息发送`.
- Node title, menu/dropdown text, inline editor, Inspector label generation, and tests use the same terms.
- A source residual scan found no old `给予物品 / 给予经验 / 发送消息` authoring labels in the changed Studio source.

### WP-E — Choice simplification and Flow Judgment

- Newly authored Choice nodes expose `flow_in` plus one Flow output per option; they no longer create a paired Logic output for every option.
- Old persisted Choice Logic ports/connections are retained as compatibility data and are presented separately as `旧版逻辑输出（兼容）`; migration is lossless.
- Added Session-only `flow_judgment` / `流程判断` with `flow_in`, `flow_out`, and Logic output `executed` / `执行状态`.
- Runtime state is sticky false→true, participates in snapshots and NBT, and continues Flow after execution.
- `流程判断` is immediately after `条件判断` under `添加节点 > 逻辑`.

## 3. Fresh automated evidence

| Area | Result | Evidence |
|---|---|---|
| Studio Core | `365/365 PASS` | `.tooling/0.3.1.5/final/testresults/core/Core-0.3.1.5-final.trx` |
| Studio WPF | `414/414 PASS` | `.tooling/0.3.1.5/final/testresults/wpf/Wpf-0.3.1.5-final.trx` |
| Java formatting/compile/runtime | `PASS` | `spotlessJavaCheck`, `testClasses`, `canonicalSessionRuntimeProbe`; probe printed `CANONICAL_SESSION_RUNTIME_PROBE=PASS` |
| Java full probe sweep | `PASS` | all 38 JavaExec probe tasks completed successfully earlier in this construction run |
| Patch hygiene | `PASS` | `git diff --check` clean before report generation |

The first Java recheck invocation did not enter Gradle tasks because the new shell had no `JAVA_HOME`. It was rerun with the E-drive JDK 8 at `.tooling/0.3.1.5/gradle-home/jdks/azul_systems__inc_-8-amd64-windows.2` and then passed. This was an environment precondition, not a product-test failure.

## 4. Authoritative runnable artifact

The newest self-contained single-file Windows x64 client is present at the mandatory delivery path:

`E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`

| Property | Value |
|---|---|
| ProductVersion | `0.3.1.5-rc` |
| FileVersion | `0.3.1.5` |
| Size | `141441750` bytes |
| SHA-256 | `9EC1466A2D35AD8428DA43C98C3CC2BDDA3D422649A180712A1F95554641967C` |
| Files at delivery directory root | `1` |

This is a local acceptance candidate. It is not a claim that the PLAN's complete Release Acceptance Matrix or user acceptance has finished.

An initial multi-file publish was not retained at the authoritative path. It was moved intact to the recoverable local backup `.tooling/0.3.1.5/final/mistaken-multifile-backup`; the authoritative path was then recreated with only the required single EXE.

## 5. Final-hash Release EXE evidence already completed

All successful live gates below ran the exact SHA-256 listed above.

| Gate | Result | Evidence |
|---|---|---|
| Wire interaction regression | `PASS` | `.tooling/0.3.1.5/live/stage3-wire-gate.json`; 27 continuous frames under `.tooling/0.3.1.5/live/stage3-wire-clean` |
| Canonical authoring smoke | `18/18 UI + 18/18 disk PASS` | `.tooling/0.3.1.5/live/s20-authoring/s20-canonical-authoring-smoke.json` |
| Finalization UI gate | `PASS` | `.tooling/0.3.1.5/live/finalization-ui-v2/finalization-ui-gate.json`; 10 screenshots |

The finalization UI gate directly verified:

- Actor, Actor Group, Item, and Item Group Inspector identity/tag presentation;
- new Choice Flow-only ports;
- `条件判断` followed immediately by `流程判断` in the real menu;
- visible Flow Judgment node and its three required ports;
- exact close-dialog actions;
- dirty-close `取消` preserves the process;
- dirty-close `保存` persists Flow Judgment;
- restart shows the saved node;
- clean close exits without a prompt.

Two old 0.3.1.4 drivers were also tried and intentionally not counted as product PASS/FAIL:

- the old Stage 2 driver expected the removed `StoryStartTriggerName` automation seam;
- the old S13–S16 Inspector driver expected the removed split label/value automation IDs.

Both reached the new UI, but their stale assertions could not describe the new contract. The dedicated 0.3.1.5 gate replaced the affected checks.

## 6. Release Acceptance Matrix — current construction state

The vocabulary is limited to the PLAN's allowed states. `AGENT_VERIFIED` means final-hash Release evidence exists; it never means `USER_ACCEPTED`.

| ID | State | Current evidence / remaining proof |
|---|---|---|
| A1 clean close | `AGENT_VERIFIED` | finalization gate clean restart/close |
| A2 dirty close → Save | `AGENT_VERIFIED` | dialog interaction, disk assertion, restart screenshot |
| A3 dirty close → Don't Save | `AGENT_VERIFIED` | audit reread gate: real dialog click, process exit, disk remains at pre-edit value |
| A4 dirty close → Cancel | `AGENT_VERIFIED` | dialog interaction proves process remains open |
| A5 multiple dirty editors → one prompt | `IMPLEMENTED` | automated multi-document proof passes; dedicated Release run remains |
| B1 Actor Inspector | `AGENT_VERIFIED` | `actor-inspector.png` |
| B2 Actor Group Inspector | `AGENT_VERIFIED` | `actor-group-inspector.png` |
| B3 Item Inspector | `AGENT_VERIFIED` | `item-inspector.png` |
| B4 Item Group Inspector | `AGENT_VERIFIED` | `item-group-inspector.png` |
| B5 tag hanging-indent wrapping | `AGENT_VERIFIED` | audit reread gate: fresh long-tag Release screenshot and value-column geometry |
| C1 Flow input center | `AGENT_VERIFIED` | exact-coordinate test plus packaged frames |
| C2 Flow output center | `AGENT_VERIFIED` | exact-coordinate test plus packaged frames |
| C3 Logic input center | `AGENT_VERIFIED` | exact-coordinate test plus packaged frames |
| C4 Logic output center | `AGENT_VERIFIED` | exact-coordinate test plus packaged frames |
| C5 reconnect center | `AGENT_VERIFIED` | Flow/Logic continuous reconnect frames |
| D1 物品给予 | `AGENT_VERIFIED` | audit reread gate: real node selection, Inspector type and 物品/数量 labels |
| D2 经验给予 | `AGENT_VERIFIED` | audit reread gate: real node selection, Inspector type and 经验值 label |
| D3 消息发送 | `AGENT_VERIFIED` | audit reread gate: real node selection, Inspector type and 消息 label |
| E1 Choice Flow-only | `AGENT_VERIFIED` | screenshot plus model/source tests |
| E2 Choice rename stable wire | `IMPLEMENTED` | model/edit tests pass; fresh dynamic frames remain |
| E3 Choice reorder stable wire | `IMPLEMENTED` | model/edit tests pass; fresh dynamic frames remain |
| E4 Flow Judgment ports | `AGENT_VERIFIED` | node screenshot plus UIA/source assertions |
| E5 runtime false→true | `AGENT_VERIFIED` | fresh Java Runtime probe and snapshot/NBT tests |
| E6 menu path | `AGENT_VERIFIED` | real context-menu traversal/screenshot |
| E7 immediately after Condition | `AGENT_VERIFIED` | real visible-menu order assertion/screenshot |
| R1 single reconnect | `AGENT_VERIFIED` | packaged Flow and Logic frame sequences |
| R2 multi ordinary drag | `AGENT_VERIFIED` | packaged Flow and Logic frame sequences |
| R3 Ctrl multi reconnect | `AGENT_VERIFIED` | packaged Flow and Logic frame sequences |
| R4 port glow clearing | `AGENT_VERIFIED` | before/leave frames |
| R5 Draft/Commit typing | `AGENT_VERIFIED` | S20 real authoring/save/restart plus full regression suites |
| R6 Objective 100-switch | `IMPLEMENTED` | automated regression passes; fresh 0.3.1.5 live 100-switch run remains |
| R7 node layout restart | `AGENT_VERIFIED` | S20 pre-close/restart disk and UI evidence |
| R8 per-graph viewport | `IMPLEMENTED` | S20 restart evidence exists; dedicated fresh five-graph viewport driver remains |
| R9 lifecycle asymmetry | `AGENT_VERIFIED` | S20 UI/disk lifecycle assertions |
| R10 Light theme | `NOT_STARTED` | fresh 0.3.1.5 Light-theme screenshots/interactions remain |

## 7. Current boundary and next work

The requested construction has started and produced a runnable, tested 0.3.1.5 implementation. It is not yet correct to mark Phase 7–9 complete because the matrix rows above still need fresh packaged interaction evidence.

Next bounded work, in PLAN order:

1. run A5 in the packaged EXE;
2. capture E2/E3 rename/reorder dynamic evidence;
3. rerun Objective 100-switch, five-graph viewport, and Light Theme against the final hash;
4. perform the final no-go review and only then decide whether technical gates can be marked complete;
5. leave `USER_ACCEPTED` to the user and leave all Git/publication operations unauthorized unless explicitly requested.

The four entries from `PLAN/0.3.1.4审计.docx` have now been reread one by one against the final-hash executable. Their dedicated evidence and the precise Stage 3 scope boundary are recorded in `PLAN/0.3.1.5/AUDIT_0314_ACCEPTANCE.md`.

`0.3.1.5_CONSTRUCTION=ACTIVE / IMPLEMENTATION_READY / FINAL_ACCEPTANCE_INCOMPLETE`
