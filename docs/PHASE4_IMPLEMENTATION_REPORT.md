# Phase 4 Implementation Report

Version: `0.4.0`

Artifact:

- `build/libs/darkgrey_rpg-0.4.0.jar`
- SHA-256:
  `86A75D5E313B937DF2390287707E01915F2373DF1194C3E73E24D686E077C7C0`
- Size: 163,930 bytes
- JAR entries: 149
- Java class major version: 52 (Java 8)
- Story runtime class entries: 35
- Shaded Gson entries: 0
- Shaded Guava entries: 0

Older 0.1.0, 0.2.0, and 0.3.0 artifacts remain in `build/libs`.

## Implemented

### Story resources

- Independent `StoryDefinition` resources with `id`, `title`, `entry`,
  `nodes`, `connections`, and `metadata`.
- Strict UTF-8 loading from the project's `stories` directory.
- Atomic project snapshot reload and rollback on invalid Story data.
- Reference checks against independent Actor, Dialogue, and Quest resources.
- Graph checks for duplicate IDs and outputs, missing references, invalid
  connection outputs, unconnected or unreachable nodes, conditions without
  both exits, and graphs without an End node.
- `schema/story.schema.json`.

### Initial node set

All 16 planned node types have registered executors:

- triggers: InteractActor, EnterRegion, QuestCompleted;
- Dialogue: PlayDialogue;
- Quest: StartQuest, CompleteQuest;
- flow: Branch, Sequence;
- conditions: QuestState, HasItem, VariableCompare;
- actions: GiveItem, GiveXP, SendMessage, SetVariable;
- End.

The runtime dispatches through `StoryNodeExecutorRegistry`; it does not use a
single node-type switch.

### Runtime integration

- `StoryInstance`, `StoryState`, `StoryNodeExecutor`, and `StoryEventBus`.
- A queued event bus prevents Dialogue-result or Quest-completion callbacks
  from re-entering the current dispatch loop.
- Forge/FML adapters provide Actor-interaction and throttled region events.
- Dialogue Result and Quest completion listeners feed Story events.
- Bound Story Actor interactions cancel the underlying CustomNPC+ interaction
  path, so the authored flow uses the DarkGrey Dialogue UI rather than the
  CustomNPC+ Dialogue or Quest GUI.
- Story variables use per-world `WorldSavedData`, keyed by player UUID and
  Story ID.
- GiveItem handles full inventories by dropping only the remainder.
- `/dgrpg reload` clears transient Story instances after a successful
  definition reload while leaving persistent Story variables intact.
- Test commands:
  - `/dgrpg story list`
  - `/dgrpg story info <id>`
  - `/dgrpg story state <id>`
  - `/dgrpg story reset <id>`

### Studio Story editor

- Stories are a first-class Project Tree section.
- Godot `GraphEdit` canvas with pan, zoom, minimap, node drag, connections, and
  built-in box selection.
- All 16 initial node types in Add Node search.
- Find Next search across node IDs, types, and properties.
- Duplicate, Copy/Paste, Delete, Undo/Redo, and keyboard shortcuts.
- Inspector editing with Actor, Dialogue, and Quest resource pickers.
- Entry-node selection and atomic Story saving.
- Problems for missing or broken references, unconnected nodes, no exit,
  invalid connections, and duplicate IDs.
- Double-clicking a node-scoped problem selects and centers the GraphNode.

### Acceptance content

`examples/phase4_project` contains separate resources:

- Actor: `tavern_owner`
- Dialogues: `tavern_offer`, `tavern_complete`
- Quest: `kill_10_slimes`
- Story: `tavern_slime_request`

The reviewed 20-node, 20-connection Story performs:

1. owner interaction;
2. NOT_STARTED check and offer Dialogue;
3. Accept Result and Quest start;
4. QuestCompleted trigger after ten Slime kills;
5. return interaction with the owner;
6. completion Dialogue;
7. 50 XP and three Emerald rewards;
8. persistent `reward_claimed` guard against duplicate rewards.

The graph also has recovery routes for an already-active or already-completed
Quest after a server restart. Actor, Dialogues, and Quest contain no ownership
fields that couple them to this Story.

## Automated evidence

Gradle `spotlessJavaCheck build phase4StoryProbe`:

- `RUNTIME_PROJECT_LOAD_PROBE=PASS`
- `RUNTIME_ACTOR_SCOPE_GUARD=PASS`
- `RUNTIME_FAILED_RELOAD_ROLLBACK=PASS`
- `RUNTIME_DIALOGUE_LOAD_PROBE=PASS`
- `RUNTIME_DIALOGUE_RESULT_PROBE=PASS`
- `DIALOGUE_NETWORK_CODEC_PROBE=PASS`
- `RUNTIME_QUEST_LOAD_PROBE=PASS`
- `RUNTIME_QUEST_GROUP_PROBE=PASS`
- `PLAYER_QUEST_NBT_PROBE=PASS`
- `PLAYER_QUEST_ISOLATION_PROBE=PASS`
- `QUEST_JOURNAL_CODEC_PROBE=PASS`
- `RUNTIME_STORY_LOAD_PROBE=PASS`
- `STORY_EXECUTOR_REGISTRY_PROBE=PASS`
- `STORY_VARIABLE_NBT_PROBE=PASS`
- `PHASE4_ACCEPTANCE_GRAPH_PROBE=PASS`

`scripts/verify-phase4.ps1`:

- acceptance Story graph and resource independence: PASS;
- all 16 node types and executor registrations: PASS;
- no giant runtime switch: PASS;
- Story event integration: PASS;
- CustomNPC+ interaction GUI suppression: PASS;
- Studio Problems and Graph UX markers: PASS.

Godot 4.7.1 headless:

- `STUDIO_STORY_SAVE_RELOAD_PROBE=PASS`
- `STUDIO_STORY_REFERENCE_PROBLEMS=PASS`
- `STUDIO_STORY_UNCONNECTED_PROBLEMS=PASS`
- `STUDIO_STORY_GRAPH_RENDER_PROBE=PASS`
- `STUDIO_STORY_NODE_SEARCH_PROBE=PASS`

Dedicated Forge 1.7.10 server:

- Forge discovered DarkGrey RPG `0.4.0`.
- Project `Phase 4 Server Probe` loaded with one Actor, one Dialogue, one
  Quest, and one Story.
- Phase 4 runtime initialized.
- Server reached `Done (0.778s)`.
- No unexpected server exception was logged.
- The server was intentionally terminated after the boot assertions.

JAR audit:

- 149 readable archive entries.
- Story model and runtime classes are present.
- sampled mod entry and Story runtime classes are Java 8 bytecode.
- no Gson or Guava classes were shaded into the artifact.
- no Phase 5 Live Bridge or Debugger classes are present.

## Verification boundary

No real Minecraft client or second player joined during automated testing.
Therefore these remain manual acceptance items:

- a real bound CustomNPC+ Actor suppressing its normal interaction GUI;
- actual Dialogue rendering and Result delivery;
- ten live Slime kills feeding Quest and Story events;
- return interaction, XP/Item delivery, and inventory-full behavior;
- restart persistence and duplicate-reward prevention;
- two live players progressing and claiming independently;
- interactive Studio box selection, keyboard UX, and visual graph readability.

Automated checks prove compilation, strict resource loading, reviewed routes,
executor registration, variable NBT isolation, Studio save/render behavior,
JAR structure, and dedicated-server initialization. They are not presented as
a substitute for the live client and multiplayer checks in
`PHASE4_TESTING.md`.

## Scope stop

Phase 5 is not implemented. Version 0.4.0 contains no Live Bridge, TCP JSON
Lines transport, runtime debugger, remote Project/Live tree, or Pick in
Minecraft workflow. Native NPC, boss, and combat framework work also remains
outside the approved scope.
