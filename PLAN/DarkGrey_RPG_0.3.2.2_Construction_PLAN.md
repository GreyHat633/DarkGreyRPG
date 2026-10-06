# DarkGrey RPG 0.3.2.2 Construction Plan

**Version:** `0.3.2.2`  
**Repository:** `GreyHat633/DarkGrey_RPG`  
**Baseline branch:** `codex/0.3.2.1`  
**Baseline commit:** `0f1f002aba10a7e1ae12564d6fdda093d5389ef4`  
**Target branch:** `codex/0.3.2.2`

---

# 0. Version Positioning

0.3.2.2 is a focused **real-machine audit correction + client UX refinement release** over 0.3.2.1.

Primary goals:

1. Complete Dialogue / Choice interaction polish.
2. Redesign Dialogue GUI layering so DGR stays underneath higher-priority GUIs without stealing input.
3. Define Esc / death / disconnect / reconnect behavior without introducing Session suspend/cancel semantics.
4. Replace Task UI pull-on-open polling with server-push + client read-only cache.
5. Reproduce and fix the real audited Task Objective transition problem.
6. Redesign Task UI into a mature RPG-style menu consistent with Dialogue.
7. Rework Copier UI to match the accepted Nominator tool style.
8. Improve Item Nominator inventory / target-slot layout.
9. Normalize creator-tool names and Inspect ID labels.

0.3.2.2 must not become a new runtime architecture phase.

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
- package ownership/reference contracts
- Studio export semantics

Do not invent author-facing distinctions such as:

- important dialogue
- normal dialogue
- cutscene dialogue
- interruptible dialogue

DGR does not currently have these concepts.

## 1.2 Canonical runtime remains authoritative

Do not replace or duplicate:

- Canonical Story Runtime
- Canonical Session Runtime
- Canonical Task Runtime
- Actor arbitration
- Session choice authority
- Task persistence
- Story/Task/Session server authority

Allowed runtime-side work is limited to minimal plumbing required for:

- client presentation synchronization
- reconnect/resume presentation
- GUI priority/focus behavior
- proven bug fixes from the real-machine audit

## 1.3 Explicitly prohibited

Do not add:

- Session suspend system
- “unfinished dialogue” list
- Dialogue TTL / timeout
- Dialogue save/load
- Choice rollback / SL
- Objective rollback because Dialogue was interrupted
- re-triggering previous gameplay prerequisites
- permanent Task HUD
- new Quest runtime
- new Dialogue runtime
- large UI framework
- new Story/Task/Session nodes

---

# 2. M0 — Baseline / Branch / Freeze Guard

Create:

`codex/0.3.2.2`

from:

`0f1f002aba10a7e1ae12564d6fdda093d5389ef4`

Update Java mod version:

`0.3.2.1 -> 0.3.2.2`

Do not rebuild or version-bump Studio.

Before production changes:

- run the complete 0.3.2.1 regression/probe set
- confirm current baseline behavior
- add/update a freeze-diff guard against unauthorized Studio/schema/DGRS modifications

Gate:

```text
BASELINE_BUILD=PASS
BASELINE_PROBES=PASS
STUDIO_DIFF=0
FROZEN_SCHEMA_DIFF=0
```

---

# 3. M1 — Dialogue / Choice Visual and Click Polish

## 3.1 Choice visual unification

Current vanilla-style Choice buttons must be replaced with a style consistent with the Dialogue Box.

Requirements:

- dark RPG visual language
- translucent dark background
- restrained light/warm border
- clear hover state
- clear disabled state
- readable Chinese text
- no disconnected vanilla gray-button appearance

Do not change Choice IDs, packet semantics, or server validation.

## 3.2 Click anywhere to continue

For `LINE` and `NARRATION`:

- left-click anywhere on the visible game screen advances the current line
- no longer require clicking inside the Dialogue Box

Exceptions:

- if a higher-priority GUI owns the screen, DGR receives no click
- during `CHOICE`, clicking outside actual Choice options does nothing
- while waiting for server confirmation, repeated clicks are ignored

## 3.3 Text cleanup

Change:

`点击对话框继续`

to:

`点击继续`

Remove visible:

`等待服务器…`

Keep the internal `awaitingServer` / duplicate-submit guard.

---

# 4. M2 — Dialogue GUI Layering / Input Priority

## Goal

DGR Dialogue becomes the **lowest-priority interactive GUI layer** while remaining visually present under higher-priority GUIs.

This is not a Session suspend system. The Session remains ACTIVE.

## 4.1 Visual stack

Target order:

```text
Top:
  Death screen / Pause menu / Chat / Inventory / Chest / Other Mod GUI
  ↓
  normal darkened GUI background
  ↓
  DGR Dialogue / Choice
  ↓
  Minecraft world
Bottom
```

When another GUI opens:

- DGR Dialogue remains rendered underneath
- upper GUI owns mouse and keyboard exclusively
- DGR does not receive click, wheel, hover, or Choice input
- Session state does not change

## 4.2 Do not hardcode chat keys

Do not hardcode:

- `T`
- `Enter`
- `/`
- any fixed key code

Players can remap controls.

Use Minecraft's actual configured KeyBinding / normal input routing.

Preferred direction:

- allow normal keybinding processing while DGR Dialogue is foreground
- let Minecraft or another Mod open its GUI normally
- when a non-DGR GUI becomes foreground, DGR becomes render-only underneath it

If 1.7.10 requires a compatibility bridge, read the actual configured binding rather than assuming a default.

## 4.3 Generic Mod compatibility

Do not build a whitelist of login mods.

The generic rule is:

> Any non-DGR GUI that becomes foreground has input priority over DGR Dialogue.

This must allow normal use of:

- Chat
- Inventory
- containers
- options/pause screens
- other Mod GUIs

## 4.4 Recommended responsibility split

```text
CanonicalSessionClientModel
    └─ authoritative client presentation state

CanonicalDialogueRenderer
    └─ reusable drawing only

GuiCanonicalSessionScreen
    └─ DGR foreground input owner

GUI draw hook / presentation bridge
    └─ draws active Dialogue underneath non-DGR GUIs
```

Do not create a second Session model.

---

# 5. M3 — Esc / Death / Disconnect / Reconnect

## 5.1 Esc

While DGR Dialogue is foreground:

**Esc opens the normal Minecraft pause/exit menu.**

Esc must NOT:

- cancel Session
- reset Session
- reset Story
- reset preceding Objective
- rollback Choice
- discard current node progress

Do not simply fall back to normal `GuiScreen` close behavior if that returns directly to the game world.

## 5.2 Returning from pause

When the pause/menu GUI closes:

- if Session is still ACTIVE, DGR Dialogue returns to foreground
- same Session
- same transport ID
- same current node
- same selected Choices
- no restart

## 5.3 Death

Death screen has higher priority than DGR.

Requirements:

- respawn button must always work
- DGR must not block death-screen input
- Dialogue may remain visible underneath if render ordering permits

After respawn:

- if Session is still ACTIVE, same current Dialogue returns
- no Story reset
- no Task reset
- no Session restart

## 5.4 Disconnect / game exit

If player disconnects during an ACTIVE Session:

- keep server-side persisted Session state
- do not cancel because the client disconnected

On reconnect:

- server restores/reads the existing Canonical Session state
- re-project/re-send the current Session frame
- if another higher-priority GUI is open, DGR does not steal foreground
- once higher-priority GUI is gone, active Dialogue becomes foreground

### Login compatibility

If login uses `/login` in Chat:

- remapped Chat binding must still work

If login uses a GUI:

- that GUI stays above DGR
- DGR must not replace it when a Session frame arrives

## 5.5 Session completion

A player only truly exits the Canonical Session by reaching the authored:

`【结束】 / END`

node.

No mid-Session player-facing suspend/cancel feature is added.

---

# 6. M4 — Task Synchronization Architecture

## Goal

Remove the visible pull-on-open loading behavior.

0.3.2.1:

```text
Press I
→ open GUI
→ request server
→ blank/loading
→ receive snapshot
→ render
→ poll again while open
```

0.3.2.2:

```text
Server Canonical Task state
        ↓
meaningful state change
        ↓
server pushes presentation snapshot
        ↓
client read-only cache
        ↓
Press I
        ↓
render immediately
```

## 6.1 Server remains the sole authority

Client cache must never:

- complete Objective
- increment progress
- activate Objective
- settle Task
- infer Task transitions

It only stores the latest server-produced presentation.

## 6.2 Add client Task presentation cache

Recommended component:

`CanonicalTaskClientStore`

Responsibilities:

- hold latest complete Task UI snapshot
- hold monotonic revision/generation
- atomically replace older state
- expose detached/read-only data to GUI
- clear on world/server change
- contain no Task execution logic

Possible shape:

```text
revision
tasks[]
lastSyncTime
```

## 6.3 Server push triggers

Push a fresh full Task presentation snapshot to the affected player when visible Task state changes:

- player login / initial join
- Task creation
- Task re-entry if presentation changes
- Objective progress change
- Objective becomes ACTIVE
- Objective becomes COMPLETED
- Task settlement
- Task cancel/discard/reset
- reload/reconciliation changes visible state

Never broadcast one player's Task state to other players.

## 6.4 Prefer full snapshots, not delta protocol

Do not create a complex packet family like:

- TASK_ADD
- OBJECTIVE_UPDATE
- OBJECTIVE_REMOVE
- TASK_REMOVE

For 0.3.2.2, send a complete presentation snapshot whenever meaningful state changes.

Client rule:

```text
if incomingRevision > localRevision:
    replace snapshot
```

## 6.5 Opening Task UI

Pressing the Task key must:

- open immediately
- render immediately from `CanonicalTaskClientStore`
- not wait for a network round trip
- not show a normal loading/blank stage

Remove normal:

- request-on-init
- one-request-per-second polling while open

## 6.6 Optional reconciliation

A low-frequency/full-resync request may remain only as abnormal recovery for:

- missing/stale cache
- reload
- reconnect recovery
- developer diagnostics

If used:

- current cached data stays visible
- no visible loading blank
- incoming authoritative state replaces cache silently

---

# 7. M5 — Task Objective Transition Bug

## Goal

Reproduce and fix the user's real-machine issue:

```text
消灭史莱姆
→ completed
→ 与酒馆老板对话 should become ACTIVE
```

but the UI still appeared to remain on the old Objective.

Do not fix this by hardcoding UI text.

## 7.1 Required trace

Use:

```text
Task event
→ CanonicalTaskRuntime.accept()
→ refreshState()
→ Objective status map
→ CanonicalTaskInstance snapshot
→ CanonicalTaskSavedData
→ CanonicalTaskJournalProjector
→ Task UI projection
→ server push
→ CanonicalTaskClientStore
→ GuiCanonicalTaskScreen
```

## 7.2 Use the actual audited package

Do not claim success only with a synthetic Codex fixture.

Use the exact `.dgrs` / Task resource that produced the audit behavior if available.

If unavailable:

- synthetic coverage may be added
- but real-machine bug closure must not be claimed until the actual package is reproduced

## 7.3 Distinguish the real root cause

Prove whether the failure is in:

- authored/exported Task graph
- prerequisite/logic connection
- Canonical Task Runtime
- persistence
- Journal projection
- UI projection
- network synchronization
- stale client cache
- GUI rendering

## 7.4 Acceptance

After Objective A completes:

- A is no longer shown as ACTIVE
- B appears if Runtime marks it ACTIVE
- client cache updates without closing/reopening Task UI
- settlement removes Task from active presentation

---

# 8. M6 — Task UI Visual Redesign

## Goal

Replace the 0.3.2.1 prototype Task screen with a mature RPG menu consistent with Dialogue.

## 8.1 Visual language

Use:

- dark translucent panels
- restrained borders
- warm/light highlight accents consistent with Dialogue
- clear hierarchy
- deliberate spacing
- readable Chinese text
- minimal visual noise

Avoid:

- huge empty black panel
- upper-left debug-text dump
- engineering/debug-panel appearance
- disconnected vanilla gray-button style

## 8.2 Recommended normal layout

Use two columns when resolution permits:

```text
┌────────────────────────────────────────────────────────┐
│                        任务                            │
├───────────────────┬────────────────────────────────────┤
│ Active Task List  │ Selected Task                      │
│                   │                                    │
│ > 地下室的麻烦    │ 地下室的麻烦                      │
│   失踪的商人      │                                    │
│                   │ 当前目标                           │
│                   │ ● 与酒馆老板对话                  │
│                   │                                    │
│                   │ progress/details                   │
├───────────────────┴────────────────────────────────────┤
│                       Esc 返回                         │
└────────────────────────────────────────────────────────┘
```

Left:

- active Task list
- selected Task highlight

Right:

- Task title
- all simultaneously ACTIVE Objectives
- descriptions
- progress where meaningful

## 8.3 Low-resolution fallback

At low logical resolutions such as effective `320x240`:

- collapse to a compact stacked/single-column layout if needed
- no horizontal scrolling
- no controls/text outside screen
- keep information hierarchy intact

Exact pixel geometry may be iterated; behavior is fixed.

## 8.4 Lifecycle presentation

Rules remain:

- show every ACTIVE Task
- show every simultaneously ACTIVE Objective
- completed Objective disappears once no longer ACTIVE
- new ACTIVE Objective appears immediately
- settled Task disappears
- no completed-history page
- no failed-history page
- no permanent HUD

## 8.5 Key binding

Default remains:

`I`

but the player may remap it in Controls.

---

# 9. M7 — Copier UI Rework

## Goal

Bring Copier presentation into the same visual family as the accepted Entity Nominator.

The accepted Entity Nominator itself must not be unnecessarily redesigned.

## 9.1 Remove old Copier presentation

Retire:

- blue/purple panel
- fixed 6-row paging
- previous/next page navigation

## 9.2 New Copier layout

Use one vertical scroll list:

```text
┌─────────────────────────────────────────┐
│               复制器                    │
├─────────────────────────────────────────┤
│ ✓ 1. Template summary                   │
│   2. Template summary                   │
│   3. Template summary                   │
│   4. ...                                │
│                                         │
│               scroll                    │
├─────────────────────────────────────────┤
│ [删除]                          [关闭]   │
└─────────────────────────────────────────┘
```

Requirements:

- gray/dark tool UI like Nominator
- mouse-wheel scrolling
- clear selected row
- deliberate delete confirmation
- no Story Package submenu
- no resource submenu

Do not change:

- template storage
- copier state semantics
- server validation
- copying behavior

---

# 10. M8 — Item Nominator Lower Layout

## 10.1 Freeze accepted upper area

Do not redesign:

- global search
- Story Package list
- resource list
- Item / Item Group selection semantics
- Entity Nominator visual layout

## 10.2 Re-layout inventory and nomination controls

Preferred shape:

```text
玩家背包                         指名
┌──────────────────────────┐   ┌──────────┐
│ □ □ □ □ □ □ □ □ □      │   │          │
│ □ □ □ □ □ □ □ □ □      │   │    □     │
│ □ □ □ □ □ □ □ □ □      │   │  指名槽  │
│                          │   │          │
│ □ □ □ □ □ □ □ □ □      │   │  [指名]  │
└──────────────────────────┘   └──────────┘
```

Requirements:

- inventory forms one coherent block
- hotbar visually belongs to inventory
- nomination slot becomes a deliberate right-side action block
- nomination slot is obvious
- bind button visually belongs with the nomination slot
- no isolated floating slot in empty space

Keep existing `GuiContainer` / `ContainerNominatorInventory` mechanics and server authority.

---

# 11. M9 — Creator Tool Names / Inspect Labels

## 11.1 Tool display names

Remove unnecessary visible `DarkGrey RPG` / `DGR` prefixes.

Final Simplified Chinese names:

- `编辑器`
- `指名器`
- `复制器`
- `收纳箱`
- `素体`
- `检查器`

Creative Tab may remain:

`DarkGrey RPG`

### Save compatibility

Do not rename registered item IDs solely to change visible names.

Keep registry/unlocalized identity stable where possible.

## 11.2 Item Inspect Tooltip

Replace:

```text
DGR Item:
  GreyHat_:CopperCoin
```

with:

```text
[ItemID] GreyHat_:CopperCoin
```

Groups:

```text
[GroupID] GreyHat_:Coins
```

Requirements:

- one line per matched ID
- stronger/high-contrast accent than current gray text
- readable on vanilla tooltip background
- identity remains a match against synchronized DGR definitions, not permanent ItemStack ownership

## 11.3 Entity Inspect labels

Use:

```text
[NPCID] GreyHat_:TarvenBoss
[GroupID] GreyHat:Slimes
```

If multiple groups match:

- one `[GroupID] ...` line per group

Do not redesign the accepted entity identity plate appearance.

---

# 12. Network / Synchronization Safety

Requirements:

- packet discriminators unique
- malformed payloads rejected
- size bounds retained
- player-specific Task data only sent to that player
- Task cache revision rejects stale/out-of-order snapshots
- reconnect/world change clears stale client-world cache
- Session identity (`transportId`, `storyId`, `currentNodeId`) remains validated
- opening another GUI never synthesizes Continue/Choice
- Dialogue rendered underneath another GUI never receives input focus

Do not turn presentation synchronization into an unbounded generic protocol.

---

# 13. Automated Probe Requirements

Add/update coverage for at least:

## Dialogue

- LINE click anywhere advances
- NARRATION click anywhere advances
- CHOICE outside-option click does nothing
- awaiting-server blocks duplicate action
- visible waiting text removed
- Choice visual/input behavior remains bounded

## GUI priority

- DGR foreground
- Chat above
- Inventory above
- arbitrary test GUI above
- DGR remains renderable underneath
- DGR underneath receives no click
- closing upper GUI restores DGR foreground

## Remapped chat key

Verify Dialogue behavior does not depend on literal `T`.

Test with a non-default configured Chat key.

## Esc

- Esc from DGR opens normal pause/menu
- Session remains ACTIVE
- node unchanged
- returning restores DGR

## Death / respawn

- death screen foreground
- respawn usable
- same Session returns after respawn

## Reconnect

- active Session persists
- reconnect reprojects current Session
- same node
- same Choice state
- no preceding Task rollback

## Task cache

- initial login snapshot
- monotonic revision
- stale snapshot rejected
- state-change push replaces cache
- opening UI does not request data in normal path
- no 20-tick polling loop
- settlement removes Task
- world/server change clears stale cache

## Task Objective transition

Using actual audited package where available:

- first Objective progresses
- first completes
- second activates
- client cache updates
- UI shows second Objective

## Copier

- scrolling keeps correct selection
- delete confirmation targets correct template
- large template count needs no page buttons

## Item Nominator

- target slot mapping unchanged
- inventory mechanics unchanged
- bind validation unchanged

## Identity labels

- `[ItemID]`
- `[GroupID]`
- `[NPCID]`
- multiple groups
- no identity -> no extra label

---

# 14. Real-Machine Acceptance Gates

## Gate A — Dialogue / Choice

Run:

```text
LINE
→ LINE
→ CHOICE
→ branch
→ LINE
→ END
```

Verify:

- Choice style matches Dialogue
- click anywhere advances LINE/NARRATION
- Choice works correctly
- no visible “等待服务器…”
- no double-submit

## Gate B — Esc / Pause

During active Dialogue:

- press Esc
- normal Minecraft pause/exit menu opens
- Dialogue remains underneath where rendering permits
- Session node unchanged
- return to game
- same Dialogue resumes

## Gate C — Remapped Chat

Change Chat key to a non-default key such as Enter.

During Dialogue:

- open Chat with remapped binding
- type a normal message/command
- Chat owns input
- Dialogue does not advance
- close Chat
- Dialogue returns at same node

No literal-T dependency.

## Gate D — Inventory / Container / Other GUI

During Dialogue:

- open Inventory
- open container
- open at least one test/third-party GUI if available

Verify:

- Dialogue remains visually underneath
- upper GUI owns all clicks
- underlying Choice cannot be clicked
- close upper GUI
- Dialogue returns to foreground

## Gate E — Death / Respawn

During active Dialogue:

- kill player
- death screen fully usable
- respawn
- same Session/current node returns

## Gate F — Disconnect / Reconnect

During active Dialogue:

- disconnect
- reconnect
- current Session restores
- current line/choice restores
- login/chat GUI is not stolen by DGR

## Gate G — Task Instant Open

After initial Task sync:

- press Task key
- Task menu renders immediately
- no empty/loading stage
- no request-on-open dependency

Keep menu open while Objective changes:

- UI updates from server push
- no close/reopen required
- no one-second polling requirement

## Gate H — Actual Objective Transition Bug

Use user's audited Task resource where available.

Verify:

```text
消灭史莱姆
→ completes
→ 与酒馆老板对话 becomes ACTIVE
```

UI must show the new Objective.

Capture both server runtime state and client presentation state.

## Gate I — Task Visual Design

Verify at:

- normal resolution
- low GUI-scale resolution

Check:

- active Task list
- selected Task detail
- multiple ACTIVE Objectives
- readable progress
- no huge useless empty panel
- visual consistency with Dialogue

## Gate J — Copier

With enough templates to require scrolling:

- scroll works
- selection visible
- delete/confirm works
- style matches Nominator

## Gate K — Item Nominator

Verify:

- upper browser remains accepted
- inventory layout coherent
- nomination slot obvious
- actual bind still succeeds

## Gate L — Creator Labels

Verify names:

- 编辑器
- 指名器
- 复制器
- 收纳箱
- 素体
- 检查器

Verify labels:

```text
[ItemID] ...
[GroupID] ...
[NPCID] ...
```

---

# 15. 0.3.2.1 / B4 Non-Regression

Must continue to pass:

- Story Package load/reload
- full ID / Namespace behavior
- Entity Nominator global search
- Item Nominator global search
- package-scoped binding validation
- Creator Inspect persistence
- 检查器/goggles effective state
- Item matcher semantics
- Actor arbitration
- Story Chooser token/index validation
- Canonical Session continuation
- Canonical Choice validation
- Canonical Task persistence
- repeatable Story cleanup/restart
- ONCE Story terminal behavior
- Story/Task/Session saved-data integrity

0.3.2.2 fails if UI work breaks accepted runtime contracts.

---

# 16. Recommended Construction Order

```text
M0  Baseline / Freeze Guard
↓
M1  Dialogue + Choice visual/click polish
↓
M2  GUI layering / input priority
↓
M3  Esc / death / disconnect / reconnect
↓
M4  Task server-push + client-cache synchronization
↓
M5  Actual Task Objective transition bug
↓
M6  Task UI visual redesign
↓
M7  Copier UI rework
↓
M8  Item Nominator lower-layout polish
↓
M9  Creator names + Inspect label normalization
↓
M10 Automated full regression
↓
M11 Real-machine acceptance
↓
USER ACCEPTANCE
```

Do not split each milestone into excessive micro-approval loops.

Also do not implement M2/M3/M4/M5 as one uncontrolled cross-system rewrite.

---

# 17. Completion Criteria

0.3.2.2 may be marked technically complete only when:

```text
JAVA_BUILD=PASS
NEW_0.3.2.2_PROBES=PASS
0.3.2.1_REGRESSION=PASS
B4_RUNTIME_REGRESSION=PASS
DIALOGUE_LAYERING_GATES=PASS
SESSION_RECONNECT_GATE=PASS
TASK_PUSH_CACHE_GATE=PASS
TASK_REAL_OBJECTIVE_TRANSITION_GATE=PASS
REAL_MACHINE_UI_GATES=PASS
STUDIO_DIFF=0
FROZEN_SCHEMA_DIFF=0
```

Then status may become:

`AGENT_REAL_MACHINE_VERIFIED`

It must remain:

`0.3.2.2_USER_ACCEPTED=NO`

until the user personally audits the delivered build and explicitly accepts it.

---

# 18. Final Design Summary

## Dialogue

```text
Canonical Session ACTIVE
        │
        ├─ normal Dialogue foreground
        │
        ├─ Chat / Inventory / other GUI
        │      → Dialogue remains visually underneath
        │      → upper GUI owns input
        │
        ├─ Esc
        │      → normal Minecraft pause/menu
        │      → Session remains ACTIVE
        │
        ├─ Death
        │      → death screen owns foreground
        │      → respawn restores same Dialogue
        │
        ├─ Disconnect
        │      → server preserves Session
        │      → reconnect restores same node
        │
        └─ authored END node
               → Session actually completes
```

There is no player-facing mid-dialogue suspend/cancel system.

## Task

```text
Server Canonical Task
        │
        │ sole authority
        ↓
meaningful state change
        ↓
push full presentation snapshot
        ↓
Client read-only Task cache
        ↓
Task UI reads instantly
```

No normal pull-on-open loading.

No one-second polling loop.

## Tooling UI

- accepted Entity Nominator remains stable
- Item Nominator lower layout is polished
- Copier adopts the accepted dark tool style
- creator-tool names become concise
- Inspect labels become `[NPCID] / [ItemID] / [GroupID]`

0.3.2.2 remains a focused client-experience correction over the accepted Canonical runtime.
