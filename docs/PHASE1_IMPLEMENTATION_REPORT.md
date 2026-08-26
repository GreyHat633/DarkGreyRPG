# Phase 1 implementation report

Version: 0.1.0  
Artifact: `build/libs/darkgrey_rpg-0.1.0.jar`  
SHA-256: `D5473078CF66E0707B7C4FDB9B4EA205F9186B6FEC886F397C987A119AC12D6A`

## Implemented

- Forge 1.7.10 runtime project and versioned Java 8 artifact.
- Strict `project.json` and standalone Actor JSON loading.
- Atomic runtime snapshot replacement: a failed reload keeps the last valid
  project.
- `/dgrpg status`, `/dgrpg reload`, and all Phase 1 Actor commands.
- Editor tool right-click interaction and look-target command interaction.
- CustomNPC+ compatibility isolated in `darkgrey.rpg.compat.customnpcs`.
- Actor ID stored through `EntityNPCInterface.wrappedNPC` and CNPC
  `getStoredData/setStoredData/removeStoredData`, using key
  `darkgrey_rpg.actor_id`.
- Godot 4 Studio shell with functional Actor create, duplicate, edit, delete,
  validate, and atomic save operations.
- Phase 2+ scope placeholders only; no Dialogue, Quest, Story, native NPC,
  boss, or combat implementation.

## Verification

- `spotlessApply build phase1ProjectProbe`: passed.
- Project load, Actor scope guard, and failed-reload rollback probes: passed.
- Phase 1 static command, stored-data, Studio, and scope checks: passed.
- Godot 4.7.1 headless scene startup: passed with no script errors.
- Godot Actor save/reload and Actor scope probes: passed.
- Reobfuscated JAR archive check: passed; 37 entries and no shaded CNPC
  classes.
- Main class bytecode version: 52 (Java 8).
- Bytecode inspection confirms the final JAR calls CNPC `wrappedNPC`,
  `getStoredData`, `setStoredData`, `removeStoredData`, and `updateClient`.
- Isolated Forge 10.13.4.1614 dedicated server reached `Done`; Forge loaded
  CustomNPC+ 1.11.1, UniMixins 0.3.1, DarkGrey_RPG 0.1.0, and the one-Actor
  server probe project.

The development server required the RFG-deobfuscated CNPC JAR in `run/server`
because ForgeGradle is a deobfuscated environment. Production servers should
use the original `libs/CustomNPC-Plus-1.11.1-fixed-v1.jar`.

## Remaining live acceptance boundary

No real player/client joined the isolated server, so the following plan items
still require the manual world procedure in `PHASE1_TESTING.md`:

- use the tool on an existing CNPC and visually confirm the binding;
- inspect the right-click information in the client;
- save the world, fully restart it, and confirm the same NPC still exposes the
  Actor ID.

These are not claimed as complete from build, bytecode, or dedicated-server
startup evidence alone.
