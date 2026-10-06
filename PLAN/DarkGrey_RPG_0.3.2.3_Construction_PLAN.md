# DarkGrey RPG 0.3.2.3 Construction Plan

**Version:** `0.3.2.3`  
**Project:** DarkGrey RPG  
**Repository:** `GreyHat633/DarkGrey_RPG`  
**Baseline branch:** `codex/0.3.2.2`  
**Baseline commit:** `c5ee16069ee938fd684af621de56597993c48c4e`  
**Target branch:** `codex/0.3.2.3`

---

# 0. Version Positioning

0.3.2.3 is a focused **Nominator / Creator UX correction release** built on the completed 0.3.2.2 runtime and presentation baseline.

The release exists for two reasons:

1. The 0.3.2.2 UI audit exposed an inconsistent gold/gray visual system and a poor Item Nominator layout.
2. The deletion of a bound NPC exposed a real NPCID lifecycle gap: a unique logical ID can remain permanently occupied by an entity UUID that no longer exists, while the normal Creator UI has no way to transfer or release that ID.

Primary scope:

- unify DGR GUI chrome into one light-gray visual system;
- refine Entity/Item Nominator resource rows;
- add resource-side `ID释放`;
- clearly separate resource-side release from host-side `实体解绑` / `物品解绑`;
- rename the Entity Nominator primary bind action to `实体指名`;
- expose unique NPCID / ItemID transfer through a small conflict modal;
- restore Item Group `精准匹配 / 模糊匹配` selection through a Group-only popup;
- keep Item Nominator open after operations for batch authoring;
- add a dedicated Item unbind slot;
- preserve 0.3.2.2 Task push-cache and Dialogue layering behavior as frozen regressions.

---

# 1. Hard Freeze Boundary

## 1.1 Studio remains frozen

`STUDIO_FROZEN = YES`

Do not modify:

- Studio WPF UI
- Story / Session / Task node definitions
- authoring schema
- `.dgrs` format
- Namespace / Origin
- package ownership/reference rules
- Studio export semantics

No new authoring field is required for this release.

## 1.2 Canonical runtime remains frozen

Do not replace or redesign:

- Canonical Story Runtime
- Canonical Session Runtime
- Canonical Task Runtime
- Actor arbitration
- Session persistence
- Task server-push/client-cache model
- Dialogue foreground/underlay lifecycle

0.3.2.3 may extend only the identity binding services, Nominator packets/containers, and Creator UI state required by the approved behavior.

## 1.3 Explicitly out of scope

Do not add:

- a general ID management dashboard
- automatic orphan-ID deletion
- periodic scanning that frees IDs because an entity is currently unloaded
- new Story/Task/Session concepts
- Session suspend/cancel
- Task polling regression
- new Studio panels
- a large GUI framework
- a third visual style

---

# 2. M0 — Baseline / Branch / Freeze Guard

Create:

`codex/0.3.2.3`

from:

`c5ee16069ee938fd684af621de56597993c48c4e`

Update Java mod version:

`0.3.2.2 -> 0.3.2.3`

Do not rebuild or version-bump Studio.

Before production changes:

- run the 0.3.2.2 regression/probe suite;
- confirm Task server-push/client-cache still passes;
- confirm Dialogue Esc / death / reconnect / GUI-underlay behavior still passes;
- confirm Entity and Item Nominator global search behavior still passes.

Final freeze gate:

```text
STUDIO_DIFF=0
FROZEN_SCHEMA_DIFF=0
FROZEN_CANONICAL_RUNTIME_DIFF=0
```

---

# 3. M1 — One Unified Light-Gray DGR UI Palette

## Goal

Eliminate the gold/brown UI family introduced during 0.3.2.2.

All normal DGR GUI chrome should use **one light-gray / dark-gray system**.

## 3.1 Shared palette

Introduce a small shared presentation constants class, for example:

`DgrUiPalette`

It may contain only visual constants such as:

- panel background
- sub-panel background
- normal border
- selected border
- hover background
- primary text
- secondary text
- disabled text
- slot border
- modal mask

This is not a GUI framework.

## 3.2 Remove warm/gold presentation tokens

Replace gold/brown accents in:

- `GuiRpgButton`
- Dialogue Choice buttons
- Task UI
- Copier UI
- Item Nominator action block
- Nominator resource IDs
- Entity resource `[NPCID] / [GroupID]` text
- Creator inspection labels where they visually conflict with the unified palette

No normal interactive state should require gold.

Use light gray / white contrast for emphasis instead.

## 3.3 Static regression guard

Add a small source-level probe/check for the reviewed GUI files so the removed warm/gold presentation literals are not casually reintroduced later.

---

# 4. M2 — Nominator Browser Resource Row Cleanup

## 4.1 Normal package browsing

When a Story Package is selected on the left and the search box is empty, the right-side resource row becomes a **single-line row**.

Entity examples:

```text
史莱姆群   [GroupID] GreyHat_:Slimes
酒馆老板   [NPCID]   GreyHat_:TarvenBoss
```

Item examples:

```text
铜币       [ItemID]  GreyHat_:CopperCoin
武器       [GroupID] GreyHat_:Weapons
```

Requirements:

- display name and ID appear on one line;
- ID/type use subdued gray text;
- do not repeat StoryID/package ID on every resource row when the left package already establishes context;
- shrink row height accordingly.

## 4.2 Global search

Global search still spans **all loaded Story Packages**.

Because results may come from different packages, package provenance must remain visible, but without returning to the old three-line resource layout.

Use a compact same-row representation such as:

```text
[测试故事] 酒馆老板 [NPCID] GreyHat_:TarvenBoss
```

Selecting a global result must continue to bind against the correct package scope.

---

# 5. M3 — Operation Semantics and Button Placement

The approved terminology is frozen:

- `ID释放`
- `实体解绑`
- `物品解绑`

## 5.1 `ID释放`

Meaning:

> Clear the current world binding(s) owned by the selected DGR resource ID.

Placement:

- upper half of Nominator;
- close to the resource list;
- visually owned by the resource library.

## 5.2 `实体解绑`

Meaning:

> Remove direct DGR identity bindings from the currently targeted entity.

Placement:

- lower/current-entity operation area.

## 5.3 `物品解绑`

Meaning:

> Remove all applicable DGR ItemID / Item Group bindings associated with the item definition represented by the stack in the unbind slot.

Placement:

- lower/right item-host area beside the unbind slot.

## 5.4 Release never deletes authoring resources

`ID释放` only changes world/runtime binding data.

It must never delete:

- Actor resources
- Item resources
- Item Group resources
- Story Package content
- `.dgrs` entries

---

# 6. M4 — Entity Nominator: Transfer / ID释放 / 实体解绑

## 6.1 NPCID is transferable

Final model:

> `NPCID` is a unique DGR Actor identity.  
> Entity UUID is its current host, not an irreversible owner.

## 6.2 Occupied NPCID transfer modal

If Entity B is targeted and the selected NPCID is already bound to Entity A:

- keep the Entity Nominator open;
- do not silently transfer;
- do not only fail in chat;
- show a small conflict modal.

```text
┌────────────────────────────────────┐
│          NPCID 已被占用            │
│                                    │
│ GreyHat_:TarvenBoss                │
│ 当前已绑定其他实体。               │
│ 是否转移到当前实体？               │
│                                    │
│ [   转移   ]      [   取消   ]     │
└────────────────────────────────────┘
```

**Button order is fixed: left = `转移`, right = `取消`.**

## 6.3 Transfer must be atomic

Server must atomically:

- move NPCID to the new entity UUID;
- update reverse UUID lookup;
- clean stale direct individual metadata from the previous host;
- preserve unrelated Group bindings according to existing semantics;
- update revisions coherently.

Do not implement transfer as two client operations.

## 6.4 NPCID `ID释放`

Must work even if the old host entity no longer exists.

Server must:

- release the NPCID;
- clean stale direct individual metadata for the previous host UUID when present;
- keep the Actor resource intact.

## 6.5 Entity GroupID `ID释放`

For an Entity Group resource:

- remove that GroupID from all direct entity Group bindings in the world save;
- remove the same GroupID from type-group bindings;
- keep the Group resource intact.

Because this can affect many entities, show a confirmation modal:

```text
[ ID释放 ]   [ 取消 ]
```

Left affirmative, right cancel.

## 6.6 `实体解绑`

Acts only on the currently targeted entity.

It clears:

- current unique NPCID binding;
- current direct per-entity GroupID bindings.

It must not globally release those Group resources from other entities.

---

# 7. M5 — Item Nominator Layout Rebuild

## 7.1 Remove the Close button

Delete the Item Nominator `关闭` button.

The player closes the GUI through normal Minecraft GUI behavior such as Esc / inventory-close controls.

## 7.2 Lower layout

Use:

```text
玩家背包                         物品操作
┌──────────────────────────┐   ┌──────────┐
│ □ □ □ □ □ □ □ □ □      │   │ 物品指名 │
│ □ □ □ □ □ □ □ □ □      │   │    □     │
│ □ □ □ □ □ □ □ □ □      │   │ [指名]   │
│                          │   │          │
│ □ □ □ □ □ □ □ □ □      │   │ 解除槽   │
└──────────────────────────┘   │    □     │
                               │ [物品解绑]│
                               └──────────┘
```

Requirements:

- player inventory + hotbar are one centered, coherent left block;
- no accidental empty strip beneath inventory;
- right side contains exactly two special slots:
  - `物品指名`
  - `物品解绑`
- buttons visually belong to their own slots;
- all borders/text use the light-gray palette.

## 7.3 Container changes

Extend `ContainerNominatorInventory` from one special slot to two explicit special slots.

Use named constants such as:

```text
NOMINATE_SLOT
UNBIND_SLOT
```

Do not rely on magic numeric indices.

Both slots:

- accept one item;
- safely return their stack to the player when appropriate;
- never delete the physical stack as a side effect of metadata operations.

---

# 8. M6 — Batch-Friendly Item Nominator Workflow

## 8.1 Do not auto-close after operations

After successful `指名`:

- keep the Item Nominator open;
- return/clear the item from `物品指名`;
- update authoritative Item identity revision;
- send refreshed revision/state to client;
- preserve browser state.

After failed `指名`:

- keep GUI open;
- show/report failure;
- keep the item safe.

After successful `物品解绑`:

- keep GUI open;
- return/clear the item from `物品解绑`;
- refresh revision/state.

## 8.2 Revision-safe batching

Do not weaken revision checks.

Each successful operation must refresh the client's revision before the next operation.

## 8.3 Preserve UI state

Preserve when still valid:

- search query
- selected Story Package
- selected resource
- package scroll
- resource scroll

Do not reset the whole browser after every operation.

---

# 9. M7 — ItemID Transfer

ItemID remains a unique exact definition.

If selected ItemID already maps to a different definition:

```text
┌────────────────────────────────────┐
│          ItemID 已被占用           │
│                                    │
│ GreyHat_:HeroSword                 │
│ 当前已绑定其他物品定义。           │
│ 是否转移到指名槽中的物品？         │
│                                    │
│ [   转移   ]      [   取消   ]     │
└────────────────────────────────────┘
```

**Left = `转移`, right = `取消`.**

Add a server-side atomic ItemID transfer operation.

Do not emulate transfer as separate unbind/bind requests.

Group memberships are independent and must not be implicitly destroyed by moving one ItemID.

---

# 10. M8 — Item Group Exact/Fuzzy Popup

## 10.1 ItemID path

When selected resource is `ItemID`:

```text
select ItemID
→ put item in 指名槽
→ click 指名
→ bind / transfer flow
```

No match-mode UI appears.

## 10.2 GroupID path

When selected resource is Item `GroupID`:

```text
select GroupID
→ put item in 指名槽
→ click 指名
→ show match-mode popup
```

Popup:

```text
┌──────────────────────────────────┐
│             匹配方式             │
│                                  │
│ [GroupID] GreyHat_:Weapons       │
│                                  │
│ 该物品以哪种方式加入此物品组？   │
│                                  │
│ [ 精准匹配 ]    [ 模糊匹配 ]     │
└──────────────────────────────────┘
```

The main Item Nominator must not permanently display Exact/Fuzzy controls.

Esc closes the popup and returns to the unchanged Item Nominator.

## 10.3 Match semantics are frozen

`精准匹配 / EXACT`:

- same registryName
- same damage/metadata
- same NBT
- stack count ignored

`模糊匹配 / FUZZY`:

- compare only registryName

Do not alter these semantics.

---

# 11. M9 — Item `ID释放` and `物品解绑`

## 11.1 ItemID `ID释放`

Remove:

`ItemID -> ItemStackDefinition`

Keep the Item resource in the package.

Use confirmation:

```text
[ ID释放 ]   [ 取消 ]
```

## 11.2 Item GroupID `ID释放`

Clear all current `ItemGroupMember` bindings under the selected GroupID.

Keep the Item Group resource itself.

Require the same confirmation modal.

## 11.3 `物品解绑`

The user puts an item into `物品解绑` and clicks `物品解绑`.

Server removes all applicable runtime bindings associated with that represented item definition, including:

- exact ItemID mappings matching that stack definition;
- exact Item Group members matching that definition;
- fuzzy Item Group members whose registry-name rule matches the stack.

For FUZZY membership, the binding unit is the registry-name rule. Removing that fuzzy member therefore removes that Group membership for all stacks covered by the same registry-name rule.

After success:

- return/clear the physical item;
- update revision;
- keep GUI open;
- refresh client state.

---

# 12. M10 — Modal Design Rules

All Nominator modals must:

- be small and centered;
- overlay the existing Nominator rather than replacing it;
- use the same light-gray palette;
- use a dark translucent background;
- preserve underlying browser/slot state;
- block clicks from reaching the underlying screen.

## Yes/No order

For yes/no dialogs:

**left = affirmative action**  
**right = `取消`**

Examples:

```text
[ 转移 ]   [ 取消 ]
[ ID释放 ] [ 取消 ]
```

## Match-mode modal

Exact/Fuzzy is not yes/no, so use:

```text
[ 精准匹配 ]   [ 模糊匹配 ]
```

No permanent match-mode controls on the main screen.

---

# 13. M11 — Server-Side Identity Service / Result Plumbing

Recommended explicit service capabilities include equivalents of:

```text
transferNpcId(...)
releaseNpcId(...)
releaseEntityGroupId(...)
unbindEntityHost(...)

transferItemId(...)
releaseItemId(...)
releaseItemGroupId(...)
unbindItemDefinition(...)
addItemGroupMemberExact(...)
addItemGroupMemberFuzzy(...)
```

Exact method names are implementation details.

Every operation must:

- validate permission;
- validate selected package/resource where applicable;
- validate catalog revision;
- validate identity/Nominator revision;
- mutate server-owned state only;
- update reverse/stale metadata coherently;
- mark saved data dirty;
- return a typed result suitable for GUI logic.

Do not make the client infer success from chat text.

For conflict/modal flows, provide explicit result data for at least:

- success
- stale revision
- occupied unique ID requiring transfer confirmation
- invalid resource
- permission failure
- already free/no-op

---

# 14. M12 — Automated Probe Requirements

At minimum cover:

## UI palette

- reviewed GUI files use shared gray palette;
- warm/gold literals do not return;
- Choice / Task / Copier / Nominator controls remain visually unified.

## Resource rows

- normal package browsing uses one-line resource rows;
- no redundant StoryID/package line;
- global search still retains package provenance.

## NPCID orphan recovery

```text
bind NPCID -> Entity A
remove/unavailable A
select NPCID
ID释放
```

Result:

- ID is free;
- stale individual metadata is cleaned;
- resource remains selectable.

## NPCID transfer

```text
NPCID -> A
attempt NPCID -> B
```

Normal bind returns transfer-required state.

Confirmed transfer produces:

`NPCID -> B`

with correct reverse mapping and stale A cleanup.

## Entity GroupID release

- same GroupID on multiple entities/type-group bindings;
- `ID释放` removes all runtime bindings;
- Group resource remains.

## `实体解绑`

- current entity has NPCID + direct GroupIDs;
- unbind clears only this host;
- other Group members remain.

## ItemID transfer

- ItemID bound to A;
- attempt B returns transfer-required;
- confirmed transfer atomically moves ItemID to B.

## Item Group modes

Use same registry name with different damage/NBT.

Verify:

- EXACT requires full definition equality;
- FUZZY matches by registryName only.

## `物品解绑`

One stack matches:

- one or more ItemIDs;
- exact Group members;
- fuzzy Group members.

After unbind:

- all applicable bindings are removed;
- unrelated bindings remain.

## Item Group `ID释放`

- Group has multiple exact/fuzzy members;
- release clears all members;
- resource remains.

## Batch Item Nominator

Perform multiple sequential operations without closing the screen.

Verify:

- GUI stays open;
- item safely returns/slot clears;
- revision refreshes;
- second/third operation is not stale;
- search/package/resource/scroll state remain.

## Modal order

Verify:

```text
left affirmative / right cancel
```

and:

```text
left EXACT / right FUZZY
```

---

# 15. M13 — Real-Machine Acceptance Gates

## Gate A — Unified gray UI

Inspect:

- Dialogue Choice
- Task UI
- Copier
- Entity Nominator
- Item Nominator
- Nominator modals
- resource IDs

No mixed gold/brown UI remains.

## Gate B — Entity resource row layout

With a package selected:

```text
史莱姆群 [GroupID] GreyHat_:Slimes
酒馆老板 [NPCID] GreyHat_:TarvenBoss
```

No redundant package/Story line under each resource.

## Gate C — NPCID dead-host recovery

Reproduce the blocking case:

1. create NPC A;
2. bind `GreyHat_:TarvenBoss`;
3. delete/remove NPC A while still bound;
4. verify the ID remains occupied;
5. select that NPCID in the resource list;
6. use `ID释放`;
7. bind the same NPCID to new NPC B.

This gate is mandatory before continuing the wider gameplay audit.

## Gate D — NPCID transfer modal

1. NPCID remains bound to A;
2. target B;
3. select same NPCID;
4. click `实体指名`;
5. modal appears;
6. buttons are left `转移`, right `取消`;
7. confirm;
8. B becomes unique host.

## Gate E — Entity unbind vs resource release

Verify:

- `实体解绑` affects current entity only;
- `ID释放` acts on the selected resource even when old host is inaccessible.

## Gate F — Item Nominator layout

Verify:

- no Close button;
- inventory/hotbar form coherent left block;
- right side contains `物品指名` and `物品解绑`;
- no gold action block.

## Gate G — Batch Item binding

Bind several items consecutively.

After every success:

- Nominator remains open;
- slot item returns/clears;
- revision refreshes;
- next bind works immediately;
- browser state remains stable.

## Gate H — ItemID transfer

Bind an ItemID to A, then attempt same ItemID on B.

Verify modal:

```text
[转移] [取消]
```

Confirm and verify ItemID now maps to B.

## Gate I — Group Exact/Fuzzy popup

Select GroupID and click `指名`.

Verify popup:

```text
[精准匹配] [模糊匹配]
```

Test same-registry-name stacks with different damage/NBT:

- EXACT excludes differing definition;
- FUZZY includes it.

## Gate J — Item unbind

Put item in `物品解绑`.

Click `物品解绑`.

Verify:

- matching ItemID / Group bindings are removed;
- physical item is returned;
- GUI remains open.

## Gate K — Resource ID release

Test all:

- NPCID
- Entity GroupID
- ItemID
- Item GroupID

Verify runtime binding is cleared while package resource remains selectable.

---

# 16. 0.3.2.2 Non-Regression Gates

The following 0.3.2.2 behavior is frozen:

## Dialogue

- server-authoritative Choice;
- click-anywhere LINE/NARRATION;
- Esc opens normal Minecraft pause menu;
- higher-priority GUI overlays Dialogue without giving DGR input;
- Dialogue underlay remains;
- death/respawn does not corrupt Session;
- reconnect reprojects active Session.

## Task

- server-authoritative Task state;
- server-push presentation;
- client read-only cache;
- no normal request-on-open loading;
- no one-second UI polling;
- Objective changes update open UI;
- settled Tasks disappear.

## Existing Nominator behavior

- global search spans all loaded packages;
- package-scoped server validation remains;
- full case-sensitive DGR IDs remain authoritative;
- Creator Inspect persistence remains;
- existing exact/fuzzy backend semantics remain unchanged.

---

# 17. Recommended Construction Order

```text
M0  Baseline / Freeze Guard
↓
M1  Shared light-gray UI palette
↓
M2  Nominator resource row cleanup
↓
M3  Operation terminology / placement
↓
M4  Entity NPCID transfer + ID释放 + 实体解绑
↓
M5  Item Nominator two-slot layout
↓
M6  Batch-safe keep-open workflow
↓
M7  ItemID transfer
↓
M8  Group EXACT/FUZZY popup
↓
M9  Item ID释放 + 物品解绑
↓
M10 Modal visual unification
↓
M11 Service/network result plumbing
↓
M12 Automated regression
↓
M13 Real-machine acceptance
↓
USER ACCEPTANCE
```

Do not split each milestone into excessive approval micro-steps, but also do not collapse all identity, container, UI and networking work into one uncontrolled rewrite.

---

# 18. Completion Criteria

0.3.2.3 may be marked technically complete only when:

```text
JAVA_BUILD=PASS
NEW_0.3.2.3_PROBES=PASS
0.3.2.2_REGRESSION=PASS
B4_RUNTIME_REGRESSION=PASS

UI_SINGLE_GRAY_PALETTE=PASS
ENTITY_RESOURCE_ROW_LAYOUT=PASS

NPCID_ORPHAN_RELEASE=PASS
NPCID_TRANSFER=PASS
ENTITY_UNBIND=PASS

ITEM_BATCH_KEEP_OPEN=PASS
ITEMID_TRANSFER=PASS
ITEM_GROUP_EXACT_FUZZY=PASS
ITEM_UNBIND=PASS
ITEM_ID_RELEASE=PASS

REAL_MACHINE_NOMINATOR_GATES=PASS

STUDIO_DIFF=0
FROZEN_SCHEMA_DIFF=0
FROZEN_CANONICAL_RUNTIME_DIFF=0
```

Then status may become:

`AGENT_REAL_MACHINE_VERIFIED`

It must remain:

`0.3.2.3_USER_ACCEPTED=NO`

until the user personally audits the delivered build and explicitly accepts it.

---

# 19. Final Interaction Model

## Entity Nominator

```text
RESOURCE AREA
├─ select NPCID / GroupID
├─ ID释放
└─ 实体指名
     └─ occupied NPCID?
          └─ [转移] [取消]

CURRENT ENTITY AREA
└─ 实体解绑
```

## Item Nominator

```text
RESOURCE AREA
├─ select ItemID
│    ├─ ID释放
│    └─ 指名
│         └─ occupied ItemID?
│              └─ [转移] [取消]
│
└─ select GroupID
     ├─ ID释放
     └─ 指名
          └─ match popup
               └─ [精准匹配] [模糊匹配]

HOST AREA
├─ 物品指名
│    └─ 指名
└─ 物品解绑
     └─ 物品解绑
```

Core rule:

> **资源操作在上，宿主操作在下；特殊选择只在需要时弹窗；唯一 ID 可迁移；任何资源都不会因为宿主消失而成为永久死 ID；批量制作不再被自动关闭 GUI 打断。**
