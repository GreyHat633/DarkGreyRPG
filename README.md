# DarkGrey_RPG

DarkGrey_RPG is a flow-first authoring framework for Minecraft 1.7.10 RPG
stories. Version `0.3.0.0` establishes one canonical model from the Windows WPF
Studio through JSON persistence to the Forge runtime:

- Story Flow is the only editable runtime orchestration source;
- Session graphs own dialogue lines, choices, conditions, and named results;
- Task graphs own objectives, logic, progress, and single settlement;
- Project Story Graph is derived and read-only;
- flow (`●`) and logic (`◆`) ports share stable IDs and strict cardinality rules;
- Story, Session, and Task instances persist across server save/restart;
- Objective events, Actions, terminate, failure cleanup, and active-child cleanup
  form a complete automated runtime vertical slice;
- legacy Studio 2.1.3 Dialogue, Quest, and safe Story patterns migrate only
  through an explicit preview/confirm transaction with full backup and rollback;
- the WPF Studio provides the shared graph editor, resource tree, Inspector,
  Story workspace, migration UI, and local authoring lifecycle;
- Forge 1.7.10 dedicated-server and Minecraft client compatibility are retained.

CustomNPC+ remains a compatibility adapter and runtime test host in `0.3.0.0`.
CNPC-specific binding polish, final NPC gameplay experience, story-pack
deployment, and final quest UI are intentionally deferred to `0.3.1.0`.

The repository also retains the earlier Phase 1–5 capabilities and compatibility
surfaces, including:

- standalone Actor resources;
- runtime project loading and `/dgrpg reload`;
- binding an existing CustomNPC+ NPC to an Actor through CNPC stored data;
- a Godot 4 Studio shell with functional Actor and Dialogue editors;
- independent Dialogue resources with Line, Choice, Jump, and End nodes;
- a server-authoritative Minecraft Dialogue UI and named Results.
- independent Quest resources with KillEntity, CollectItem, ReachLocation, and
  InteractActor Objectives;
- ALL, ANY, and SEQUENCE Objective Groups;
- persistent, UUID-isolated player Quest progress;
- a server-authoritative Quest Journal with Active and Completed views;
- independent Story graph resources connecting Actors, Dialogues, Quests,
  triggers, conditions, flow, and rewards;
- a registry-driven, server-authoritative Story runtime with persistent
  per-player Story variables;
- a Godot GraphEdit Story editor with node search, connections, Inspector,
  Undo/Redo, Copy/Paste, validation, and problem-to-node navigation;
- a loopback-only JSON Lines Live Bridge between Studio and Minecraft;
- Project/Live runtime views, Quest progress, Story Graph highlighting,
  variables, conditions, and Why Not Triggered explanations;
- Minecraft Pick/Locate, automatic save-triggered reload, reversible Play
  Test sessions, autosave/backups, and Content Pack builds.
- a self-contained WPF Studio with Story-first navigation, formal
  Create/Import/Reference semantics, dedicated Dialogue and Quest editors,
  a freely draggable/connected Story Flow canvas, and a derived read-only
  Project Story Graph;
- Runtime StoryStart, DialogueExitBranch, EnterStory, and EndStory support,
  including named Dialogue exits and `/dgrpg story start <id>`.

Native NPC, boss, combat, cutscene, and custom animation systems remain
intentionally outside this project. Session and Task internal sequencing remain
separate from Story Flow; `0.3.0.0` does not add implicit concurrency, Task
entry/return nodes, or Task-embedded Sessions.

## Requirements

- Minecraft 1.7.10
- Forge 10.13.4.1614
- CustomNPC+ 1.11.1 fixed-v1
- UniMixins 1.7.10 0.3.1 (required by the supplied CustomNPC+ environment)
- Java 8 for Forge/Minecraft runtime and the validated Gradle build
- .NET 10 SDK for WPF Studio source builds (the packaged EXE is self-contained)
- Godot 4.x only for the legacy Phase 1–5 external Studio

## Build

PowerShell:

```powershell
$env:JAVA_HOME='E:\Java\jdk1.8.0_471'
$env:GRADLE_USER_HOME='E:\Java\gradle'
.\gradlew.bat build --offline --no-daemon --no-configuration-cache
```

The mod jar is written to `build/libs/darkgrey_rpg-0.3.0.0.jar`.

## WPF Studio 0.3.0.0

Run `Studio/package-studio.ps1` to publish the self-contained Windows x64
single-file application to `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`.
Open or create a Project from the File menu, enter a Story, and author canonical
Session, Task, and Story Flow graphs from the shared Story workspace. The
Project Story Graph remains derived navigation. Existing 2.1.3 projects are
migrated from Project → Migrate to Canonical; preview is read-only, Apply
requires explicit confirmation, and legacy files are never silently replaced.
See `PLAN/DarkGrey_RPG_0.3.0.0_Design_Plan.md` and
`docs/0.3.0.0_ARCHITECTURE.md` for current contracts. The `docs/2.1.3_*`
documents remain the legacy migration-source specification.

## First run

1. Copy `examples/phase1_project` to the Minecraft working directory as
   `darkgrey_rpg_project`.
2. Install DarkGrey_RPG, CustomNPC+, and UniMixins in `mods`.
3. Start the world and run `/dgrpg status`, then `/dgrpg actor list`.
4. Run `/dgrpg actor select tavern_owner`.
5. Hold the DarkGrey RPG Editor Tool and right-click an existing CustomNPC+
   NPC. The selected Actor ID is written to CNPC stored data under
   `darkgrey_rpg.actor_id`.
6. Save and reopen the world, then hold the tool and sneak-right-click the NPC
   to inspect the persisted binding.

See `docs/PHASE1_TESTING.md` for the full acceptance procedure.

## Dialogue test

1. Use `examples/phase2_project` as `darkgrey_rpg_project`.
2. Run `/dgrpg dialogue list`.
3. Run `/dgrpg dialogue play tavern_offer`.
4. Advance with Enter/Space or the Continue button and choose an option with
   the mouse or number keys.
5. Run `/dgrpg dialogue last-result` and confirm `accept` or `refuse`.

## Quest test

1. Use `examples/phase3_project` as `darkgrey_rpg_project`.
2. Run `/dgrpg quest list`, then `/dgrpg quest start kill_10_slimes`.
3. Kill ten Slimes and run `/dgrpg quest progress`.
4. Press `J` to open the independent in-game Journal (`/dgrpg quest journal`
   remains an operator test fallback).
5. Leave and rejoin the world to confirm that the completed progress persists.

See `docs/PHASE3_TESTING.md` for persistence and two-player acceptance tests.

## Legacy Story compatibility test

This older CustomNPC+ scenario is retained as compatibility evidence and a
runtime test host. It is not the final `0.3.0.0` NPC or quest-UI product form.

1. Use `examples/phase4_project` as `darkgrey_rpg_project`.
2. Bind the `tavern_owner` Actor to an existing CustomNPC+ NPC.
3. Talk to the owner, accept `tavern_offer`, and kill ten Slimes.
4. Return to the owner, finish `tavern_complete`, and confirm the reward is
   exactly 50 XP and three Emeralds.
5. Interact again and confirm the reward cannot be claimed twice.

See `docs/PHASE4_TESTING.md` for the complete Studio, runtime, restart, and
multiplayer procedure.

## Legacy Phase 5 live authoring

1. Start Minecraft or the dedicated server with Live Bridge enabled.
2. Open `studio/project.godot`. Studio connects only to
   `127.0.0.1:32145`; offline editing remains available.
3. Select an online player in Debugger to inspect Quest progress, Story
   instances, variables, and condition explanations.
4. Use the Minecraft tab for Actor/Position/Region/Item/Entity Type Pick and
   Actor Locate.
5. Saving a resource sends a hot-reload notification. `/dgrpg reload` remains
   the fallback.
6. Use `▶ Test` and `■ Stop + Restore` for reversible Story testing.
7. Use Project → Build Content Pack to publish the standalone runtime folder.

The production server and normal players need the mod and Content Pack, not
Godot Studio. See `docs/PHASE5_TESTING.md` and `docs/LIVE_PROTOCOL.md`.
