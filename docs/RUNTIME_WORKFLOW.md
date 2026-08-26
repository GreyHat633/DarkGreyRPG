# DarkGrey RPG Runtime Workflow

This document is the bounded workflow for moving an Actor edit from Studio to
the Minecraft 1.7.10 runtime. Studio writes the same schema-1 Actor JSON that
the Java loader consumes; Minecraft remains the authority for the loaded
snapshot.

## Studio save, then Minecraft reload

1. Launch `DarkGreyRPGStudio.exe` (or run the WPF project during development)
   and open the target project directory.
2. Edit the Actor and press `Ctrl+S`. Save the project before asking Minecraft
   to reload it. The file must be `<actor id>.json` under `actors/` and contain
   `schema_version`, `id`, `display_name`, and optional `notes`/`tags` only.
3. In Minecraft, run `/dgrpg status` and confirm the displayed project path and
   CustomNPC+ status. Then run `/dgrpg reload`.
4. Run `/dgrpg status` again. A successful reload reports the project display
   name and Actor/Dialogue/Quest/Story counts. A failed reload reports an
   error and must not be treated as a published edit.
5. Check the resulting Actor with `/dgrpg actor info <actor_id>`. For a binding
   check, run `/dgrpg actor select <actor_id>`, look at the target CustomNPC+
   within 8 blocks, and use the editor tool (or `/dgrpg actor bind <actor_id>`).
   The runtime binding key is `darkgrey_rpg.actor_id`; `/dgrpg actor unbind`
   removes it from the looked-at NPC.

The Phase 1 WPF Studio does not claim a live bridge. The explicit
`/dgrpg reload` sequence above is the supported handoff. Do not edit the JSON
while a reload is in progress; save first, then issue one reload and inspect
its result.

## Bounded offline verification

Run from the repository root with the required build toolchain:

```powershell
$env:JAVA_HOME='E:\Java\jdk-25.0.1'
$env:GRADLE_USER_HOME='E:\Java\gradle-home-darkgrey'
.\gradlew.bat phase1ProjectProbe --no-daemon --console=plain
```

The probe creates `build/phase1-runtime-probe`, loads valid Actor JSON, checks
the dialogue/quest/story runtime probes, writes an unsupported Actor field,
and confirms that the failed reload leaves the previous valid snapshot active.
The observed output included:

```text
RUNTIME_PROJECT_LOAD_PROBE=PASS
RUNTIME_ACTOR_SCOPE_GUARD=PASS
RUNTIME_FAILED_RELOAD_ROLLBACK=PASS
RUNTIME_DIALOGUE_LOAD_PROBE=PASS
RUNTIME_QUEST_LOAD_PROBE=PASS
RUNTIME_STORY_LOAD_PROBE=PASS
LIVE_JSON_LINES_PROTOCOL_PROBE=PASS
PHASE4_ACCEPTANCE_GRAPH_PROBE=PASS
BUILD SUCCESSFUL
```

This is offline/static or JVM probe evidence. It does not prove that a real
Minecraft client/server, CustomNPC+ installation, Studio process, network
bridge, visual UI, or multiplayer session accepts the workflow. Those require
the live steps above and the acceptance scenarios in `docs/PHASE5_TESTING.md`.

## 2026-08-19 dedicated-server verification

The repository's existing `run/server` acceptance environment was started with
`gradlew runServer`. Forge 1.7.10 loaded 13 mods, including DarkGrey RPG and
CustomNPC+, opened the saved `phase1-runtime-world`, and reported `Done`.
The server console then verified:

```text
dgrpg status
CustomNPC+: loaded
dgrpg reload
Loaded 'Phase 5 Server Probe' with 1 actor(s) ...
dgrpg actor info tavern_owner
Actor: tavern_owner
Display Name: 酒馆老板
```

The server stopped cleanly and Gradle finished `BUILD SUCCESSFUL`. This is real
dedicated-server reload and lookup evidence. It still does not exercise a player
looking at a CustomNPC+ entity, so bind/unbind interaction and client rendering
remain explicit live-client acceptance items.
