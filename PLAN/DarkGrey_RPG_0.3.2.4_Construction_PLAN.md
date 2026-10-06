# DarkGrey RPG 0.3.2.4 Construction Plan

**Version:** `0.3.2.4`  
**Repository:** `GreyHat633/DarkGrey_RPG`  
**Baseline:** `codex/0.3.2.3` @ `879fcfc60b6b4876ea1b673541186f63dc8f2792`  
**Target branch:** `codex/0.3.2.4`

---

# 0. Version Positioning

0.3.2.4 is a focused **Creator/UI finishing release**.

0.3.2.3 already established the identity lifecycle and must remain the baseline:

- NPCID transfer
- ItemID transfer
- `ID释放`
- `实体解绑`
- `物品解绑`
- Item Group `EXACT / FUZZY`
- batch-safe Item Nominator
- correlated/revision-validated Nominator operations
- single gray DGR palette

0.3.2.4 must not redesign these systems. Its scope is:

1. finish Entity Nominator layout/closing;
2. finish Item Nominator lower layout and add real armor slots;
3. add smooth/inertial Nominator scrolling;
4. clean Task GUI closing UX;
5. allow a narrow Studio file-dialog maintenance exception;
6. correct the actual test-story Objective description without changing Task Runtime;
7. add persistent draggable/resizable utility windows.

---

# 1. Hard Boundaries

## 1.1 Canonical runtime remains frozen

Do not redesign:

- Canonical Story Runtime
- Canonical Session Runtime
- Canonical Task Runtime
- Task push/cache synchronization
- Dialogue Session persistence
- Dialogue Esc/death/reconnect behavior
- Actor arbitration

## 1.2 0.3.2.3 identity behavior remains frozen

Preserve:

- server authority
- token/sequence correlation
- revision validation
- atomic NPCID/ItemID transfer
- resource-side `ID释放`
- host-side `实体解绑` / `物品解绑`
- GroupID multi-member semantics
- Item Group EXACT/FUZZY semantics
- Item Nominator keep-open batching

## 1.3 Studio maintenance exception

Studio remains frozen except:

```text
STUDIO_MAINTENANCE_EXCEPTION = FILE_DIALOG_UX_ONLY
```

Allowed Studio changes:

- user-visible `.dgrs` suggested filename
- remembered Export directory
- remembered Import directory
- remembered Reference directory
- settings fields/tests required for those paths

Still prohibited:

- node/schema changes
- Inspector changes
- DGRS archive/schema changes
- Namespace/Origin semantic changes
- Story ownership/reference semantic changes
- general Studio redesign

---

# 2. M0 — Baseline / Freeze Capture

Create `codex/0.3.2.4` from:

`879fcfc60b6b4876ea1b673541186f63dc8f2792`

Update Java mod version to `0.3.2.4`.

Before production changes, run and record 0.3.2.3 regressions.

Required baseline:

```text
JAVA_BUILD=PASS
0.3.2.3_NOMINATOR_PROBES=PASS
TASK_PUSH_CACHE=PASS
DIALOGUE_LAYERING=PASS
NPCID_TRANSFER=PASS
NPCID_RELEASE=PASS
ITEMID_TRANSFER=PASS
ITEM_GROUP_EXACT_FUZZY=PASS
ENTITY_UNBIND=PASS
ITEM_UNBIND=PASS
ITEM_BATCH_KEEP_OPEN=PASS
```

---

# 3. M1 — Entity Nominator Final Layout

Target layout:

```text
┌──────────────────────────────────────────────┐
│                 实体指名器                   │
│ [搜索......................................] │
├───────────────┬──────────────────────────────┤
│ 故事包        │ 资源                         │
│               │                              │
│               │                              │
├───────────────┴──────────────────────────────┤
│ 当前实体：GreyHat_:TarvenBoss    [ID释放]   │
│                                              │
│ [实体指名]                      [实体解绑]   │
└──────────────────────────────────────────────┘
```

Final rules:

- remove `关闭`
- bottom-left = `实体指名`
- bottom-right = `实体解绑`
- move `当前实体` to the information strip
- move `ID释放` to the resource-side position
- keep resource browser/identity semantics unchanged

## Close behavior

Entity Nominator closes with:

- Esc
- player's configured inventory key

Use `mc.gameSettings.keyBindInventory`, never hardcode `E`.

Search-focus rule:

```text
modal open -> modal owns input
search focused -> text input wins
search not focused + inventory key -> close
```

Do not make the inventory-bound printable character impossible to type into search.

---

# 4. M2 — Item Nominator Final Geometry

Keep all 0.3.2.3 behavior; change layout only.

Approved hierarchy:

```text
LEFT              CENTER                     RIGHT
item operations   inventory + hotbar         armor
```

Resource-side `ID释放` stays at upper-right near the resource list.

Target:

```text
┌──────────────────────────────────────────────────────────┐
│                    物品指名器                            │
│ [搜索.................................................] │
├───────────────────┬──────────────────────────────────────┤
│ 故事包            │ 资源                     [ID释放]   │
├───────────────────┴──────────────────────────────────────┤
│                                                          │
│  物品指名           玩家背包                    装备栏   │
│     □            □ □ □ □ □ □ □ □ □              □     │
│ [物品指名]       □ □ □ □ □ □ □ □ □              □     │
│                  □ □ □ □ □ □ □ □ □              □     │
│                                                   □     │
│  物品解绑        □ □ □ □ □ □ □ □ □              □     │
│     □                                             □     │
│ [物品解绑]                                               │
└──────────────────────────────────────────────────────────┘
```

## 4.1 Operation groups

Left side contains two centered units:

```text
物品指名
  slot
[物品指名]
```

and:

```text
物品解绑
  slot
[物品解绑]
```

The actual bind button must be named `物品指名`, not generic `指名`.

## 4.2 Inventory alignment

The 3×9 inventory and 9-slot hotbar must share the same x-origin.

`玩家背包` title must be centered over that same block.

No independent ad-hoc horizontal offsets.

## 4.3 Real armor slots

Add the player's actual four armor slots to the Container.

Do not draw fake slots.

They must:

- use real armor inventory indices
- retain normal armor-slot validity behavior
- preserve item safety
- appear as a vertical column right of the inventory

## 4.4 Slot-index safety

Keep named constants for:

- `NOMINATE_SLOT`
- `UNBIND_SLOT`
- player main inventory range
- hotbar range
- armor range

Update shift-click logic deliberately.

No item duplication/loss is acceptable.

---

# 5. M3 — Smooth / Inertial Nominator Scrolling

Scope:

- Entity Nominator
- Item Nominator

Current integer row jumping should be replaced by short visible interpolation.

Recommended state:

```text
packageScrollTarget
packageScrollPosition
resourceScrollTarget
resourceScrollPosition
velocity/interpolation state
```

Behavior:

- wheel changes target
- visual position eases toward target
- short inertia
- fast settle
- no overscroll
- no bounce
- no long mobile-style glide

Critical requirements:

- hover follows rendered row position
- click selects the visually rendered row
- scrolling cannot draw into search/title/bottom controls
- preserve query/package/resource selection and scroll across server refreshes

Use clipping/scissor for the list viewport.

---

# 6. M4 — Task GUI Minor Cleanup

Task GUI:

- Esc closes
- configured inventory key closes
- remove visible `Esc 返回`

Do not touch:

- `CanonicalTaskClientStore`
- Task push snapshots
- Task Runtime
- Objective projection logic

---

# 7. M5 — Studio File-Dialog Maintenance

## 7.1 Human-readable export filename

For user-visible export filenames only:

```text
':' -> '.'
```

Examples:

```text
GreyHat_:Firest
→ GreyHat_.Firest.dgrs

MyPack:chapter_01
→ MyPack.chapter_01.dgrs

legacy_story
→ legacy_story.dgrs
```

Do not change:

- StoryID
- Namespace
- internal hex resource paths
- archive structure

Use a dedicated display filename helper if needed.

## 7.2 Remember file-dialog directories

Persist separately:

```text
last_export_directory
last_import_directory
last_reference_directory
```

Rules:

- after successful selection/export, remember parent directory
- next corresponding dialog starts there
- Export/Import/Reference do not share one path
- if saved path no longer exists, fall back safely

## 7.3 Settings compatibility

Adding these settings must preserve old settings.

Missing new fields must not reset:

- theme
- namespace
- recent projects
- window state
- browser widths
- bottom-panel height

---

# 8. M6 — Actual Test Objective Description Correction

Do **not** patch Task Runtime.

The observed transition pattern is consistent with:

```text
Objective 1:
kill_entity
description = 消灭史莱姆
required = 3

Objective 2:
interact_actor
description = 消灭史莱姆   <- authored text error
required = 1
```

Correct the authoritative test content to:

`与酒馆老板对话`

Preserve:

- node IDs
- objective type
- target NPCID
- prerequisite edge
- settlement edge
- runtime semantics

Acceptance must prove separately:

```text
RUNTIME_TRANSITION=PASS
AUTHOR_DESCRIPTION_FIXED=PASS
```

If the exact editable source project is unavailable:

- do not modify Runtime to compensate
- do not edit unrelated Studio projects
- identify the exact tested `.dgrs`
- use an isolated corrected acceptance package if necessary
- record that source authoring still needs the same description correction when recovered

---

# 9. M7 — Draggable / Resizable Utility Windows

Supported:

- Entity Nominator
- Item Nominator
- Copier
- Task GUI

Excluded:

- Dialogue
- Choice
- death screen
- Vanilla GUIs

Each supported window gets:

- draggable title bar
- resize handle at bottom-right
- minimum width/height
- maximum constrained to current scaled screen
- clamped position so it cannot become unrecoverable

## 9.1 Drag

During title-bar drag, only update position.

No:

- network call
- catalog reload
- server request
- identity mutation

## 9.2 Resize

Resize changes layout space:

- window width/height
- list viewport
- visible row count
- button coordinates
- scroll bounds
- Item Nominator slot coordinates

Resize must **not** scale pixels.

Do not scale:

- fonts
- 16×16 item icons
- slot pixels
- button pixel height

## 9.3 Performance rule

Drag/resize is pure client presentation.

Forbidden per mouse-move frame:

- network requests
- disk writes
- package reload
- catalog reload
- repeated business-state reconstruction
- framebuffer whole-window scaling
- unnecessary full `initGui()` churn

A sustained visible FPS drop while dragging/resizing is a bug.

Use lightweight geometry methods such as:

```text
updateWindowGeometry()
layoutControls()
layoutSlots()
```

---

# 10. M8 — Persistent Window Layout

The purpose is user-controlled layout across different resolutions, so settings must survive Minecraft restarts.

Persist separately for:

- Entity Nominator
- Item Nominator
- Copier
- Task

Each saves:

```text
centerXRatio
centerYRatio
width
height
```

Position uses normalized screen ratios.

Size uses Minecraft `ScaledResolution` logical pixels.

On open:

1. load saved state
2. convert center ratios to current resolution
3. clamp size to per-window minimum and screen maximum
4. clamp position so title bar/resize handle remain reachable

Do not save raw absolute X/Y only.

Do not write to disk every mouse-move.

Save:

- when drag ends
- when resize ends
- and/or when GUI closes

This is client-local only.

Do not send layout settings to the server or mix them with Story/Task/NPC persistence.

---

# 11. M9 — Dynamic Layout by GUI

## Entity Nominator

Resize affects:

- Browser width/height
- visible row count
- resource text width

Bottom controls remain anchored.

## Item Nominator

Anchors are fixed conceptually:

```text
left   -> 物品指名 / 物品解绑
center -> inventory / hotbar
right  -> armor
top-right resource side -> ID释放
```

Enforce a minimum width large enough for the fixed 9-slot inventory grid.

No overlap is allowed.

## Copier

Resize affects:

- list viewport
- visible template count
- text/button width

Do not change template order or selected server index.

## Task

Refactor `CanonicalTaskLayout` to consume a user window rectangle rather than always constructing a centered fixed panel.

Resize affects:

- task-list viewport
- detail viewport
- stacked/two-column threshold

Task data/cache remains untouched.

---

# 12. M10 — Input Arbitration

Priority:

```text
1. modal
2. GuiContainer slot interaction
3. resize handle
4. title-bar drag
5. buttons/list selection
6. scrolling
```

Rules:

- modal blocks underlying drag/resize
- button click must not start drag
- title-bar drag must not click list rows
- resize handle must not activate buttons
- Item Nominator slot behavior remains Vanilla-safe
- inventory-key close must respect focused text input

---

# 13. Automated Probe Requirements

At minimum add coverage for:

## Entity Nominator

- no Close button
- `实体指名` bottom-left
- `实体解绑` bottom-right
- `ID释放` resource-side
- current entity info visible
- remapped inventory key closes
- focused search does not lose printable inventory-key input

## Item Nominator

Verify slot topology:

- 2 special slots
- 27 main inventory
- 9 hotbar
- 4 real armor slots

Verify:

- no duplicate indices
- no item loss/duplication
- main inventory/hotbar same x-origin
- operation units left
- armor right
- `ID释放` resource-side

## Smooth scrolling

Verify:

- target changes on wheel
- visual position converges
- boundaries clamp
- no overshoot
- click mapping matches visual offset
- state survives Nominator refresh

## Task

- remapped inventory key closes
- no `Esc 返回`
- push-cache behavior unchanged

## Studio

Verify filename mapping:

```text
GreyHat_:Firest -> GreyHat_.Firest.dgrs
MyPack:chapter_01 -> MyPack.chapter_01.dgrs
legacy_story -> legacy_story.dgrs
```

Verify independent remembered:

- Export
- Import
- Reference

Verify old settings still load.

## Objective content

Verify actual test flow:

```text
kill objective ACTIVE
→ COMPLETED
→ interact_actor ACTIVE
```

and UI text becomes:

`与酒馆老板对话`

## Window geometry/persistence

For all 4 supported windows:

- drag math
- resize minimum
- screen clamp
- save/reopen
- restart/reload settings
- resolution change
- ratio-position restoration
- modal blocks drag/resize

---

# 14. Real-Machine Acceptance Gates

## Gate A — Entity Nominator

Verify:

- no Close button
- left-bottom `实体指名`
- right-bottom `实体解绑`
- `当前实体` information strip
- resource-side `ID释放`
- Esc close
- remapped inventory-key close
- search-focus key behavior

## Gate B — Item Nominator

Verify visually:

- left `物品指名`
- left `物品解绑`
- both units centered internally
- main inventory exactly aligned over hotbar
- real armor column right
- resource-side upper-right `ID释放`

## Gate C — Armor integrity

Test valid armor placement/removal and close behavior.

No duplication/loss.

## Gate D — Smooth scrolling

Use enough packages/resources to scroll.

Verify:

- visible easing/inertia
- quick settle
- correct click during animation
- no clipping leak
- no jump-to-top after Nominator server result

## Gate E — Task close

Verify:

- Esc closes
- remapped inventory key closes
- no footer hint
- live push updates still work

## Gate F — Studio filename

For `GreyHat_:Firest`:

Save dialog default must be:

`GreyHat_.Firest.dgrs`

Exported package still contains authoritative StoryID:

`GreyHat_:Firest`

## Gate G — Studio path memory

Verify separately:

- Export remembers Export
- Import remembers Import
- Reference remembers Reference
- restart preserves all three

## Gate H — Objective description

Using the user's actual current test story:

```text
消灭史莱姆
→ kill 3
→ 与酒馆老板对话
```

Record Runtime status and Task UI before/after.

## Gate I — Drag

For:

- Entity Nominator
- Item Nominator
- Copier
- Task

Verify smooth title-bar dragging, no accidental actions, no sustained obvious FPS drop.

## Gate J — Resize

For all supported GUIs:

- shrink to minimum
- enlarge
- content reflows
- no pixel scaling
- no lost/overlapping controls

Item Nominator receives extra scrutiny.

## Gate K — Persistent custom layout

Arrange all four windows differently, restart Minecraft, reopen them, and verify each restores its own position/size.

Then change resolution/GUI scale and verify safe ratio-based/clamped recovery.

---

# 15. Non-Regression Gates

Must continue to pass:

## 0.3.2.3 identity

- NPCID transfer
- left `转移`, right `取消`
- orphan NPCID release
- Entity GroupID release
- `实体解绑`
- ItemID transfer
- Item Group release
- Group EXACT/FUZZY popup
- EXACT = registryName + damage/metadata + NBT
- FUZZY = registryName only
- `物品解绑`
- batch keep-open
- revision-safe repeated actions

## 0.3.2.2 Task

- server push
- read-only client cache
- no request-on-open loading
- no normal polling
- live Objective updates

## 0.3.2.2 Dialogue

- click-anywhere LINE/NARRATION
- server-authoritative Choice
- Esc opens Vanilla pause
- higher GUI overlays Dialogue
- Dialogue remains underlay
- death/respawn safe
- reconnect restores active Session

## UI

Do not reintroduce a gold/brown DGR UI family.

---

# 16. Construction Order

```text
M0  Baseline / regression capture
↓
M1  Entity Nominator fixed layout / close behavior
↓
M2  Item Nominator fixed layout / armor slots
↓
M3  Smooth Nominator scrolling
↓
M4  Task close cleanup
↓
M5  Studio file-dialog maintenance
↓
M6  Actual test Objective description correction
↓
M7  Shared drag/resize mechanics
↓
M8  Persistent per-window layout settings
↓
M9  Per-GUI dynamic layout integration
↓
M10 Input arbitration hardening
↓
M11 Automated regression
↓
M12 Real-machine acceptance
↓
USER ACCEPTANCE
```

Do not begin dynamic Item Nominator resize until the fixed armor/inventory/special-slot layout is proven correct.

---

# 17. Completion Criteria

0.3.2.4 may be marked technically complete only when:

```text
JAVA_BUILD=PASS
STUDIO_BUILD=PASS_FOR_FILE_DIALOG_EXCEPTION

NEW_0.3.2.4_PROBES=PASS
0.3.2.3_REGRESSION=PASS

ENTITY_NOMINATOR_LAYOUT=PASS
ENTITY_INVENTORY_KEY_CLOSE=PASS

ITEM_NOMINATOR_LAYOUT=PASS
ITEM_ARMOR_SLOTS=PASS
ITEM_SLOT_SAFETY=PASS

NOMINATOR_SMOOTH_SCROLL=PASS

TASK_INVENTORY_KEY_CLOSE=PASS
TASK_ESC_HINT_REMOVED=PASS

STUDIO_HUMAN_READABLE_FILENAME=PASS
STUDIO_EXPORT_DIR_MEMORY=PASS
STUDIO_IMPORT_DIR_MEMORY=PASS
STUDIO_REFERENCE_DIR_MEMORY=PASS

TASK_RUNTIME_TRANSITION=PASS
TEST_OBJECTIVE_DESCRIPTION=PASS

GUI_DRAG=PASS
GUI_RESIZE=PASS
GUI_LAYOUT_PERSISTENCE=PASS
GUI_RESOLUTION_RECOVERY=PASS
GUI_DRAG_RESIZE_PERFORMANCE=PASS

NPCID_TRANSFER_REGRESSION=PASS
NPCID_RELEASE_REGRESSION=PASS
ITEMID_TRANSFER_REGRESSION=PASS
ITEM_GROUP_EXACT_FUZZY_REGRESSION=PASS
ENTITY_UNBIND_REGRESSION=PASS
ITEM_UNBIND_REGRESSION=PASS
ITEM_BATCH_KEEP_OPEN_REGRESSION=PASS

FROZEN_CANONICAL_RUNTIME_DIFF=0
FROZEN_DGRS_SCHEMA_DIFF=0
STUDIO_DIFF_OUTSIDE_FILE_DIALOG_EXCEPTION=0
```

Then status may become:

`AGENT_REAL_MACHINE_VERIFIED`

It must remain:

`0.3.2.4_USER_ACCEPTED=NO`

until the user personally audits and accepts the build.

---

# 18. Final Frozen Design

```text
Entity Nominator
├─ resource-side ID释放
├─ bottom-left 实体指名
├─ bottom-right 实体解绑
├─ Esc / inventory-key close
├─ smooth scrolling
├─ drag / resize
└─ persistent layout

Item Nominator
├─ resource-side ID释放
├─ left 物品指名
├─ left 物品解绑
├─ centered inventory + hotbar
├─ right real armor column
├─ smooth scrolling
├─ drag / resize
├─ persistent layout
└─ 0.3.2.3 identity semantics unchanged

Task
├─ Esc / inventory-key close
├─ remove Esc footer
├─ drag / resize
├─ persistent layout
└─ push-cache unchanged

Copier
├─ drag / resize
└─ persistent layout

Studio
├─ GreyHat_:Firest -> GreyHat_.Firest.dgrs
├─ remember Export directory
├─ remember Import directory
├─ remember Reference directory
└─ no DGRS/schema/runtime semantic changes
```

Core rule:

> **0.3.2.4 only finishes usability and client customization. It must not reopen the already-stabilized RPG runtime or identity architecture.**
