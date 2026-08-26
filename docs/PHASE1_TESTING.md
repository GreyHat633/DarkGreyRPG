# Phase 1 acceptance test

This checklist separates build/static evidence from a real Minecraft runtime
test. A successful build does not prove world persistence.

## Build and content checks

- Run `scripts/verify-phase1.ps1`.
- Run the Gradle build shown in `README.md`.
- Confirm `build/libs/darkgrey_rpg-0.1.0.jar` exists.
- Confirm Actor JSON contains no Dialogue or Quest ownership fields.

## Studio

1. Open `studio/project.godot` in Godot 4.x.
2. Open `examples/phase1_project`.
3. Create an Actor, edit ID, Display Name, Notes, and Tags, then press Ctrl+S.
4. Reopen the project and confirm the Actor JSON loads.
5. Confirm Dialogues, Quests, and Stories show Phase placeholders only.

## Minecraft and CNPC+ binding

1. Install the three runtime mods listed in `README.md`.
2. Copy `examples/phase1_project` to the game working directory as
   `darkgrey_rpg_project`.
3. Enter a world with an existing CustomNPC+ NPC.
4. Run `/dgrpg status`, `/dgrpg reload`, and
   `/dgrpg actor info tavern_owner`.
5. Run `/dgrpg actor select tavern_owner`, hold the editor tool, and
   right-click the NPC.
6. Sneak-right-click with the tool and confirm it reports
   `Actor: tavern_owner`.
7. Run `/dgrpg actor unbind` while looking at the NPC, inspect it, and bind it
   again.
8. Save and quit the world completely, restart, then inspect the same NPC.
   The Actor ID must still be `tavern_owner`.

The final persistence step is the required live proof for CNPC stored-data
persistence. It cannot be replaced by compilation or archive inspection.
