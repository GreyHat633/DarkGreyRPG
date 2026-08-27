# DarkGrey RPG Studio 2.1.2 Testing

## Automated gates

Run from the repository root with the repository E-drive caches:

```powershell
$env:DOTNET_CLI_HOME="$pwd\.tooling\wpf-build"
$env:NUGET_PACKAGES="$pwd\.tooling\wpf-build\nuget-packages"
& 'E:\Java\dotnet-sdk-10\dotnet.exe' test .\Studio\DarkGreyRPG.Studio.sln -c Release --no-restore

$env:GRADLE_USER_HOME='E:\Java\gradle'
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

2.1.1 development gate on 2026-08-26:

- Studio Core: 56/56 passed.
- Studio WPF: 93/93 passed.
- New coverage includes shared viewport math, source-scoped Problems, Flow
  graph-coordinate actions, output replacement, Membership Warning versus
  missing-resource Error, explicit add-reference, invalid Flow route switching,
  Save/Discard/Cancel, SCC cycles, self-loops, and finite graph layout.
- Runtime build/probes remain the unchanged 0.5.0 contract and are rerun before
  packaging.
- The 2.1.1 Release WPF process passed inline-selector, middle-pan,
  cursor-centered zoom, real output-to-input drag connection, context-menu,
  compact graph, 1100x700 primary-canvas, and Story JSON hash-preservation
  checks. The drag-connected draft was terminated without saving before the
  graph checks resumed in a fresh process.

2.1.2 development gate on 2026-08-27:

- Studio Core: 77/77 passed.
- Studio WPF non-font-cache gate: 137/137 passed. Focused Shell, Story
  navigation, settings, and identity-dialog tests: 49/49 passed. The raw full
  run passed 139 tests and reported four known host font-cache URI
  initialization failures in WPF control-only tests; the Release application
  and those controls passed real-window automation.
- The added suites cover the complete node registry, endpoint-directional
  connect/reconnect/disconnect, dynamic output migration and protection,
  pointer cleanup, viewport transforms, node/field Problems navigation,
  invalid-draft Recovery isolation, parallel edge aggregation, self-loop and
  arrow geometry, SCC layout, accessible edge invocation, and safe Story
  deletion with legacy Actor/Dialogue/Quest membership repair, owned-resource
  cascade, unsaved/external-reference/incoming-transition blockers, and
  deletion of an empty `uncategorized` migration bucket. New coverage also
  proves that project creation leaves `stories/` empty, explicit Story creation
  persists a valid one-node Story, recent project paths normalize and persist,
  and missing project paths are omitted from the File menu.
- Scale coverage constructs a 200-node/400-connection Flow and lays out a
  200-Story/400-transition graph containing one large SCC.
- `studio/qa/2.1.2-connection-ui-acceptance.ps1` passed in the Release WPF
  process. It proved that unaffected parameter controls retain their UI
  Automation Runtime IDs across node click, drag, and Undo; captured occupied
  input/output endpoint drags while the opposite endpoint remained attached;
  and exercised same-endpoint reconnect, blank disconnect, Escape/Undo restore,
  multi-incoming handles, registry menus, property editors, Recovery
  creation/cleanup, exact Problem focus, aggregate graph edges, self-loops,
  auto-layout, and missing-target source focus. The same real-window run also
  proved the single left Story navigation plus the complete relocated overview
  on the right, including description, resource ownership, and Flow count. It
  also proved that the entered Story workspace has no duplicate Overview route,
  and exercised the direct `打开剧情` / `删除剧情` menu, cancel-preserves-file
  behavior, confirmation resource listing, and cascaded removal from disk and
  the visible navigation list. The run emitted
  `STORY_DELETE_CASCADE_RESOURCES=PASS`.
- The same Release process expanded File > Recent Projects, found the current
  project entry, and verified the renamed `项目文件夹` command. It emitted
  `FILE_RECENT_PROJECTS_MENU=PASS` and `FILE_PROJECT_FOLDER_LABEL=PASS`.
- Runtime Gradle `build`, vertical slice, 20-check regression, and CustomNPC+
  binding probes passed. The intentionally failed reload inside the regression
  still logs an expected error before `RUNTIME_FAILED_RELOAD_ROLLBACK=PASS`.
- The repaired legacy `examples/phase4_project` passed `phase4StoryProbe` and
  `scripts/verify-phase4.ps1` as a 20-node/20-connection graph.
- The 2.1.1 compatibility UI script also passed against the 2.1.2 binary,
  including 1700x980 and minimum 1100x700 workspace checks.
- The automated real-window run reported DPI 119 (approximately the Windows
  125% scale). Separate 100% and 150% environment screenshots remain a manual
  acceptance item and are not inferred from this run.

## Real WPF acceptance

Scripts:

- `.tooling/2.1-acceptance/m6-ui-acceptance.ps1`
- `.tooling/2.1-acceptance/m7-ui-acceptance.ps1`
- `.tooling/2.1-acceptance/final-ui-acceptance.ps1`
- `studio/qa/2.1.1-ui-acceptance.ps1`
- `studio/qa/2.1.2-connection-ui-acceptance.ps1`

The recorded runs cover free node drag, port connection, selection,
copy/paste/delete, undo/redo, pan/zoom, save and restart, derived graph layout,
search/filter/diagnostics, direct Flow navigation, project auto-restore, all
four Story editing pages plus the relocated Project Home Overview,
shared-reference synchronization, import independence,
Bottom Dock resize/collapse/restart, UI Automation names, and clipping bounds.
2.1 screenshots are under `.tooling/2.1-acceptance/screenshots/`; 2.1.1
screenshots are under `.tooling/2.1.1-acceptance/screenshots/`.
2.1.2 Flow, occupied-endpoint drag, aggregate/self-loop graph, and
missing-target screenshots are under
`.tooling/2.1.2-acceptance/screenshots/`.

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

The 2026-08-27 package verification produced exactly one file at
`dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`: file version `2.1.2.0`, size
`140463826` bytes, SHA-256
`e254336210f7d65d835f724ad5f1452f6237d572c4d9909cb82177ab2f460295`.
The self-contained EXE launched with the title `DarkGrey RPG Studio 2.1.2`.
It is not Authenticode-signed. Runtime artifacts are generated under
`build/libs/`.

The 2.1.2 real-window run also emitted
`STORY_FLUENT_CONTEXT_MENU_VISUAL=CAPTURED`. It verified that the direct Open
and Delete items retained at least a 32-DIP automation target, and recorded the
flat Fluent menu in
`.tooling/2.1.2-acceptance/screenshots/00b-story-fluent-context-menu-dpi-119.png`.
The File-menu evidence is
`.tooling/2.1.2-acceptance/screenshots/00-file-recent-projects-menu-dpi-119.png`
with SHA-256
`5ded2adb4956e178fca64445ba55103d90ad04f7c2d8f724813a6b3250bf0414`.
The compact vector New Story header command was verified in a real dark-theme
window and recorded at
`.tooling/2.1.2-acceptance/screenshots/00c-story-header-new-command.png`,
SHA-256
`7acc9aa0b63a830749a94328a34d3cc9990712a25e2ee4378b17ba0b76e88ba2`.
