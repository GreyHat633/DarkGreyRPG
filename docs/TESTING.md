# DarkGrey RPG Studio 2.1 Testing

## Automated gates

Run from the repository root with the repository E-drive caches:

```powershell
$env:DOTNET_CLI_HOME="$pwd\.tooling\wpf-build"
$env:NUGET_PACKAGES="$pwd\.tooling\wpf-build\nuget-packages"
& 'E:\Java\dotnet-sdk-10\dotnet.exe' test .\Studio\DarkGreyRPG.Studio.sln -c Release --no-restore

$env:GRADLE_USER_HOME='E:\Java\gradle-home-darkgrey'
$env:JAVA_HOME='E:\Java\jdk1.8.0_471'
.\gradlew.bat build studio21VerticalSliceProbe studio21RegressionProbe studio21ActorBindingProbe --offline --no-daemon --console=plain
```

Accepted on 2026-08-26:

- Studio Core: 56/56 passed.
- Studio WPF: 77/77 passed.
- Gradle `build`: passed, including Spotless and JAR assembly.
- 2.1 vertical slice: both `hand_over -> kingdom_route` and
  `conceal -> empire_route` passed, including StoryInstance snapshot restore.
- Legacy Runtime regression: 20 project/dialogue/quest/story/live probes passed.
- CustomNPC+ binding: stored-data write/read, wrapper recreation, removal, and
  client-update calls passed.

The regression task deliberately loads one invalid Actor to prove failed reload
rollback. Its error log is expected; `RUNTIME_FAILED_RELOAD_ROLLBACK=PASS` is
the acceptance result.

## Real WPF acceptance

Scripts:

- `.tooling/2.1-acceptance/m6-ui-acceptance.ps1`
- `.tooling/2.1-acceptance/m7-ui-acceptance.ps1`
- `.tooling/2.1-acceptance/final-ui-acceptance.ps1`

The recorded runs cover free node drag, port connection, selection,
copy/paste/delete, undo/redo, pan/zoom, save and restart, derived graph layout,
search/filter/diagnostics, direct Flow navigation, project auto-restore, all
five Story pages, shared-reference synchronization, import independence,
Bottom Dock resize/collapse/restart, UI Automation names, and clipping bounds.
Screenshots are under `.tooling/2.1-acceptance/screenshots/`.

The final M7 UI script uses the valid acceptance project and checks its isolated
Story warning. Missing-target diagnostics are exercised by automated
repository/ViewModel tests, so the Runtime acceptance project is not left with
an intentionally broken EnterStory target.

## Real Forge acceptance

A Forge 1.7.10 dedicated server with CustomNPC+ and UniMixins loaded the final
acceptance project as 2 Actors, 1 Dialogue, 1 Quest, and 4 Stories. The server
reached ready state; `/dgrpg reload` succeeded; Story list/info reported
`royal_mystery` with 8 nodes and 7 connections; shutdown was clean.

The 2.1 M8 Gate requires at least the data-layer branch vertical slice, and that
is complete. No interactive Minecraft client/player click-through of both
Dialogue choices was recorded in this acceptance run; branch execution is
proved by the registered Runtime executors and the two deterministic probes,
not presented as client-visual evidence.

## Release verification

Package Studio with `Studio/package-studio.ps1`, then verify the single-file
EXE version and SHA-256. Runtime artifacts are generated under `build/libs/`.
The final handoff records artifact paths and hashes.
