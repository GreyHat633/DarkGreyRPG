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

## 2026-08-26 — Studio 2.1.1 graph interaction boundary

- Flow and Project Graph share coordinate, pan, zoom, fit, and reset math, while each retains its own editing authority.
- Flow ports are real rendered controls and connections anchor to their measured centers; Project Graph edges remain derived and expose no editable ports.
- Project Graph cycles are warnings computed from SCCs. Layout runs on the condensed DAG and never changes Runtime Story JSON.
- Story page changes preserve an invalid Flow draft in memory. Save/Discard/Cancel is reserved for leaving the Story/project/application boundary.
- Project registry presence and Story Membership are distinct: missing is Error, present-but-unorganized is Warning, and reference insertion requires an explicit user action.
- Problems are replaced per source so Flow, project validation, and Project Graph diagnostics can coexist.

## 2026-08-27 — Studio 2.1.2 connection and recovery boundary

- One registry defines every supported Story node's Runtime type, aliases,
  localized label, category, property metadata, and output strategy.
- Inputs and outputs can both begin a connection gesture. Outputs remain
  single-target; inputs may have multiple sources and require an explicit fan
  handle when the source connection would otherwise be ambiguous.
- Reconnect and disconnect operations are atomic Undo units. Escape, lost
  capture, deactivation, view unload, and context-menu opening cancel the shared
  pointer state without leaking a partial connection.
- Invalid dirty Flow data is recoverable only from the editor recovery store.
  Runtime enumeration and official Story saves never consume that store.
- Validation coordinates include Story, node, and field so Problems navigation
  can focus the exact inline editor without reopening an already dirty Flow.

## 2026-08-27 — Studio 2.1.2 derived graph edge boundary

- Parallel EnterStory transitions aggregate per source/target while retaining
  their source node IDs and incoming branch reasons for tooltips and navigation.
- A graph edge has separate hit-test, visible stroke, arrow, and count visuals.
  The hit shape exposes an automation Invoke pattern as well as mouse input.
- Self-loops use explicit non-zero geometry. Cyclic layouts operate on SCCs and
  terminate with finite positions.
- Missing-target Problems navigate to the source EnterStory
  `target_story_id`; all graph interactions remain logic-read-only and are
  hash-checked against Story JSON.

## 2026-08-27 — Studio 2.1.3 resource lifecycle and library boundary

- Actor, Dialogue, and Quest remain project-level resources. Create and
  Duplicate are in-memory Drafts until first valid Save; Reference adds only
  Referenced membership and preserves the shared resource's Home Story.
- First Save validates and writes resource plus Owned Story membership with
  rollback on membership/write failure. A failed transaction keeps the Draft
  repairable and must not leave an orphan resource or membership.
- Dialogue Drafts begin with End/`complete` and no line; Quest Drafts begin with
  no objectives/groups and no fake actor. Dialogue owns lines/choices/exits;
  Quest owns objectives/completion modes; Story Flow owns transitions/rewards.
- Actor, Dialogue, and Quest each expose one compact, virtualized library that
  mixes Owned and Referenced entries. The shared link icon, missing warning
  icon, Home Story tooltip, and persisted 220–380 DIP width are UI contracts.
- Project Home selection, Core/WPF tests, Release UI automation, manual DPI and
  Minecraft client observation, Runtime probes, and packaging remain separate
  evidence categories. Studio 2.1.3 does not change Runtime 0.5.0.
