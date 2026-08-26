# Phase 4 Testing

Use `examples/phase4_project` as the server's `darkgrey_rpg_project`.
Install DarkGrey RPG 0.4.0, CustomNPC+ fixed-v1, and UniMixins.

## Automated checks

```powershell
$env:JAVA_HOME='E:\Java\jdk-21.0.7'
$env:GRADLE_USER_HOME='E:\Java\gradle-home-darkgrey'
.\gradlew.bat build phase4StoryProbe --offline --no-daemon `
  --no-configuration-cache --max-workers=2
.\scripts\verify-phase4.ps1
```

Run the Godot probe with all writable data directories redirected to this
project's E-drive `.tooling` directory:

```powershell
$env:APPDATA=(Resolve-Path '.tooling\appdata').Path
$env:LOCALAPPDATA=(Resolve-Path '.tooling\localappdata').Path
$env:TEMP=(Resolve-Path '.tooling\temp').Path
$env:TMP=$env:TEMP
.\.tooling\godot-4.7.1\Godot_v4.7.1-stable_win64_console.exe `
  --headless --path studio --script res://tests/Phase4StoryProbe.gd
```

## Studio acceptance

1. Open `studio/project.godot`, then open `examples/phase4_project`.
2. Open `tavern_slime_request` under Stories.
3. Confirm the graph visibly separates the initial owner interaction, offer
   Dialogue, Quest start, Quest completion trigger, return interaction,
   completion Dialogue, and reward actions.
4. Pan, zoom, drag a node, box-select nodes, connect and disconnect ports, use
   Add Node search, Duplicate, Copy/Paste, Undo, and Redo.
5. Select Actor, Dialogue, and Quest nodes and confirm the Inspector uses
   resource pickers.
6. Temporarily select or type a missing resource, create an orphan node, or
   remove an exit. Confirm Problems reports the error and double-clicking the
   problem locates its node.
7. Save, close, and reopen the Story. Confirm positions, properties,
   connections, and entry were preserved.

The supplied acceptance graph was authored as an independent Story resource;
the Actor, both Dialogues, and Quest remain separate resources.

## Minecraft acceptance

1. Bind `tavern_owner` to an existing CustomNPC+ NPC with the editor tool.
2. Interact without holding the editor tool. Confirm `tavern_offer` opens in
   the DarkGrey Dialogue UI, not a CustomNPC+ Dialogue or Quest GUI.
3. Choose Refuse once. Confirm no Quest starts and interacting again can offer
   the Quest again.
4. Choose Accept. Confirm `kill_10_slimes` becomes ACTIVE.
5. Kill nine Slimes. Confirm the Quest remains ACTIVE and no reward is granted.
6. Kill the tenth Slime. Confirm the Story advances to its return-interaction
   trigger, but no reward is granted remotely.
7. Return to the same Actor and complete `tavern_complete`.
8. Confirm the player receives exactly 50 XP and three Emeralds.
9. Interact again. Confirm the Story reports that the reward was already
   claimed and grants nothing.
10. Restart the server and repeat the final interaction. Confirm the persisted
    per-player Story variable still prevents a duplicate reward.

## Multiplayer isolation

1. Have two players accept the Story independently.
2. Complete and claim the reward for Player A only.
3. Confirm Player B can still progress and claim once.
4. Restart the server and confirm neither player's `reward_claimed` state leaks
   into the other player's Story data.

Automated probes cover strict Story loading, the reviewed acceptance routes,
all 16 executor registrations, two-player Story-variable NBT round-trips,
Studio save/reload, reference and connectivity problems, and GraphNode
rendering. They do not replace the real client, two-player, or gameplay steps
above.
