# Phase 5 Testing

Install DarkGrey RPG 0.5.0, CustomNPC+ fixed-v1, and UniMixins. Keep the Live
Bridge on its default loopback address and port unless `32145` conflicts with
another local process.

## Automated checks

```powershell
$env:JAVA_HOME='E:\Java\jdk-21.0.7'
$env:GRADLE_USER_HOME='E:\Java\gradle-home-darkgrey'
.\gradlew.bat spotlessJavaCheck build phase5LiveProbe --offline --no-daemon `
  --no-configuration-cache --max-workers=2
.\scripts\verify-phase5.ps1
```

Run the Studio build/UI probe:

```powershell
$env:APPDATA=(Resolve-Path '.tooling\appdata').Path
$env:LOCALAPPDATA=(Resolve-Path '.tooling\localappdata').Path
$env:TEMP=(Resolve-Path '.tooling\temp').Path
$env:TMP=$env:TEMP
.\.tooling\godot-4.7.1\Godot_v4.7.1-stable_win64_console.exe `
  --headless --path studio --script res://tests/Phase5LiveBuildProbe.gd
```

With the dedicated server running, use `Phase5LiveRoundTripProbe.gd` to verify
the actual Studio client handshake and runtime snapshot.

## Live acceptance

1. Start Minecraft/server and Studio. Confirm the header says
   `Minecraft Connected`.
2. Confirm Project still shows local Actors, Dialogues, Quests, and Stories.
3. Join with a player and confirm Minecraft shows the player and bound live
   Actors.
4. Select the player in Debugger. Start and progress a Quest; verify Active,
   Completed, objective progress, Story instance, current/previous nodes,
   waiting event, variables, and conditions update.
5. Force a failed `QuestState` condition. Confirm Why Not Triggered shows its
   expected and actual values.
6. Open the Story graph and confirm Current, Executed, Waiting, and Error
   colors follow the selected player's state.

## Pick, Locate, and reload

1. Start each Pick mode in Studio.
2. Use the Editor Tool to select an Actor/entity, block position, two region
   corners, or a dropped item as instructed.
3. Confirm the result returns to Studio and fills a compatible selected Story
   node or Quest Objective.
4. Select an Actor in Studio and click Locate. Confirm temporary particles and
   player feedback appear at loaded bound NPC instances.
5. Save a resource with Ctrl+S. Confirm Live reports a successful reload
   without running `/dgrpg reload`.
6. Stop Studio, save another resource, and confirm offline authoring still
   works. Use `/dgrpg reload` as the fallback.

## Reversible Play Test

1. Give the test player known Quest progress and Story variables.
2. Select a Story and start node, then click `▶ Test`.
3. Confirm only the selected Story and referenced Quests are cleared and the
   Story begins at that node.
4. Change progress and variables during the test.
5. Click `■ Stop + Restore`.
6. Confirm the original Quest progress, variables, and Story instances return.
7. Repeat after reconnecting the player, and test orderly server shutdown with
   an active online test session.

## Content Pack

1. Choose Project → Build Content Pack and select an output parent directory.
2. Confirm the project-ID folder contains `project.json`, `actors`,
   `dialogues`, `quests`, `stories`, and `resources`.
3. Build over an existing output and confirm a timestamped backup is retained.
4. Point the production server's `project.directory` configuration at the
   built folder.
5. Start without Godot Studio installed or running. Confirm the runtime loads
   the Content Pack and normal players can complete the acceptance Story.

Automated checks cover build shape, atomic backup, protocol framing, real
Studio-to-server handshake, hot reload, snapshot fields, Java 8 packaging, and
dedicated-server startup. A real client is still required for visual Pick,
Locate, Graph highlight, and interactive Test/Stop acceptance.
