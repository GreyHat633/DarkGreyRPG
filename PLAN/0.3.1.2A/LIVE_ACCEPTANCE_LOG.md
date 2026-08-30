# DarkGrey RPG Studio 0.3.1.2A Live Acceptance Log

## Candidate identity

- Build source SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
- EXE: `E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio-0.3.1.2A\DarkGreyRPGStudio.exe`
- File version: `0.3.1.2`
- Product version: `0.3.1.2A`
- Size: `141338326` bytes
- SHA-256: `A460437194662A3E0F4E624B0F5C541EB4A0312352506CDEA05DD64D109D2971`
- Host display scale: 125%.
- Test project: `.tooling/0312a-live/project` (disposable copy of the DarkGrey 2.1 acceptance fixture).

The standalone self-contained Release EXE was controlled through native Windows UIAutomation and Win32 mouse, keyboard, cursor, and screen-capture APIs. No WPF test host was used for the live results below.

## Measurements and operations

### A1 empty project

- At logical `1100x700`, the button center was `x=876.5`; the right content panel center was `x=876`.
- At logical `1700x980`, the button center was `x=1044.5`; the right content panel center was `x=1044`.
- Both captures were made on the host's 125% display scale. A 100% DPI session was not available, so A1 remains blocked for the missing DPI pair.

### A2 Left Alt

- Ten key-down/key-up cycles were executed against blank canvas, node, and wire-near positions in rotation.
- The ten screenshots and contact sheet show no reproduced white rectangle.
- PLAN requires `BLOCKED — NEED_USER_VERIFICATION` when the original defect cannot be reproduced; focus/access-key/mouse-capture instrumentation was not complete enough to override that rule.

### A4 resource context menus

- Actor `侦探`: `编辑 | 新建角色 | 引用已有角色 | 删除资源`.
- Item `证物令牌`: `编辑 | 新建物品 | 引用已有物品 | 删除资源`.
- Session `最终质询`: `编辑 | 新建会话 | 引用已有会话 | 删除资源`.
- Task `搜集证物`: `编辑 | 新建任务 | 引用已有任务 | 删除资源`.
- Each menu came from the resource item, not the folder header.

### A5 inline editing

- Session Line was clicked directly without prior node selection, changed from `证物就在你手里。现在，作出选择。` to `证物就在你手里。现在，作出选择。【0312A直编】`, saved through the File menu, navigated away/back, and confirmed in `resources/canonical/sessions/final_confrontation.json`.
- Task Objective type/target/count controls were present inside the unselected node. Selecting DGR item `evidence_token` and saving produced target `evidence_token`, required count `3`, and no `minecraft:*` value.
- Objective parameter expander heights were `319 -> 118 -> 319` pixels for expanded, collapsed, restored.
- Story Start inline trigger type, region fields, remove button, and add-trigger button were visible and interactive.
- Action inline editing was not exercised in this live fixture, so the complete A5 Gate remains blocked.

### A6 theme and popup

- Objective target popup was opened in Dark and Light themes. Placeholder and `证物令牌` remained readable in both; the Dark capture also shows the unresolved legacy `minecraft:paper` entry.
- Start, Action, Line, Choice, and Settlement Inspectors were not all opened in both themes; the complete Gate remains blocked.

### A7 authoring menus

- Story, Session, and Task blank-canvas `添加节点` menus were opened in the Release EXE.
- Visible authoring categories were `流程 | 聚合 | 逻辑 | 动作 | 触发`.
- Story Start, Session 起始, Task 结算, and Task 激活 did not appear, including as disabled entries.

### A8 scissors cursor

- Default canvas cursor handle: `0x10003`.
- Scissors-mode cursor handle: `0xFFFFFFFFCE8A1637`.
- The handles differed, and the actual custom cursor was drawn from the current Win32 cursor handle into `A8-real-scissors-cursor.png`; the image visibly shows scissors over the graph.
- The separate Alt-temporary and graph-leave lifecycle was not captured end to end, so the full A8 Gate remains blocked.

### A9 wire drag

- First live pass exposed a white new-wire visual even though automation passed. That was treated as a real FAIL and fixed by using the normal formal `GraphConnectionVisualStyle` for new uncommitted wires.
- The republished EXE shows the drag wire in the same blue Flow color and thickness as persisted Flow wires.
- Releasing over empty canvas removed the transient visual. Story JSON SHA-256 remained `E5E9223379BE4A62C5C2F5E0FCDC0D5EA6AA98691DD2B66482087CDC5105F4DE` before and after cancellation.
- Existing single-wire reconnect and multi-incident reconnect were not recorded live; complete A9 remains blocked.

### A11 folder collapse/expand

- Item folder was collapsed and expanded ten consecutive times.
- Session folder Y was always `417` collapsed and `489` expanded.
- Maximum UIAutomation toggle call was `4.571 ms`; final state was expanded.
- There was no delayed second movement or end-of-animation jump.

### A13 DGR objective identity

- A real DGR item `evidence_token` / `证物令牌` was created through the UI.
- The Task Objective target popup selected that item and saved `item_target=evidence_token`, `required=3`; persisted JSON contained no new `minecraft:*` target.
- The complete empty-project actor group/item group/three-objective sequence was not run, so A13 remains blocked.

### A14 Start trigger replacement

- Added a second trigger, changed it to `角色交互`, and selected actor `侦探`.
- Removing connected `进入区域` initially exposed a missing WPF confirmation callback; this was fixed and covered by a regression test.
- Republished EXE displayed `确认删除启动方式`; confirming removed the trigger and its connection.
- Saved JSON retained exactly one trigger: `trigger_type=interact_actor`, `actor_id=detective`, with one output port and no connection from Start.

## Diagnostic evidence

`evidence/live/diagnostics/` contains intermediate failures that were not counted as passing evidence: missing folder headers before the Expander template fix, the pre-fix Start removal path, and a Light-theme diagnostic capture with graph validation messages.
