# Phase 3 Testing

Use `examples/phase3_project` as the server's `darkgrey_rpg_project`.
Install DarkGrey RPG 0.3.0, CustomNPC+ fixed-v1, and UniMixins.

## Automated checks

```powershell
$env:JAVA_HOME='E:\Java\jdk-25.0.1'
$env:GRADLE_USER_HOME='E:\Java\gradle-home-darkgrey'
.\gradlew.bat build phase3QuestProbe --offline --no-daemon --no-configuration-cache --max-workers=2
.\scripts\verify-phase3.ps1
```

Run the Godot probe with its data directories redirected to `.tooling`:

```powershell
$env:APPDATA=(Resolve-Path '.tooling\appdata').Path
$env:LOCALAPPDATA=(Resolve-Path '.tooling\localappdata').Path
$env:TEMP=(Resolve-Path '.tooling\temp').Path
$env:TMP=$env:TEMP
.\.tooling\godot-4.7.1\Godot_v4.7.1-stable_win64_console.exe `
  --headless --path studio --script res://tests/Phase3QuestProbe.gd
```

## Studio acceptance

1. Open `studio/project.godot`.
2. Confirm Actors, Dialogues, and Quests are separate Project Tree sections.
3. Open `objective_showcase`.
4. Add each Objective type, edit its Inspector, drag it to another position,
   duplicate it, delete it, Undo, and Redo.
5. Add ALL, ANY, and SEQUENCE groups and edit their ordered Objective ID lists.
6. Save, close, and reopen the Quest. Confirm no data was lost.
7. Try adding a top-level `issuerNpc`, `dialogue`, or `story` field through a
   copied JSON file. Confirm validation rejects it.

## Minecraft acceptance

1. Run `/dgrpg quest list`.
2. Run `/dgrpg quest start kill_10_slimes`.
3. Kill nine Slimes. `/dgrpg quest progress` must display `9/10` and ACTIVE.
4. Kill the tenth Slime. It must display `10/10` and COMPLETED.
5. Press `J` as a normal player. Confirm Active and Completed tabs, wrapped
   text, objective progress, scrolling, and the Close button.
   `/dgrpg quest journal` is an operator test fallback.
6. Leave the server, restart it, and rejoin. Confirm `10/10` remains completed.
7. Run `/dgrpg quest reset kill_10_slimes` only when intentionally repeating
   the test.

## Two-player isolation

1. Start `kill_10_slimes` for two different players.
2. Player A kills three Slimes; Player B kills one.
3. Each player runs `/dgrpg quest progress`.
4. Confirm A sees `3/10` and B sees `1/10`.
5. Restart the server and confirm both values persist independently.

Automated probes cover resource validation, group evaluation, 10/10 completion,
NBT round-trip, two UUIDs in one persisted ledger, and Journal packet codecs.
They do not replace the real client/server steps above.
