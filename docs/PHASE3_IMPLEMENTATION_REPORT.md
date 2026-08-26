# Phase 3 Implementation Report

Version: `0.3.0`

Artifact:

- `build/libs/darkgrey_rpg-0.3.0.jar`
- SHA-256:
  `CFC34847DBB12E83432855ECDDE60696CB616E054913A137C8533BFD37709D97`
- Size: 107,113 bytes
- JAR entries: 104
- Java class major version: 52 (Java 8)
- Shaded `noppes/*` entries: 0

## Implemented

### Quest resources

- Strict, independent `QuestDefinition` loading.
- Top-level fields: `id`, `title`, `description`, `objectives`,
  `objective_groups`, and `metadata`.
- Unknown fields and duplicate resource/objective/group IDs are rejected.
- Failed reload keeps the last valid snapshot.
- Every Objective must belong to exactly one group.
- A Quest cannot own an issuer, Dialogue, or Story.
- `InteractActor.actor_id` is an intrinsic target parameter for that Objective;
  it is not an issuer or resource-ownership relationship.

### Objectives and groups

- `KillEntity`
- `CollectItem`
- `ReachLocation`
- `InteractActor`
- `ALL`
- `ANY`
- `SEQUENCE`, including ordered eligibility

### Runtime

- One `QuestEventAdapter` translates Forge/FML events into Quest events.
- Kill tracking uses `LivingDeathEvent`.
- Collection tracking uses `EntityItemPickupEvent`.
- Location tracking uses a throttled `PlayerTickEvent`.
- Actor interaction tracking uses `EntityInteractEvent` and the existing CNPC
  Actor binding.
- Quest progress is server authoritative.
- `PlayerQuestData` is `WorldSavedData`.
- Player records are keyed by UUID and stored in one per-world ledger.
- Active, Completed, and reserved Failed states are supported.

### Journal and commands

- Independent Minecraft `GuiQuestJournal`.
- Active and Completed views.
- Long text wrapping, objective progress, scrolling, and mouse controls.
- Journal snapshot is sent from the server.
- Normal players request their own Journal with the default `J` key.
- All S2C discriminators are registered on both physical sides so the
  dedicated server has outbound codec mappings.
- Commands:
  - `/dgrpg quest list`
  - `/dgrpg quest info <id>`
  - `/dgrpg quest start <id>`
  - `/dgrpg quest journal`
  - `/dgrpg quest progress`
  - `/dgrpg quest reset <id>`

The commands are operator test entrances. Phase 4 Story nodes will
be the normal authoring connection for starting Quests.

### Studio

- Quests are a first-class Project Tree section.
- Dedicated Quest Editor window.
- All four Objective Inspectors.
- ALL, ANY, and SEQUENCE group editing.
- Drag reorder, move, duplicate, delete, Undo, Redo, and Ctrl+S.
- Atomic save and strict validation.
- `schema/quest.schema.json`.
- Phase 3 example with `kill_10_slimes` and `objective_showcase`.

## Automated evidence

Gradle `build phase3QuestProbe`:

- `RUNTIME_PROJECT_LOAD_PROBE=PASS`
- `RUNTIME_DIALOGUE_LOAD_PROBE=PASS`
- `RUNTIME_DIALOGUE_RESULT_PROBE=PASS`
- `DIALOGUE_NETWORK_CODEC_PROBE=PASS`
- `RUNTIME_QUEST_LOAD_PROBE=PASS`
- `RUNTIME_QUEST_GROUP_PROBE=PASS`
- `PLAYER_QUEST_NBT_PROBE=PASS`
- `PLAYER_QUEST_ISOLATION_PROBE=PASS`
- `QUEST_JOURNAL_CODEC_PROBE=PASS`

`scripts/verify-phase3.ps1`:

- four Objective types: PASS
- three group modes: PASS
- Quest scope guard: PASS
- unified event adapter markers: PASS
- UUID persistence markers: PASS
- Journal markers: PASS
- common S2C registration: PASS
- Studio Quest UX markers: PASS

Godot 4.7.1 headless:

- `STUDIO_QUEST_SAVE_RELOAD_PROBE=PASS`
- `STUDIO_QUEST_OBJECTIVE_TYPES=PASS`
- `STUDIO_QUEST_GROUP_MODES=PASS`
- `STUDIO_QUEST_SCOPE_GUARD=PASS`
- `STUDIO_QUEST_EDITOR_RENDER_PROBE=PASS`

Dedicated Forge 1.7.10 server:

- DarkGrey RPG `0.3.0` discovered.
- Project `Phase 3 Server Probe` loaded with one Actor, one Dialogue, and one
  Quest.
- Phase 3 runtime initialized.
- Server reached `Done (0.774s)`.
- The server was intentionally terminated after the boot assertion.
- Forge's background version checker emitted an unrelated JSON parse trace;
  no DarkGrey RPG project/runtime error was logged.

## Verification boundary

No real Minecraft client or second player joined during automated testing.
Therefore these remain manual acceptance items:

- actual Slime kills entering through Forge and displaying 9/10 then 10/10;
- save, full server restart, and player rejoin persistence;
- two live players observing independent progress;
- real Journal rendering, scrolling, tab switching, and S2C delivery.

The pure evaluator, NBT round-trip, two-UUID ledger, packet codec, Studio
render, JAR, and dedicated-server boot paths passed. They are not presented as
a substitute for the live multiplayer checks in `PHASE3_TESTING.md`.

## Scope stop

Phase 4 was not implemented. There is no Story graph, StartQuest Story node,
reward orchestration, Live Bridge, debugger, native NPC, boss, or combat
framework in `0.3.0`.
