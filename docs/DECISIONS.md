# DarkGrey RPG Studio 2.0 Decisions

## 2026-08-18 — WPF Phase 1 shell

- Use the built-in .NET 10 WPF Fluent theme and `DynamicResource` brushes. No third-party UI framework is introduced.
- The shell is a standard WPF window with a native menu, a compact navigation rail, a resizable resource browser, a central workspace, and a collapsible bottom panel.
- The resource browser defaults to 260 device-independent pixels and is constrained to 200–400. The bottom panel defaults to 220.
- Navigation and bottom-panel state live in dedicated view models so interaction is testable without launching WPF.
- The temporary application mark is generated from the checked-in `Assets/DarkGreyRPGStudio.svg` source and packaged as a transparent multi-size ICO.
- Startup repairs a missing `windir` process variable from `SystemRoot` before Fluent resources initialize. This is needed for sandboxed launch environments and is inert on normal Windows sessions.

## 2026-08-18 — Actor workspace lifecycle

- Actor creation uses a modal identity dialog with inline validation and an explicit normalization suggestion; invalid input is never silently rewritten.
- Rename remains an explicit file operation. The editor keeps an existing Actor ID read-only.
- Create, duplicate, rename, delete, and search are exposed as testable shell commands backed by `ProjectService`; UI tests verify their real files on disk.
- Destructive commands are disabled while the selected Actor is dirty. Switching resources asks to save first, while closing the application offers save, discard, or cancel.

## 2026-08-18 — Phase 1 placeholder navigation

- Dialogue, Quest, and Story are real selectable navigation destinations with explicit Phase 1 placeholder workspaces; they do not pretend to edit data.
- Settings is also a real destination and reuses the persisted live theme control rather than presenting decorative UI.

## 2026-08-18 — Feedback surfaces

- Operation and technical messages are stored in a bounded Output history; success, warning, and error remain visually subtle and structured.
- Validation and operation failures are mapped to structured Problems with resource and field context. Actor problems can navigate back to their resource.
- Routine success and failure feedback uses non-blocking toast notifications. Success toasts dismiss after four seconds and errors after eight; destructive confirmation still uses modal dialogs.

## 2026-08-18 — Commands and project creation

- Standard menu and keyboard commands call the same testable view-model operations as the visible workspace controls.
- Project creation is a modal, validated flow that creates the runtime-compatible directory layout through `ProjectService`; invalid IDs receive an explicit suggested normalization.
- Actor undo/redo stores editor snapshots and restores display name, notes, and tags while keeping validation and dirty state synchronized.

## 2026-08-18 — Responsive settings and runtime handoff

- User settings persist theme, safe window size/maximized state, resource-browser width, bottom-panel height, and the last opened project in `%AppData%\DarkGreyRPG\Studio\settings.json`.
- Saved window dimensions outside the current work area fall back to safe defaults. The minimum supported logical window remains 1100×700.
- The bottom panel is resizable and collapsible. Its saved height is constrained to 160–400 device-independent pixels; the resource browser remains constrained to 200–400.
- Studio does not claim a live Minecraft bridge. “准备 Runtime 重载” saves and validates files, then directs the user to execute `/dgrpg reload` in Minecraft.

## 2026-08-26 — Studio 2.1 Story Flow boundary

- Story Flow is a free-form draggable node canvas; Dialogue and Quest remain dedicated list/tree editors and are not embedded as Flow actions.
- DialogueExitBranch consumes named Dialogue exits, while EnterStory is the only Flow construct that creates a project-level Story dependency.
- New 2.1 nodes use explicit snake_case persisted types. Older Runtime node types remain loadable and are preserved byte-semantically at the type/property level when edited in Studio.
- Story logic and node layout stay in Story JSON for Runtime handoff. The M7 project graph is derived, logic-read-only, and stores only its own editor layout separately.

## 2026-08-26 — Studio 2.1 Project Graph layout

- Project Graph edges are rebuilt from valid EnterStory targets whenever project Stories are loaded; no graph operation writes a Flow edge.
- Project Graph positions use the editor-only `resources/editor/story-graph-layout.json` file so authors can arrange the overview without producing Runtime diffs.
- Missing targets do not create phantom Stories. They produce diagnostics on the source Story, while degree-zero Stories receive isolated warnings.
- Direct graph navigation opens the selected Story on its Flow route; layout dragging remains available from the node body and navigation is explicit through the node button or double-click.

## 2026-08-26 — Studio 2.1 Runtime transition contract

- PlayDialogue stores its named Result. New 2.1 flows route `next` into DialogueExitBranch; legacy flows may still branch directly on the Result name.
- DialogueExitBranch exposes configured named exits as ports, and Runtime validation requires every configured exit to have a connection.
- EnterStory is a terminal transition for the source Story, starts the target Story at its entry, advances it in the same event, and uses a bounded transition depth to reject cycles.
- StoryStart advances only while the instance is running; a waiting or idle instance is never revived by an unrelated event.
- `/dgrpg story start <id>` is the explicit operator entry point for the new Story-first Runtime flow.

## 2026-08-26 — 2.1 release acceptance boundary

- M8 is accepted at the Plan's required data-layer vertical slice plus real Forge server lifecycle: both named branches, project load/reload, Story list/info, legacy regressions, and CustomNPC+ stored-data binding persistence are proved.
- The Project Graph remains editor-only; its layout file is not a Runtime input and Story JSON hashes are checked around graph interaction.
- Final UI acceptance edits and restores the same referenced Actor across two Stories, edits an imported independent Actor, and verifies Bottom Dock and project restoration in the real Release WPF process.
