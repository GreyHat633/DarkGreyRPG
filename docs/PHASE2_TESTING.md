# Phase 2 acceptance test

## Automated checks

```powershell
$env:GRADLE_USER_HOME='E:\Java\gradle-home-darkgrey'
.\gradlew.bat build phase2DialogueProbe --offline --no-daemon --no-configuration-cache
.\scripts\verify-phase2.ps1
```

Run the Godot probes with the project-local portable Godot executable:

```powershell
.\.tooling\godot-4.7.1\Godot_v4.7.1-stable_win64_console.exe `
  --headless --path .\studio --script res://tests/Phase2DialogueProbe.gd
```

## Studio manual UX

1. Open `studio/project.godot`.
2. Open `examples/phase2_project`.
3. Select `Dialogues > 史莱姆委托`.
4. Confirm all eight nodes are visible together without opening one window per
   line.
5. Exercise add, drag reorder, duplicate, delete, Ctrl+Z, Ctrl+Y, search,
   replace, Speaker picker, and node target picker.
6. Edit the Choice option list and save with Ctrl+S.
7. Reopen the project and confirm the Dialogue is unchanged.
8. Confirm adding `StartQuest` or another unknown field is rejected.

## Minecraft live playback

1. Install DarkGrey_RPG 0.2.0, CustomNPC+ fixed-v1, and UniMixins 0.3.1.
2. Use `examples/phase2_project` as `darkgrey_rpg_project`.
3. Join the world and run `/dgrpg dialogue play tavern_offer`.
4. Confirm the independent DarkGrey RPG GUI displays the speaker and long
   wrapped text, and that the mouse wheel scrolls.
5. Continue to the Choice and test mouse and number-key selection.
6. Take the “more” branch and confirm Jump returns to the Choice.
7. Take the accept branch and run `/dgrpg dialogue last-result`; it must report
   `tavern_offer -> accept`.
8. Replay and take refuse; it must report `tavern_offer -> refuse`.
9. Confirm no CNPC Dialogue GUI or Quest GUI was used.

The automated flow probe validates both the Jump loop and the `accept` Result.
The live client procedure remains necessary to prove rendering and input in an
actual Minecraft client.
