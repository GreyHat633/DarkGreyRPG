# Phase 2 implementation report

Version: 0.2.0  
Artifact: `build/libs/darkgrey_rpg-0.2.0.jar`  
SHA-256: `FD05B1F1FF2FA2DC1ADBE1DA25A0796A378ABECFF348F2E8F442955292B06A96`

The Phase 1 `0.1.0` artifacts remain in `build/libs` for rollback.

## Implemented

- Strict standalone Dialogue resources with `schema_version`, `id`, `title`,
  `speakers`, `entry`, `nodes`, and `metadata`.
- Line, Choice, Jump, and End nodes.
- Named End Results such as `accept` and `refuse`.
- Actor speaker references without Actor ownership of Dialogue.
- Missing Actor, duplicate node, missing target, unknown field, and invalid
  Result validation.
- Atomic project snapshot reload: invalid Dialogue data does not replace the
  last valid project.
- Server-authoritative per-player Dialogue sessions.
- Session ID, current node ID, and Choice range checks on client actions.
- C2S actions are queued to the server tick thread; S2C GUI work is queued to
  the client tick thread.
- Independent Minecraft Dialogue GUI with wrapped long text, mouse-wheel
  scrolling, Choice buttons, paging, keyboard number selection, and
  Enter/Space continue.
- `/dgrpg dialogue list`, `info`, `play`, and `last-result`.
- Studio Dialogue editor with an all-nodes list, multiline editing, drag
  reorder, add, duplicate, delete, Undo/Redo, search, replace, Speaker picker,
  node target picker, and atomic save.
- Example `tavern_offer` Dialogue with accept, refuse, ask-more, and Jump-back
  paths.

Dialogue contains no Quest action and cannot call `StartQuest`. Quest and Story
remain unimplemented.

## Verification

- Gradle build, Spotless, and Checkstyle: passed.
- Runtime Dialogue load probe: passed.
- Line -> Choice -> End Result `accept` flow probe: passed.
- Choice -> Jump -> Choice loop probe: passed.
- Unicode frame, action, and close packet codec probes: passed.
- Phase 2 static schema, connection, scope, main-thread, and session-authority
  checks: passed.
- Godot scene startup: passed.
- Studio Dialogue save/reload, four-node-type, Quest-scope, and editor-render
  probes: passed.
- Reobfuscated JAR: 73 entries, Java 8 bytecode, no shaded CustomNPC+ classes.
- Isolated Forge 10.13.4.1614 server loaded DarkGrey_RPG 0.2.0 and the Phase 2
  project, then reached `Done`.

The dedicated-server process was terminated after the `Done` evidence was
captured, so the Gradle `runServer` wrapper itself ended non-zero. This was an
intentional test-process termination, not a startup failure.

## Live acceptance boundary

No real Minecraft client joined the test server. The following still require
the manual procedure in `PHASE2_TESTING.md`:

- visually confirm the Dialogue GUI, wrapping, scrolling, and input;
- send real client Choice packets through multiplayer;
- confirm `/dgrpg dialogue last-result` after choosing both `accept` and
  `refuse`.

The flow engine and packet codecs are tested, but those tests are not presented
as live client proof.
