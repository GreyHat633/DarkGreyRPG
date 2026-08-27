# DarkGrey_RPG

DarkGrey_RPG is an authoring framework for Minecraft 1.7.10 RPG actors,
dialogues, quests, and stories. The current `0.5.x` line implements Phase 1
through Phase 5 of the master plan plus the Windows WPF Studio 2.1 workflow:

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
- a self-contained WPF Studio 2.1.3 with Story-first navigation, formal
  Create/Import/Reference semantics, dedicated Dialogue and Quest editors,
  a freely draggable/connected Story Flow canvas, and a derived read-only
  Project Story Graph;
- Runtime StoryStart, DialogueExitBranch, EnterStory, and EndStory support,
  including named Dialogue exits and `/dgrpg story start <id>`.

Native NPC, boss, combat, cutscene, and custom animation systems remain
intentionally outside this project. Dialogue still never starts or owns a
Quest, and a Quest has no issuer, Dialogue, or Story ownership fields.

## Requirements

- Minecraft 1.7.10
- Forge 10.13.4.1614
- CustomNPC+ 1.11.1 fixed-v1
- UniMixins 1.7.10 0.3.1 (required by the supplied CustomNPC+ environment)
- Java 8 to run Minecraft; JDK 21 or newer to run this Gradle build
- .NET 10 SDK for WPF Studio source builds (the packaged EXE is self-contained)
- Godot 4.x only for the legacy Phase 1–5 external Studio

## Build

PowerShell:

```powershell
$env:GRADLE_USER_HOME='E:\Java\gradle-home-darkgrey'
.\gradlew.bat build --offline --no-daemon --no-configuration-cache
```

The mod jar is written to `build/libs/darkgrey_rpg-0.5.0.jar`.

## WPF Studio 2.1.3

Run `Studio/package-studio.ps1` to publish the self-contained Windows x64
single-file application to `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`.
Open or create a Project from the File menu, enter a Story, author its Actor,
Dialogue, Quest, and Flow pages, then use the Project Story Graph for derived
cross-Story navigation. Create and Duplicate use unsaved Drafts until the
first explicit Save; Reference keeps the shared project resource and its Home
Story. `docs/2.1.3_RESOURCE_CREATION_MODEL.md` and
`docs/2.1.3_STORY_RESOURCE_LIBRARY.md` define these contracts;
`docs/TESTING.md` contains the separate Core, WPF, Release UI, Runtime, and
manual acceptance boundaries.

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

## Story test

1. Use `examples/phase4_project` as `darkgrey_rpg_project`.
2. Bind the `tavern_owner` Actor to an existing CustomNPC+ NPC.
3. Talk to the owner, accept `tavern_offer`, and kill ten Slimes.
4. Return to the owner, finish `tavern_complete`, and confirm the reward is
   exactly 50 XP and three Emeralds.
5. Interact again and confirm the reward cannot be claimed twice.

See `docs/PHASE4_TESTING.md` for the complete Studio, runtime, restart, and
multiplayer procedure.

## Live authoring

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
