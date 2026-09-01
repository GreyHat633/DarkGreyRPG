# DarkGrey_RPG 0.3.1.5 Studio Finalization & Regression Construction PLAN

**Project:** DarkGrey_RPG / DGR
**Target Version:** `0.3.1.5`
**Target Branch:** `codex/0.3.1.5`
**Construction Baseline:** `codex/0.3.1.4`
**Primary Input:** `0.3.1.4审计.docx` + user-approved Choice / Flow Judgment design discussion
**Purpose:** Finish the remaining Studio usability defects without regressing any accepted/fixed behavior from `0.3.1.0` through `0.3.1.4`.

---

# 0. Executive Rule

`0.3.1.5` is **not** a new architecture release.

It is a **focused Studio finalization + regression-lock release**.

The release must do exactly two things:

1. Repair the five current improvement areas defined in this PLAN.
2. Prove that previously repaired `0.3.1.x` behavior has not regressed.

A build that fixes the five new items but reintroduces an older accepted defect is **FAIL / NO-GO**.

Codex may deliver:

```text
IMPLEMENTED
AGENT_VERIFIED
Release Candidate
```

Codex must **not** self-declare:

```text
USER_ACCEPTED
Studio 已冻结
0.3.1.x 已完成
```

Only the user may grant `USER_ACCEPTED` and only the user may explicitly say:

```text
冻结 Studio
```

Until that happens, the Studio remains unfrozen.

---

# 1. Authoritative Inputs

Before coding, Codex MUST read the following in full.

## 1.1 Current audit

```text
0.3.1.4审计.docx
```

The current audit contains four direct defects:

1. Closing Studio with unsaved work does not provide the desired save-before-close confirmation behavior.
2. Actor / Item Inspector still has poor information design and exposes unnecessary headings.
3. Graph wires can visually originate from text instead of the visible port center.
4. Action node inline/Inspector field labels and action names need cleanup.

## 1.2 Additional approved design work

In addition to the four audit items, `0.3.1.5` must implement one combined graph-design work package:

```text
Choice UI simplification
+
new 【流程判断】 node
```

This fifth work package is not optional.

The accepted direction is:

- `【选择】` should no longer expose both Flow and Logic outputs for every option.
- Choice should remain primarily a Flow branching node.
- A new `【流程判断】` node supplies a clean way to expose whether a Flow branch has been executed to the Logic network.

## 1.3 Historical regression baseline

Codex MUST also reread the relevant historical repair PLANs / audits available in the repository / project files, especially:

```text
0.3.1.1
0.3.1.2A
0.3.1.2B
0.3.1.3
0.3.1.4
```

Do not inherit historical `PASS` labels blindly.

Convert still-valid requirements into regression checks.

---

# 2. Source Baseline Rule

Before changing code:

```bash
git checkout codex/0.3.1.4
git pull
git rev-parse HEAD
git status
```

Record the actual baseline HEAD in the implementation report.

Do not assume an older Development Report commit is still current.

The latest source review before this PLAN showed:

- `MainWindow.Closing` already routes into `ShellViewModel.TryClose()`;
- `ShellViewModel` already owns `HasUnsavedDocuments()` and Save-All logic;
- `FlowPortControl.GetAnchorPoint()` falls back to the center of the whole port control when the visible anchor is not measurable;
- the authoring menu currently performs an additional category-local sort around `condition`;
- `GraphWireGestureKind` already separates:
  - `NewConnection`
  - `ReconnectSingleEndpoint`
  - `AddOnMultiPort`
  - `ReconnectMultiBundle`

Therefore `0.3.1.5` must **repair the existing architecture**, not replace the graph editor.

---

# 3. Scope Lock

## IN SCOPE

Only the following five Work Packages:

```text
WP-A  Unsaved-close protection
WP-B  Actor / Item Inspector simplification
WP-C  Wire endpoint visual-anchor correctness
WP-D  Action node naming + field-label cleanup
WP-E  Choice simplification + Flow Judgment node
```

Plus mandatory regression verification.

## OUT OF SCOPE

Do NOT introduce:

- a graph editor rewrite;
- a new persistence database;
- a generic variable system;
- a generic event bus;
- a new typed-data port family;
- a third general interface kind;
- autosave infrastructure;
- a generic node-layout framework rewrite;
- a generic property-editor framework;
- a plugin API;
- a new Runtime scripting language;
- a generic boolean-variable node family;
- a generalized state-machine engine;
- a second Choice state store;
- a new project format solely for these fixes.

This version is a repair/finalization release.

---

# 4. Severity / Priority

| Priority | Work |
|---|---|
| P1 | WP-C Wire must originate exactly from visible port center |
| P1 | WP-E Choice + Flow Judgment graph semantics and UI |
| P2 | WP-A Unsaved close confirmation |
| P2 | WP-B Actor / Item Inspector cleanup |
| P2 | WP-D Action node naming / field labels |
| Release Blocker | Any historical regression |

Although most remaining items are smaller than the earlier P0 defects, a regression in saved author work, graph interaction, crash stability, or canonical semantics is still an immediate Release Blocker.

---

# 5. WP-A — Unsaved Close Protection

## 5.1 User contract

When the user closes Studio and **any current project authoring data is unsaved**, Studio must ask whether the user wants to save before closing.

Expected three-way decision:

```text
保存
不保存
取消
```

Semantics:

### 保存

- Flush the active UI draft first.
- Save **all dirty authoring documents in the current project that Studio currently owns**.
- Only close after all required saves succeed.
- If any save fails, stay open.
- Surface the error through the existing normal error / Problems infrastructure.

### 不保存

- Close without writing the outstanding dirty changes.
- Do not silently save.
- Do not show additional per-resource save prompts afterward.

### 取消

- Abort application close.
- Preserve all current in-memory authoring state.

## 5.2 Important boundary

The close prompt must be a **project/workspace-level decision**.

Do NOT show:

```text
Save Story?
Save Session A?
Save Session B?
Save Task?
Save Actor?
...
```

one after another.

The user requested one understandable close boundary, not a prompt cascade.

## 5.3 Existing source issue to inspect

Current `MainWindow_OnClosing` calls:

```text
_shell.TryClose()
```

Current `TryClose()` still resolves several editors/routes individually and may return early.

Meanwhile the code already has:

```text
HasUnsavedDocuments()
SaveAll()
TrySaveAllCanonicalResources(...)
```

The preferred repair is therefore to unify the application-close path around a **single complete dirty check + one close decision + save-all operation**.

Do not create a parallel dirty tracking framework.

## 5.4 Draft flushing

Before checking/saving:

- flush the currently focused TextBox draft;
- commit the active inline editor using the existing 0.3.1.4 Draft/Commit boundary;
- do not reintroduce per-keystroke canonical mutation.

`Ctrl+S` behavior fixed previously must remain intact.

## 5.5 DoD

PASS only if all are true:

1. Clean project → close immediately, no unnecessary prompt.
2. Dirty Story Flow → one prompt.
3. Dirty Session → one prompt.
4. Dirty Task → one prompt.
5. Dirty Actor / Item where applicable → one prompt.
6. Multiple dirty resources → still one prompt.
7. `保存` → all dirty work survives restart.
8. `不保存` → changes are discarded as requested.
9. `取消` → Studio remains open with all edits intact.
10. Save failure → Studio remains open.
11. No new global “save before normal editing” gate is introduced.

---

# 6. WP-B — Actor / Item Inspector Final Simplification

## 6.1 Product goal

The selected resource already has a visible resource type / display name in the surrounding workspace.

Therefore the Inspector must stop repeating meaningless hierarchy.

Remove author-facing UI elements:

```text
资源属性
显示名称
拥有/引用状态
```

Do not replace them with new technical headings.

## 6.2 Actor presentation

### Individual Actor

Show only relevant identity + tags:

```text
NPC_ID: tavern_boss
标签：酒馆老板、任务发布者、北方城镇
```

### Actor Group

```text
Group_ID: slimes
标签：敌对、史莱姆
```

## 6.3 Item presentation

### Individual Item

```text
Item_ID: copper_coin
标签：货币、普通
```

### Item Group

```text
Group_ID: swords
标签：武器、剑类
```

## 6.4 Layout

Identity name and value MUST be on the same visual line:

```text
Group_ID: test_group_npc
```

Not:

```text
Group_ID
test_group_npc
```

Tags begin on the same line:

```text
标签：这个用于角色测试的标签页……
```

If the value wraps, wrapped lines must align with the beginning of the **tag value**, not return to the far-left edge.

Conceptually:

```text
标签：这是一段比较长的标签内容，第一行空间不足时
      后续内容从正文起点继续排列
```

Exact pixel indentation may use WPF layout rather than literal spaces.

## 6.5 Theme / accessibility

Verify both Dark and Light:

- identity label readable;
- identity value readable;
- tag wrapping readable;
- no clipped text;
- no unnecessary border/card added merely to compensate for the removed headings.

## 6.6 DoD

PASS only if:

- “资源属性” absent;
- “显示名称” absent;
- “拥有/引用状态” absent;
- identity key/value single row;
- tags use the required wrapped hanging-indent behavior;
- Actor, Actor Group, Item, Item Group all show the correct identity terminology;
- no legacy `Actor ID`, `角色 ID`, generic `ID` leakage.

---

# 7. WP-C — Wire Endpoint Must Equal Visible Port Center

## 7.1 Locked invariant

Every graph wire endpoint must originate from / terminate at the geometric center of the **visible port anchor**:

```text
● Flow
◆ Logic
```

Never from:

- the label center;
- the complete FlowPortControl center;
- the row center;
- an old cached anchor position;
- a hard-coded text offset.

This applies to:

```text
formal stored wire
new connection preview
single-capacity reconnect
multi-capacity add
Ctrl+multi reconnect
restored graph
freshly rebuilt node
renamed port
dynamic port rebuild
zoom/pan
theme switch
```

## 7.2 Source risk

Current `FlowPortControl.GetAnchorPoint()` has this fallback shape:

```csharp
if (_anchor is null || _anchor.ActualWidth <= 0 || _anchor.ActualHeight <= 0)
    return center-of-entire-FlowPortControl;
```

Because the full control contains the label, this fallback can visually put the wire endpoint inside text.

This fallback must no longer violate the invariant.

## 7.3 Acceptable implementation directions

Prefer minimal correction such as:

- derive fallback anchor center from the known fixed 20 DIP anchor slot;
- or defer/redraw wire geometry after anchor layout is measurable;
- or cache a valid visual-anchor transform only after layout.

Do NOT compensate with magic offsets tied to label length.

## 7.4 Preserve existing port rules

The fix must not regress:

```text
visual anchor ≈ 9–11 px
interaction hitbox ≈ 18–20 px
label is NOT wire-start target
input/output alignment remains correct
valid glow uses the same effective target region as drop
```

## 7.5 Acceptance measurement

For each test port:

- locate visible circle/diamond bounds;
- calculate its center;
- inspect rendered Path start/end;
- allow only normal anti-alias/subpixel tolerance.

A screenshot where the line “looks close enough” is insufficient if the endpoint is visibly offset.

## 7.6 DoD

PASS on both Flow and Logic ports for:

- input;
- output;
- occupied single reconnect;
- multi-connection port;
- dynamic option port;
- Start startup-condition port;
- Choice option port;
- Flow Judgment ports.

---

# 8. WP-D — Action Node Names + Field Labels

## 8.1 Rename author-facing action names

Change author-facing display names:

```text
给予物品 → 物品给予
给予经验 → 经验给予
发送消息 → 消息发送
```

Internal stable types / serialization names should remain unchanged unless a true contract requires otherwise.

Example:

```text
give_item
```

may remain `give_item`.

Do not perform unnecessary migration of stable canonical action type IDs.

## 8.2 Inline / Inspector field labeling

The node body already communicates field meaning more clearly than the current node property surface.

The node property/inline editing surface must explicitly label each field.

### 物品给予

At minimum:

```text
动作类型：物品给予
物品：<Item selector>
数量：<number>
```

### 经验给予

At minimum:

```text
动作类型：经验给予
经验值：<number>
```

Use the existing canonical field meaning; do not invent a second XP concept.

### 消息发送

At minimum:

```text
动作类型：消息发送
消息：<text>
```

## 8.3 Consistency rule

The same author-facing vocabulary must be used in:

- node title;
- inline editor;
- Inspector;
- dropdown;
- validation message where applicable;
- menu entry if action type is shown there.

Do not show:

```text
give_item
give_xp
send_message
```

on normal author surfaces.

## 8.4 Draft behavior regression check

Text fields must retain the 0.3.1.4 Draft/Commit behavior.

Do not reintroduce:

```text
every keystroke
→ canonical mutation
→ full graph refresh
```

---

# 9. WP-E — Choice Simplification + New 【流程判断】 Node

This is the only work package that introduces a new authorable node.

It must remain narrow.

---

# 9.1 Why the current Choice UI changes

A Choice option currently exposing:

```text
● Flow Output
◆ Logic Output
```

on every option is semantically possible but visually poor.

With many options, blue Flow wires and amber Logic wires leave the same rows and inevitably cross.

This creates poor graph readability.

The final design must separate:

```text
Flow branching
```

from:

```text
whether a branch has been executed
```

---

# 9.2 Final Choice visible interface

`【选择】` should expose:

```text
1 × ● Flow Input
N × named ● Flow Outputs
```

One Flow output per option.

It must NOT expose one visible Logic output for every option.

Example:

```text
┌─────────────────────┐
│       【选择】        │
│                     │
● 流程输入             │
│                     │
│ 接受委托          ●  │
│ 拒绝委托          ●  │
│ 再问一些问题      ●  │
└─────────────────────┘
```

## 9.3 Stable option identity

Each option must preserve stable identity.

At minimum, the existing stable contract must continue to distinguish:

```text
option_id
display_text
flow_port_id
```

Renaming an option:

- changes display text;
- does not change stable option identity;
- does not change stable Flow port identity;
- does not break existing Flow wires.

Reordering options:

- changes visual order;
- does not recreate identities;
- does not break wires.

Deleting one option:

- removes only that option and its incident Flow wire(s);
- does not renumber/recreate unrelated stable IDs.

---

# 9.4 New node: 【流程判断】

## Purpose

`【流程判断】` answers one narrow authoring question:

> Has Flow actually executed through this node in the current runtime instance?

It provides a clean Logic result without forcing every Choice option to carry a Logic output.

## Visible interface

```text
┌─────────────────────┐
│     【流程判断】      │
│                     │
● 流程输入          ●  │ 流程输出
│                     │
│                 ◆   │ 执行状态
└─────────────────────┘
```

Required ports:

```text
1 × ● Flow Input
1 × ● Flow Output
1 × ◆ Logic Output: 执行状态
```

No Logic input.

## Runtime semantics

Initial state for the current runtime instance:

```text
执行状态 = false
```

When Flow successfully executes through this node:

```text
执行状态 = true
```

The Flow continues through its Flow Output normally.

Once true, the state remains true for the lifetime/scope defined by the containing graph runtime instance.

Do NOT add in 0.3.1.5:

- Reset input;
- reset command;
- execution count;
- timestamp;
- arbitrary variable name;
- generic state storage;
- pulse/event mode;
- multiple Logic outputs.

If future use cases require these, they are future design work.

## Scope

The primary intended use is the **Session graph**, especially after Choice outputs:

```text
【选择】
  接受委托 ●
       │
       ▼
【流程判断】
  ●──────● → next dialogue / action
       ◆ 执行状态
       │
       └────────→ logic network
```

Before implementation Codex must verify the canonical graph scope rules and place this node only in scopes consistent with the current Session authoring model.

Do not silently enable it in Story Flow / Task just because the registry API makes that easy.

If existing project design sources explicitly require broader scope, document that evidence before enabling it.

---

# 9.5 【流程判断】 authoring-menu placement — HARD UI CONTRACT

This requirement is exact.

In the blank-canvas context menu:

```text
添加节点
└─ 逻辑
   ├─ 条件判断
   ├─ 流程判断
   ├─ ...
```

`【流程判断】` must appear:

1. under `添加节点`;
2. inside category `逻辑`;
3. immediately below `条件判断`;
4. with no other menu item between them.

This is a Release DoD, not a preference.

## Source ordering warning

Current authoring menu code says registry order should be preserved, but still performs a local sort involving `condition`.

`0.3.1.5` must make ordering deliberate and deterministic.

Preferred direction:

- define canonical authoring order in the node-definition registry;
- make the menu preserve that order;
- avoid accumulating one-off special sorts.

Do not create a generic menu-ordering framework.

---

# 9.6 Choice + Flow Judgment examples

## Example A — ordinary Choice, no Logic required

```text
【选择】
接受 ●────→【台词 A】
拒绝 ●────→【台词 B】
```

No Logic ports clutter the Choice.

## Example B — later logic needs to know whether player chose Accept

```text
【选择】
接受 ●────→【流程判断】────●→【台词】
                   ◆
                   └────→【与 / 或 / 非 / 条件逻辑】
```

This is the normal intended composition.

## Example C — two branches independently tracked

```text
接受 ●→【流程判断 A】
拒绝 ●→【流程判断 B】
```

Each Flow Judgment owns its own execution state.

Choice does not duplicate that state.

---

# 9.7 Canonical / Runtime implementation boundary

Implement the smallest canonical support necessary for `流程判断`.

The node must have:

- stable node type;
- stable port IDs;
- schema validation;
- Studio authoring definition;
- serialization/deserialization;
- Runtime execution semantics;
- package compatibility where required.

Do not fake the node as Studio-only visual state if it affects runtime logic.

At the same time, do not introduce a general variable subsystem.

---

# 9.8 Compatibility / migration

Existing `0.3.1.4` content may contain Choice option Logic outputs.

Codex MUST inspect the actual saved schema and existing fixtures before choosing migration behavior.

Required rule:

- no silent destructive conversion;
- no arbitrary loss of connected authoring work.

Before coding, classify current persisted Choice Logic outputs as one of:

```text
A. Not persisted / RC-only visual implementation
B. Persisted but no meaningful existing content
C. Persisted and potentially connected in user projects
```

If C:

- provide explicit compatibility/migration handling;
- document exactly what happens to old Logic connections;
- do not silently delete meaningful wires.

If the repository test fixtures establish a canonical migration convention, reuse it.

---

# 10. Historical Regression Gate

This section is mandatory.

A `0.3.1.5` Release Candidate cannot be produced merely because WP-A..E pass.

Revalidate still-valid behavior from earlier repair cycles.

---

# 10.1 Graph interface semantics

Must remain:

```text
● Flow Output: max 1 target
● Flow Input: 0..N sources

◆ Logic Output: 0..N targets
◆ Logic Input: max 1 source
```

Rule:

```text
流程去向唯一，来源可多。
逻辑来源唯一，去向可多。
```

---

# 10.2 Wire gesture regression matrix

Recheck all:

### Multi-capacity

```text
● Flow Input
◆ Logic Output
```

Ordinary drag:

```text
add one new connection
```

Ctrl + drag:

```text
move all existing connections
```

### Occupied single-capacity

```text
● Flow Output
◆ Logic Input
```

Dragging the occupied endpoint must:

- pick up the endpoint actually grabbed;
- keep the opposite endpoint fixed;
- keep the original wire visually present;
- bend the original wire with the mouse;
- not delete the old wire before drag;
- not produce a fake replacement preview;
- not collapse to zero length;
- not create an unexplained line segment on simple click.

### Transaction

Valid target:

```text
atomic reconnect/connect
```

Escape:

```text
restore original
```

Invalid target:

```text
formal data unchanged
```

Blank drop:

```text
explicit disconnect
```

---

# 10.3 Port hit target / glow

Must remain:

```text
visual ≈ 9–11 px
hit target ≈ 18–20 px
```

- label not clickable as wire start;
- padding around anchor clickable;
- glow == actual valid drop area;
- moving out clears glow immediately.

WP-C must not regress these.

---

# 10.4 Draft / Commit

Recheck:

- text can temporarily be empty while editing;
- typing does not mutate canonical graph every character;
- Enter/focus-loss/explicit completion commits appropriately;
- Ctrl+S flushes active draft first;
- no severe typing lag;
- dropdown changes really commit;
- no reentrant crash.

---

# 10.5 Objective stability

Repeat target switching:

```text
Actor
→ Actor Group
→ Actor
→ Actor Group
...
```

at least 100 changes in the Release EXE.

Required:

- zero crash;
- zero unhandled exception;
- no multi-second stall;
- final selection correct.

---

# 10.6 Node layout persistence

Arrange nodes manually.

Save.

Close Studio.

Reopen.

Required:

- node X/Y restored;
- no default re-layout;
- Story Flow / Session / Task all covered.

Node layout remains separate from runtime semantic JSON where current architecture provides the Studio layout persistence boundary.

---

# 10.7 Viewport isolation

Within one Studio session, verify independent:

```text
Story Flow
Session A
Session B
Task A
Task B
```

pan/zoom state restoration.

Do not confuse viewport with node layout.

---

# 10.8 Resource lifecycle asymmetry

Delete Session / Task resource:

- all aggregate placements referencing it removed;
- incident wires removed.

Delete only placement:

- resource remains.

Must not regress.

---

# 10.9 Dirty state

Dirty means:

```text
unsaved to disk
```

It must NOT block normal same-project authoring:

- create resource;
- reference resource;
- switch Story Flow / Session / Task;
- continue editing.

WP-A must not reintroduce a global save-first lock.

---

# 10.10 Palette

Do not show:

- fixed required nodes;
- compatibility-only nodes;
- Session / Task aggregate creation nodes;
- useless blank aggregate category.

Condition remains:

```text
条件判断
```

New Flow Judgment is placed exactly below it.

---

# 10.11 Story Start

Recheck:

- fixed required node;
- no Flow input;
- startup conditions retain stable IDs;
- unique default names;
- clear grouping;
- author-facing term `启动条件`;
- each startup condition owns its Flow output;
- rename does not change stable port ID.

---

# 10.12 Session

Recheck:

- required `【起始】`;
- Flow only;
- no old legacy Logic output;
- Choice option rows remain aligned after WP-E;
- no option/output row inversion;
- Session resource placement remains resource-driven.

---

# 10.13 Task

Recheck:

- pure Logic;
- unique `【结算】`;
- no legacy `【激活】`;
- N named result Logic inputs;
- first true wins.

---

# 10.14 Objective semantics

Recheck:

```text
实体击杀
物品收集
角色交互
```

- kill quantity valid;
- collect quantity valid;
- interaction has NO quantity;
- proper Actor / Actor Group / Item / Item Group targeting contract retained.

---

# 10.15 Actor / Item identity

Recheck exact terms:

```text
NPC_ID
Item_ID
Group_ID
```

No normal UI leakage of legacy Actor ID.

WP-B must simplify presentation without weakening actual identity semantics.

---

# 10.16 Themes

Dark + Light must both preserve:

- node borders;
- panel boundaries;
- selection;
- ports;
- wires;
- labels;
- TextBox / ComboBox;
- Problems;
- new Flow Judgment node;
- simplified Inspector.

---

# 10.17 Error presentation

Normal UI:

- short Chinese;
- actionable;
- no raw `graph.xxx`;
- no internal exception dumps.

Problems:

- may show detailed diagnostics.

---

# 11. Testing Strategy

Automated tests are required where appropriate but are not sufficient for dynamic UI behavior.

---

# 11.1 Core / model tests

Add or update tests for:

- Flow Judgment definition;
- allowed scope;
- port cardinality;
- stable port IDs;
- serialization round trip;
- Runtime false → true execution state;
- Flow continues after judgment;
- no duplicate state;
- Choice option stable IDs;
- old Choice compatibility/migration if applicable.

---

# 11.2 WPF tests

Add targeted tests for:

- FlowPortControl fallback anchor position;
- wire geometry start/end tied to visual anchor;
- Actor / Item Inspector text structure;
- Action labels;
- action display-name mappings;
- Choice visible ports;
- Flow Judgment rendered ports;
- authoring menu category/order;
- close dirty decision coordination if test seams exist.

Do not write brittle tests that merely search raw XAML strings and claim full UX verification.

---

# 11.3 Release EXE manual verification

Use:

```text
dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe
```

or the actual packaged release output produced by this branch.

Dynamic UI evidence must come from the Release EXE.

---

# 12. Release Acceptance Matrix

Every row must be reported independently.

| ID | Test | Evidence | Status |
|---|---|---|---|
| A1 | clean close, no prompt | Release run | |
| A2 | dirty close → Save | Release run + reopen | |
| A3 | dirty close → Don't Save | Release run + reopen | |
| A4 | dirty close → Cancel | Release run | |
| A5 | multiple dirty editors → one prompt | Release run | |
| B1 | Actor Inspector simplified | screenshot | |
| B2 | Actor Group Inspector simplified | screenshot | |
| B3 | Item Inspector simplified | screenshot | |
| B4 | Item Group Inspector simplified | screenshot | |
| B5 | tag hanging-indent wrapping | screenshot | |
| C1 | Flow input wire center | measured frame | |
| C2 | Flow output wire center | measured frame | |
| C3 | Logic input wire center | measured frame | |
| C4 | Logic output wire center | measured frame | |
| C5 | reconnect still center-correct | video/frames | |
| D1 | 物品给予 naming + labels | screenshot + interaction | |
| D2 | 经验给予 naming + labels | screenshot + interaction | |
| D3 | 消息发送 naming + labels | screenshot + interaction | |
| E1 | Choice has Flow-only option outputs | screenshot + source/model test | |
| E2 | Choice rename stable wire | video/frames | |
| E3 | Choice reorder stable wire | video/frames | |
| E4 | Flow Judgment ports correct | screenshot | |
| E5 | Flow Judgment runtime false→true | automated + runtime evidence | |
| E6 | menu path correct | screenshot | |
| E7 | 条件判断 immediately followed by 流程判断 | screenshot | |
| R1 | single reconnect regression | video | |
| R2 | multi-port ordinary drag | video | |
| R3 | Ctrl multi reconnect | video | |
| R4 | port glow clearing | video | |
| R5 | Draft/Commit typing | video | |
| R6 | Objective 100-switch stability | run log/video | |
| R7 | node layout restart persistence | before/after | |
| R8 | per-graph viewport | video | |
| R9 | resource lifecycle asymmetry | run evidence | |
| R10 | Light theme | screenshots | |

Allowed status vocabulary:

```text
NOT_STARTED
ROOT_CAUSE_CONFIRMED
IMPLEMENTED
AGENT_VERIFIED
USER_ACCEPTED
BLOCKED
```

Codex must never mark `USER_ACCEPTED`.

---

# 13. NO-GO Conditions

Do not publish a Release Candidate if any of the following is true:

1. Studio can still lose node layout after restart.
2. Objective selection can crash.
3. Occupied single-capacity reconnect is broken again.
4. Multi-capacity ordinary drag semantics regress.
5. Port glow and actual drop target diverge.
6. Wire visually originates from label/text instead of visible anchor.
7. Close-with-unsaved can silently lose changes without the requested decision.
8. Save-on-close saves only the currently visible resource while other dirty canonical resources are lost.
9. Actor/Item Inspector still shows `资源属性`, `显示名称`, or `拥有/引用状态`.
10. Action names are inconsistent across node/Inspector/dropdown.
11. Choice still exposes a Flow + Logic output pair for each option in normal UI.
12. Flow Judgment lacks correct Runtime semantics.
13. Flow Judgment is not immediately below Condition in `添加节点 > 逻辑`.
14. Flow Judgment introduces an unrequested generic variable/reset/count system.
15. Old Choice connections are silently destroyed during compatibility handling.
16. Light Theme is unusable.
17. A historical accepted behavior regresses.
18. Codex uses unit tests/screenshots alone to declare dynamic interaction PASS.

---

# 14. Work Order

Recommended implementation order:

## Phase 1 — Baseline / reproduction

- read original audit;
- record HEAD;
- build existing 0.3.1.4 Release;
- reproduce all four audit items;
- inspect current Choice persisted representation;
- confirm where Flow Judgment belongs in scope/runtime.

## Phase 2 — WP-C first

Fix wire-anchor geometry before Choice changes, because Choice/Flow Judgment adds ports and would otherwise make geometry debugging ambiguous.

## Phase 3 — WP-E model/runtime

- define Flow Judgment;
- define compatibility behavior for Choice;
- add Core/model tests;
- implement Runtime semantics.

## Phase 4 — WP-E Studio

- simplify Choice visible outputs;
- render Flow Judgment;
- exact menu placement;
- verify stable options/wires.

## Phase 5 — WP-A / WP-B / WP-D

These are comparatively contained UI/workspace fixes.

## Phase 6 — focused automated regression

Run Core + Studio + WPF tests.

## Phase 7 — Release EXE acceptance

Execute every matrix item.

## Phase 8 — original audit reread

Reopen:

```text
0.3.1.4审计.docx
```

Inspect all four original entries again against the Release EXE.

Then separately inspect the approved Choice / Flow Judgment behavior.

## Phase 9 — historical regression gate

Execute the high-risk regression matrix before writing the final report.

---

# 15. Expected Source Hotspots

Codex must inspect actual current source before editing. Likely hotspots include:

```text
studio/src/DarkGreyRPG.Studio/MainWindow.xaml.cs
studio/src/DarkGreyRPG.Studio/ViewModels/ShellViewModel.cs

studio/src/DarkGreyRPG.Studio/Views/FlowPortControl.cs
studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalGraphEditorView.xaml.cs
studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalGraphNodeControl.xaml
studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalGraphNodeControl.xaml.cs

studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalStoryWorkspaceView.xaml
studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalInlineNodeEditorControl.xaml
studio/src/DarkGreyRPG.Studio/ViewModels/Graph/CanonicalNodeInspectorViewModel.cs

Graph node definition registry
Canonical graph validation / serialization
Session graph execution runtime
Story package model/schema if Flow Judgment must be serialized
Relevant Studio/Core/WPF test projects
```

Do not treat this list as permission to rewrite all files.

Change the smallest coherent set.

---

# 16. Commit / Branch Rules

Create:

```text
codex/0.3.1.5
```

from the verified current `codex/0.3.1.4` baseline.

Recommended commit grouping:

```text
fix(studio): correct graph wire anchor geometry
feat(graph): add flow judgment and simplify choice outputs
fix(studio): protect unsaved work on close
fix(studio): simplify actor item inspector
fix(studio): align action names and field labels
test(studio): lock 0.3.1.5 regression contracts
docs: record 0.3.1.5 release candidate evidence
```

Exact grouping may differ if implementation dependencies require it, but avoid one giant opaque commit.

Push the completed branch to GitHub.

---

# 17. Final Development Report Requirements

Final report must include:

## Baseline

```text
branch
baseline HEAD
final HEAD
build configuration
Release EXE path
```

## Per current item

For all five Work Packages:

```text
Observed problem
Root cause
Files changed
Behavior after fix
Automated verification
Release EXE verification
Status
```

## Historical regression

A table of high-risk historical contracts and actual result.

Do not say:

```text
all previous issues should still work
```

without evidence.

## Blocked items

If some dynamic behavior cannot be proven in Codex's environment:

```text
BLOCKED / NEED_USER_VERIFICATION
```

Do not convert lack of evidence into PASS.

---

# 18. Definition of Done

`0.3.1.5` is a valid **Release Candidate** only when:

- all five current Work Packages are implemented;
- all P1/P2 acceptance items pass agent verification or are truthfully marked blocked;
- no P0/P1 historical regression exists;
- automated suites pass;
- Release EXE is packaged and exercised;
- original 0.3.1.4 audit is reread at the end;
- the Choice / Flow Judgment contract is manually checked;
- the menu visibly shows:

```text
添加节点
→ 逻辑
→ 条件判断
→ 流程判断
```

with `流程判断` immediately below `条件判断`;
- final report does not claim user acceptance.

Final project state remains:

```text
0.3.1.5 Release Candidate
Studio NOT FROZEN
Awaiting USER_ACCEPTED
```

until the user explicitly freezes Studio.

---

# 19. Short Codex Instruction

If a compact instruction is needed:

> Implement `0.3.1.5` as a focused Studio finalization release. Read the original `0.3.1.4审计.docx`, use the actual latest `codex/0.3.1.4` HEAD as baseline, fix the four audit items, simplify `【选择】` to Flow-only option outputs, add the minimal `【流程判断】` node (`● in`, `● out`, `◆ 执行状态`) with real canonical/runtime semantics, and place it exactly at `添加节点 > 逻辑`, immediately below `条件判断`. Do not expand this into a generic state system. Re-run the full high-risk `0.3.1.0–0.3.1.4` regression gate in the real Release EXE. Any historical regression is NO-GO. Codex may report Release Candidate but never USER_ACCEPTED or Studio frozen.
