# DarkGrey RPG 0.3.2.1 Construction Plan

**Version focus:** Minecraft Client UX / Creator Tooling

**Baseline:** `codex/0.3.2.0_B4 @ effce10e48beb694904627a9284a930cd0e3db9c`

**Construction status:** `AUTHORIZED_TO_START`

**Frozen state:**
- `STUDIO_FROZEN = YES`
- `CANONICAL_RUNTIME_BASELINE_FROZEN = YES`
- `0.3.2.1_USER_ACCEPTED = NO`

0.3.2.1 no longer continues Studio or low-level RPG architecture work. This version focuses on the actual Minecraft-side user experience problems exposed by the B4 audit.

---

## 1. Hard Freeze Boundary

### 1.1 Studio is fully frozen

Normal 0.3.2.1 construction must not modify:

- Studio WPF/C# UI
- Studio node definitions
- Story / Session / Task authoring schema
- `.dgrs` format
- Namespace / Origin
- Resource ownership / external reference rules
- Story Package export semantics
- Canonical Story lifecycle
- Canonical Session state machine
- Canonical Task lifecycle
- Actor arbitration

Allowed changes are limited to:

- Minecraft client UI
- Minecraft client-side presentation state
- Minimal network transport required by the new client UI
- Creator tooling items
- Player-side persisted creator/debug preferences

If construction proves that a required piece of data cannot be expressed by the existing frozen Canonical contracts, stop that subtask and report it. Do not unfreeze Studio by inference.

Also prohibited:

- Reintroducing a second Dialogue runtime
- Reintroducing a second Quest/Task runtime
- Introducing a large GUI framework
- Adding Studio portrait/image fields for 0.3.2.1
- Storing Inspect state in fake Story IDs
- Writing DGR Item IDs directly into ordinary ItemStack NBT just for display

---

# 2. Phase 0 — Baseline and Freeze Guard

## Goal

Enter 0.3.2.1 safely from the accepted B4 baseline.

## Construction

Create branch:

`codex/0.3.2.1`

from:

`effce10e48beb694904627a9284a930cd0e3db9c`

Upgrade Java mod version:

`0.3.2.0 -> 0.3.2.1`

Do **not** version-bump or republish Studio for this release.

Run the full B4 Java baseline before implementation.

Add a final diff gate so that the 0.3.2.1 diff against B4 contains no unauthorized Studio/schema/DGRS/Namespace modifications.

## Completion gate

`BUILD PASS + B4 BASELINE PASS + STUDIO DIFF = 0`

---

# 3. Phase 1 — Canonical Dialogue + Choice UI Rework

This is the first priority of 0.3.2.1.

The existing large centered blue/purple prototype GUI is retired.

## 3.1 Dialogue Box

Rework the existing Canonical dialogue screen while preserving the existing data path:

`CanonicalSessionFrame -> ClientModel -> GUI -> CanonicalSessionAction -> Server`

Target layout:

```text
┌──────────────────────────────────────────┐
│                  world                   │
│                                          │
│             ┌──────────────┐             │
│             │ Choice 1     │             │
│             │ Choice 2     │             │
│             │ Choice 3     │             │
│             └──────────────┘             │
│                                          │
│ ┌──────┐  Speaker Name                   │
│ │portrait│ dialogue text...              │
│ │ slot │                                 │
│ └──────┘                                 │
└──────────────────────────────────────────┘
```

Requirements:

- Fixed at the bottom of the screen
- About 5% horizontal safe margin
- Height around 22%–28% of the screen
- Dark gray / black translucent background
- Light border
- No large blue/purple management-panel styling
- Must support Minecraft GUI Scale
- Chinese text wrapping must remain correct

### Portrait

0.3.2.1 keeps only an **empty portrait slot**.

Do not add Studio portrait fields.
Do not use random character artwork as a fake portrait.

## 3.2 Input

When dialogue GUI is open:

- Mouse is free
- Left-clicking the Dialogue Box advances LINE / NARRATION
- Enter / Space may remain keyboard shortcuts
- Clicking arbitrary world/background areas must not advance dialogue

## 3.3 Choice

Choice buttons move out of the Dialogue Box and appear above it, centered vertically in a list.

Only clicking an actual Choice option selects it.

The client must continue sending only the existing Canonical choice action and must not decide Story/node execution locally.

If the Choice frame has prompt text:
- display the prompt

If the Choice frame has no prompt:
- preserve the previous visible dialogue line as client-side context

This must be presentation-only; do not change Studio or Canonical Session authoring contracts.

## Phase 1 freeze

Do not modify:

- Canonical Session Runtime semantics
- Server-owned choice token behavior
- Session persistence semantics
- Story continuation semantics

## Phase 1 real-machine acceptance

At minimum:

`LINE -> LINE -> CHOICE -> branch -> LINE -> END`

Verify:

- Mouse behavior
- Click behavior
- No accidental choice
- Chinese wrapping
- Low-resolution layout
- Esc does not violate the existing Session contract

---

# 4. Phase 2 — Entity and Item Nominator UI Rework

Entity and Item Nominator should share the same interaction language without creating a large GUI framework.

Only small reusable helpers are allowed, such as:

- Search field helper
- Scroll/list helper
- Resource row helper

---

## 4.1 Entity Nominator

Final layout:

**Top global search + left Story Package list + right resource list**

Normal browsing:

```text
┌─────────────────────────────────────────────┐
│ [ Search NPC ID / Group ID / name / tag ]  │
├──────────────┬──────────────────────────────┤
│ Story A      │ [NPC] Tavern Keeper          │
│ Story B      │       Test:tavernboss        │
│ Story C      │                              │
│              │ [Group] Tavern Staff         │
│              │         Test:bartenders      │
│              │                              │
├──────────────┴──────────────────────────────┤
│ Current binding...                  [Bind]  │
└─────────────────────────────────────────────┘
```

Remove the old:

- “Target”
- “Search...” label text
- Previous package
- Next package
- Previous actor
- Next actor

### Global Search

The search field searches **all currently loaded Story Packages**.

Search fields:

- NPC ID
- Group ID
- DisplayName
- Tags

When the query is non-empty, the right panel temporarily becomes **Global Search Results**.

Results are scoped as:

`Package + Resource`

rather than globally deduplicating by resource ID.

Each result should expose:

- resource type
- display name
- full ID
- package/source

Selecting a search result should:

- select/highlight the corresponding Story Package on the left
- select the resource on the right

NPC and Group should be displayed in one unified list with a lightweight type marker.

---

## 4.2 Item Nominator

Keep the existing `GuiContainer` / Container architecture.

Do **not** rebuild Minecraft inventory mechanics.

Required layout:

- Top global search
- Left Story Package list
- Right Item / Item Group list
- Clearly visible player inventory
- Hotbar
- Clearly visible “Nominator Slot”

Example:

```text
┌─────────────────────────────────────────────┐
│ [ Search Item ID / Group ID / name / tag ] │
├─────────────┬───────────────────────────────┤
│ Story pack  │ [Item] Silver Key             │
│             │ Test:silver_key               │
│             │                               │
│             │ [Group] Keys                  │
│             │ Test:keys                     │
├─────────────┴───────────────────────────────┤
│                                             │
│      Nominator Slot [ □ ]                   │
│                                             │
│      □ □ □ □ □ □ □ □ □                    │
│      □ □ □ □ □ □ □ □ □                    │
│      □ □ □ □ □ □ □ □ □                    │
│                                             │
│      □ □ □ □ □ □ □ □ □                    │
└─────────────────────────────────────────────┘
```

Minecraft slot backgrounds must actually be visible.

The player must be able to immediately see where items can be placed.

Item and Item Group should be shown in one list with a lightweight type marker.

Global search behavior matches Entity Nominator.

## Phase 2 authority boundary

Server remains authoritative for:

- package membership validation
- catalog revision validation
- target stack validation
- actual binding
- acceptance/rejection result

Client must never announce a successful bind on its own.

---

# 5. Phase 3 — Creator Identity Inspection

Formal feature:

**Creator Identity Inspection**

It has two parallel entry points:

1. Persistent `/dgr inspect`
2. DGR Inspector Goggles

Both drive the same effective Inspect system.

---

## 5.1 Persistent `/dgr inspect`

Default:

`OFF`

First use:

```text
/dgr inspect
-> DGR identity display: ON
```

Second use:

```text
/dgr inspect
-> DGR identity display: OFF
```

The state must be written to the **world save**.

Persistence requirements:

- keyed by player UUID
- survives logout/login
- survives server restart
- survives game restart while the same world/save is used

Do not fake this by writing a special Story ID into `PlayerRpgSavedData`.

Recommended implementation:

`CreatorInspectSavedData`

Minimal representation may simply be:

`Set<UUID> enabledPlayers`

stored in overworld `MapStorage`.

---

## 5.2 Inspector Goggles

Add a DGR head-slot equipment item:

**DGR Inspector Goggles**

Recommended behavior:

- No armor bonus
- No combat/stat bonus
- No durability consumption
- No crafting recipe required
- Available from the DarkGrey RPG Creative Tab
- Pure creator/debug utility

Prefer a simple Forge/Minecraft armor-equipment-compatible implementation rather than inventing a new equipment system.

---

## 5.3 Effective Inspect State

The final rule is:

```text
effectiveInspect =
    persistentInspectEnabled
    OR
    wearingInspectorGoggles
```

The two inputs must not mutate each other.

Examples:

Persistent OFF + goggles equipped
-> ON

Goggles removed
-> OFF

Persistent ON + goggles equipped
-> ON

Goggles removed
-> still ON

---

## 5.4 Entity Identity Display

When Inspect is ON, nearby entities with DGR identities display identity text above the entity.

Example:

```text
Test:tavernboss
Group: Test:bartenders
```

Group-only example:

```text
Group: Test:slimes
```

Entities without DGR identity display nothing.

Recommended maximum distance:

~32 blocks

Visibility may fade with distance.

Do not show:

- UUID
- runtime entity ID
- registry name

unless a future deeper debug mode explicitly requests them.

This system answers only:

**“Who does DGR currently consider this entity to be?”**

---

## 5.5 Item Identity Display

Do **not** draw identity labels over dropped world items.

When Inspect is ON, item identity is shown only when the mouse cursor is hovering an item icon in:

- Inventory
- Chest
- Container
- Nominator
- other ordinary GUI item slots

Append DGR identity information to the normal Tooltip.

Example:

```text
Diamond Sword
...

DGR Item:
  Test:hero_sword

DGR Group:
  Test:swords
  Shared:weapons
```

This means:

**“This ItemStack currently matches these DGR Item / Item Group definitions.”**

It does not imply that the ItemStack itself owns those IDs internally.

---

## 5.6 Inspect Network Strategy

DGR identity remains server-authoritative.

The client must not guess server-owned identity state.

At the same time, do not create per-render-frame network traffic.

### Entity identity

When Inspect is effective:

- server synchronizes nearby relevant entity identities
- small radius
- throttled updates
- revision/change-driven refresh where practical
- no per-frame request spam

### Item identity

When Inspect becomes effective:

- synchronize a lightweight client-side Item identity matcher catalog
- Tooltip matching occurs locally

Refresh when Item identity revision changes.

Use client-side cache keyed by:

`ItemStack fingerprint + catalog revision`

to avoid repeatedly matching the same stack every tooltip frame.

---

# 6. Phase 4 — Canonical Task UI

0.3.2.1 implements a key-bound Task screen, **not** a permanent HUD tracker.

---

## 6.1 Key Binding

Add a standard Minecraft KeyBinding:

**Open Tasks**

Default:

`I`

It must appear under:

`Options -> Controls`

so the player can rebind it.

---

## 6.2 New Canonical Task Screen

Add:

`GuiCanonicalTaskScreen`

Do not build the new official Task UI on the legacy Quest DTO layer.

Recommended path:

```text
Canonical Task
      ↓
Canonical Task Journal Projection
      ↓
S2C Canonical Task UI Snapshot
      ↓
GuiCanonicalTaskScreen
```

The old `GuiQuestJournal` may remain for compatibility but should not remain the new official player entry point.

Avoid creating two competing task UIs.

---

## 6.3 Display Rules

Only currently relevant Canonical Task state is shown.

Display all simultaneously **ACTIVE Objectives** for each active task.

Example:

```text
Clear the Basement

● Kill slimes
  2 / 3

● Find the basement entrance
```

When an Objective completes:

If a later Objective becomes ACTIVE:
- replace the old Objective with the new active Objective

Example:

```text
Kill slimes 3/3
```

becomes:

```text
Return to the tavern keeper
```

If the current objective set is complete but Task has not yet entered Settlement:
- show `Objective complete`

When Task enters Settlement:
- remove the entire Task from the Task UI

Do not implement in 0.3.2.1:

- completed-history page
- failed-history page
- achievement log
- quest tree
- encyclopedia

### Multiple active Tasks

If several Canonical Tasks are active simultaneously:
- display all of them
- each Task is its own section/block

This is presentation aggregation only; do not change Task Runtime semantics.

---

# 7. Phase 5 — Story Chooser Presentation

B4 Actor arbitration behavior is frozen.

Do not modify:

```text
0 candidates -> pass
1 candidate  -> direct
2+           -> chooser
```

Do not change:

- server token ownership
- index-only client selection
- server-side revalidation

Only rework presentation.

Recommended UI:

```text
Multiple stories are available

Tavern Trouble
AVAILABLE

Missing Merchant
AVAILABLE
```

Primary text:
- Story DisplayName

Secondary:
- state/status

Full Story ID may be shown in weaker text for creator usefulness, but should not dominate the visual hierarchy.

---

# 8. Phase 6 — Visual Consistency and Compatibility

This phase does not redesign systems again. It only unifies presentation.

## Vanilla-like surfaces

Keep these close to Minecraft's own UI language:

- Entity Nominator
- Item Nominator
- Task UI
- Story Chooser
- Inspect Tooltip

## RPG narrative surfaces

These may clearly depart from vanilla:

- Dialogue
- Choice

Unify:

- font sizes
- margins
- hover state
- disabled state
- scroll behavior
- tooltip behavior
- DarkGrey RPG dark-gray visual language

Do not return to the B4 style of giant centered blue/purple engineering panels.

---

# 9. Phase 7 — Automated Regression

At minimum add:

## 9.1 `NominatorGlobalSearchProbe`

Cover:

- cross-package search
- ID search
- DisplayName search
- Tag search
- same resource visible through multiple package closures
- package-scoped final selection

## 9.2 `CreatorInspectSavedDataProbe`

Cover:

- default OFF
- toggle ON/OFF
- save/load
- player UUID isolation
- persistence across reload/restart simulation

## 9.3 `CreatorInspectEffectiveStateProbe`

Cover:

- persistent OFF + no goggles
- persistent OFF + goggles
- persistent ON + no goggles
- persistent ON + goggles

## 9.4 Inspect network codec probe

Cover malformed and valid payloads.

## 9.5 Item identity tooltip/matcher projection probe

Cover:

- exact Item ID match
- Item Group match
- multiple groups
- no DGR match
- revision/cache invalidation behavior where testable

## 9.6 Canonical Task UI projection probe

Cover:

- active task
- multiple simultaneous active objectives
- objective transition
- objective-complete-without-settlement
- settlement removal
- multiple active tasks

## 9.7 Network discriminator uniqueness

All new packets must keep discriminator registration unique and deterministic.

## 9.8 Existing Canonical Session / Choice regression

All existing B4 Canonical Session, Story arbitration, Task and package probes continue to pass.

---

# 10. Real-Machine Acceptance Gates

## Gate A — Dialogue

Real NPC:

`LINE -> LINE -> CHOICE -> LINE -> END`

Verify:

- bottom dialogue box
- free mouse
- click-to-continue
- choice click
- no accidental advancement
- Chinese text
- GUI Scale behavior

## Gate B — Entity Nominator

Use at least two Story Packages.

Verify:

- left Package list
- right resource list
- global search
- NPC ID search
- Group ID search
- name search
- tag search
- successful bind

## Gate C — Item Nominator

Verify:

- visible inventory slots
- visible target/nominator slot
- Item selection
- Item Group selection
- global search
- successful bind

## Gate D — `/dgr inspect`

Start OFF.

Enable.

Logout and rejoin:
- still ON

Fully restart server:
- still ON

Disable.

Logout/rejoin:
- still OFF

## Gate E — Inspector Goggles

Persistent OFF.

Equip goggles:
- IDs appear

Remove goggles:
- IDs disappear

Persistent ON.

Equip/remove goggles:
- IDs remain visible

## Gate F — Identity Presentation

Test:

- NPC individual identity
- Group-only identity
- individual + Group identity
- no DGR identity
- Item tooltip with Item ID
- Item tooltip with Item Group
- Item tooltip with multiple groups
- no-match item

## Gate G — Task UI

Use a real task such as `kill_slimes`.

Verify:

`0/3 -> 1/3 -> 2/3 -> 3/3 -> next objective -> settlement -> UI disappears`

Verify default key:

`I`

## Gate H — Story Chooser

Bind one NPC so at least two Stories are currently available.

Verify:

- chooser appears
- selecting one Story starts only that Story
- other Story state is not incorrectly changed

---

# 11. B4 Non-Regression Gates

Even though 0.3.2.1 is primarily a UI release, the following B4 contracts must still be sampled:

- Repeatable Run1 / Run2 / Run3
- ACTIVE Story cannot start again
- ONCE terminal Story cannot restart
- Actor arbitration
- Canonical Session continuation
- Canonical Task progress
- DGRS package reload
- Namespace / full ID propagation

A visually successful 0.3.2.1 is still a failure if it breaks the accepted B4 runtime behavior.

---

# 12. Explicitly Out of Scope

0.3.2.1 does **not** include:

- Studio image/portrait editing
- Portrait authoring
- Dialogue voice
- Typewriter animation framework
- Permanent right-side Task HUD
- Quest history
- Minimap objective markers
- World dropped-item ID labels
- Inspect settings GUI
- New Story nodes
- New Task nodes
- New Session nodes
- New DGRS schema
- Large UI animation framework
- Legacy runtime clean-room rewrite/removal

Convenience is not authorization to expand scope.

---

# 13. Recommended Milestone Order

Use:

**M0 — Baseline / Freeze Guard**
→ **M1 — Dialogue + Choice**
→ **M2 — Entity + Item Nominator**
→ **M3 — Creator Identity Inspection + Goggles**
→ **M4 — I-key Canonical Task UI**
→ **M5 — Story Chooser + Visual Consistency**
→ **M6 — Full Regression**
→ **M7 — Real-machine Acceptance**
→ **USER ACCEPTANCE**

M1–M4 are the core construction of 0.3.2.1.

Do not split work into dozens of tiny approval steps, but also do not let one implementation pass simultaneously rewrite Dialogue, Task, Nominator, persistence and network architecture.

---

# 14. Final Closure Conditions

Before 0.3.2.1 may be marked technically complete:

```text
JAVA_BUILD=PASS
NEW_0.3.2.1_PROBES=PASS
B4_REGRESSION=PASS
REAL_MACHINE_GATES=PASS
STUDIO_DIFF=0
CANONICAL_CONTRACT_REGRESSION=0
```

Then status may become:

`AGENT_REAL_MACHINE_VERIFIED`

But it must remain:

`0.3.2.1_USER_ACCEPTED=NO`

until the user personally performs acceptance and explicitly approves the version.

---

# 15. Construction Boundary Summary

0.3.2.1 is the first fully Studio-frozen client-experience release.

The version exists to make the already-accepted B4 Canonical runtime usable and presentable in normal Minecraft play and creator workflows.

The core principle is:

**Reuse the accepted runtime; replace prototype presentation; add only the minimum client/network/persistence plumbing needed for the approved UX.**
