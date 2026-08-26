# Phase 5 Implementation Report

Version: `0.5.0`

Artifact:

- `build/libs/darkgrey_rpg-0.5.0.jar`
- SHA-256:
  `0342C9EF9137DDE652C31E0E4EF862482C1F683360D4DD4350EE74DE374DF388`
- Size: 197,459 bytes
- JAR entries: 167
- Java class major version: 52 (Java 8)
- Live package class entries: 14
- Story model/runtime class entries: 44
- Shaded Gson entries: 0
- Shaded Guava entries: 0

Older 0.1.0 through 0.4.0 artifacts remain in `build/libs`.

Example Content Pack:

- `build/content-packs/darkgrey_rpg_phase5_example`
- Contains `project.json`, `actors`, `dialogues`, `quests`, `stories`, and
  `resources`.

## Implemented

### Local Live Bridge

- UTF-8 JSON Lines over TCP.
- Binds only to `127.0.0.1`; default port `32145`.
- Rejects non-loopback clients and lines larger than 1 MiB.
- Studio reconnects automatically and remains fully editable offline.
- Runtime-changing requests are queued through `MainThreadScheduler` before
  touching Minecraft, Quest, Story, or project state.
- Protocol hello reports protocol 1 and mod version.
- Configuration supports enabling/disabling the bridge and changing its port.

### Project / Live and Debugger

- Existing Project tree remains the local Actor, Dialogue, Quest, and Story
  view.
- Minecraft Live view contains loaded bound Actor entities and online players.
- Selected-player Debugger contains:
  - Active and Completed Quests;
  - objective progress;
  - Story instances and current nodes;
  - previous/executed nodes;
  - waiting state and errors;
  - per-Story variables;
  - per-node condition traces.
- `StoryDebugExplainer` records expected, actual, match result, and selected
  route for trigger and condition decisions.
- Why Not Triggered displays both the latest explanation and retained
  node-scoped explanations.
- Open Story graphs color Executed, Current, Waiting, and Error states from the
  selected player's live snapshot.

### Pick and Locate

- Pick Actor.
- Pick block Position.
- Pick Region from two opposite block corners.
- Pick Item from a dropped item entity.
- Pick Entity Type.
- Pick results return to Studio and fill compatible selected Story node or
  Quest Objective properties.
- Locate Selected Actor scans loaded bound NPCs, emits temporary particles, and
  sends player feedback.
- Pick actions use the existing DarkGrey RPG Editor Tool.

### Hot reload

- Actor, Dialogue, Quest, and Story saves notify the runtime automatically.
- Runtime reload remains project-atomic so cross-resource validation cannot
  expose a partial project.
- Failed reload keeps the last valid runtime snapshot.
- `/dgrpg reload` remains available as the offline/fallback path.

### Reversible Play Test

- Studio provides Story, start-node, player, `Test`, and `Stop + Restore`
  controls.
- Test start snapshots the player's Quest records, Story variables, and
  transient Story instances.
- It clears only Quests referenced by the selected Story and that Story's
  variables, then starts from the chosen node.
- Stop restores the original RPG snapshot.
- Reconnected players can stop a still-active test by UUID.
- Online test players are restored during orderly server shutdown.

### Content Build and data safety

- Project → Build Content Pack publishes a standalone runtime directory.
- Output includes `project.json` and all five planned directories.
- The output is staged and renamed into place.
- Rebuilding over an existing output retains a timestamped directory backup.
- Resource saves use a temporary file, atomic replacement, and retain the
  previous resource as `.bak`.
- Existing Actor, Dialogue, Quest, and Story editors autosave valid, already
  named resources after ten seconds.
- Studio itself is not required by the production server or normal players.

## Automated evidence

Gradle `spotlessJavaCheck build phase5LiveProbe`:

- all Phase 1–4 Java probes remained passing;
- `PLAY_TEST_SNAPSHOT_PROBE=PASS`;
- `STORY_DEBUG_TRACE_PROBE=PASS`;
- `LIVE_JSON_LINES_PROTOCOL_PROBE=PASS`;
- build, Spotless, Checkstyle, tests, sources JAR, and reobfuscation passed.

`scripts/verify-phase5.ps1`:

- loopback JSON Lines and line-size guard markers: PASS;
- main-thread command dispatch: PASS;
- Project/Live/Debugger snapshot fields: PASS;
- all five Pick kinds and Editor Tool integration: PASS;
- Locate and hot-reload paths: PASS;
- Why Not Triggered data: PASS;
- Play Test snapshot/restore: PASS;
- Content Pack shape: PASS;
- autosave, backup, and atomic-write paths: PASS;
- excluded future-system scope guard: PASS.

Godot 4.7.1 headless:

- Phase 1–4 Studio regressions: PASS;
- `STUDIO_CONTENT_PACK_BUILD_PROBE=PASS`;
- `STUDIO_CONTENT_PACK_RUNTIME_SHAPE=PASS`;
- `STUDIO_ATOMIC_BACKUP_PROBE=PASS`;
- `STUDIO_LIVE_DEBUGGER_UI_PROBE=PASS`;
- `STUDIO_STORY_GRAPH_LIVE_HIGHLIGHT_PROBE=PASS`;
- `STUDIO_PICK_TO_INSPECTOR_PROBE=PASS`.

Actual Studio-to-server round trip:

- `STUDIO_MINECRAFT_CONNECTED_PROBE=PASS`;
- `LIVE_JSON_LINES_ROUND_TRIP_PROBE=PASS`;
- protocol version 1 and mod version 0.5.0 received;
- `state.snapshot` resource counts received;
- `project.reload` returned success.

Content Pack server load:

- Studio built `build/phase5-content-pack`.
- Forge loaded it directly through `project.directory`.
- Runtime reported one Actor, two Dialogues, one Quest, and one Story.
- Server reached `Done`.

Final dedicated Forge 1.7.10 boot:

- Forge discovered DarkGrey RPG 0.5.0.
- `Phase 5 Server Probe` loaded one Actor, one Dialogue, one Quest, and one
  Story.
- Phase 5 runtime initialized.
- Live Bridge listened on `127.0.0.1:32145`.
- Server reached `Done (0.786s)`.
- Final protocol state and hot-reload requests succeeded.
- No DarkGrey RPG project, runtime, or Live Bridge exception was logged.
- The existing CustomNPC+ environment still logs its Scala script-compiler
  initialization warning; it did not prevent Forge or DarkGrey RPG from
  reaching `Done`, but CustomNPC+ Scala scripts were not validated here.
- Test server processes were intentionally stopped afterward.

## Verification boundary

No real Minecraft client/player joined the automated dedicated-server run.
Therefore the following remain manual acceptance items:

- visible Studio status during a normal interactive session;
- live Quest progress while killing actual entities;
- particle/HUD appearance for Locate;
- all five Editor Tool Pick interactions against real world targets;
- visually evaluating Graph highlight colors and readability;
- interactive Test/Stop with a real player's full RPG state;
- production deployment without Studio on a separate real server.

The actual TCP handshake, Studio client framing, state snapshot, hot reload,
Content Pack runtime load, pure snapshot restoration, build, JAR, and dedicated
server initialization were verified. They are not presented as substitutes
for the client-side checks in `PHASE5_TESTING.md`.

## Final scope

The original RPG authoring roadmap is now implemented through Phase 5.
Version 0.5.0 does not add Native NPC, custom entity rendering, complex AI,
Boss editors, combat timelines, projectile editors, cutscene cameras, or a
custom animation engine. Those systems remain excluded unless explicitly
requested in the future.
