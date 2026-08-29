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

## 2026-08-28 — DarkGrey_RPG 0.3.0.0 graph language

- Story Flow is the sole editable source of Story-level execution. Session and
  Task resources own their local graphs; Project Story Graph remains derived and
  logic-read-only.
- Every persisted connection addresses stable node and port IDs and declares
  exactly one interface kind: `flow` or `logic`. Display names and ordering are
  presentation data and never participate in connection identity.
- Flow outputs accept at most one target while flow inputs accept multiple
  sources. Logic outputs accept multiple consumers while logic inputs accept at
  most one source. Flow and logic ports never connect to each other.
- Logic-only dependencies are acyclic. Flow cycles remain representable because
  their Runtime lifecycle is explicit and does not imply logic feedback.
- Task local graphs contain only logic ports. Session local graphs may contain
  both interface kinds. Aggregated Session and Task nodes expose stable dynamic
  ports derived from their resource boundary nodes.
- Product version `0.3.0.0` is independent from every resource schema version.
  Existing 2.1.3 data stays readable until an explicit, previewed, backed-up
  migration is accepted.

## 2026-08-28 — DarkGrey_RPG 0.3.0.0 non-goals

- No Task entry/return nodes, Task-embedded sessions, Task flow network,
  junctions, implicit fan-out, concurrent execution cursors, or second editable
  Project Story Graph are introduced.
- Actor remains a narrative identity and CustomNPC+ binding resource; Studio does
  not become an NPC health, model, combat, movement, AI, or animation editor.
- Migration never silently guesses an unsafe legacy structure and never rewrites
  a project without preview, backup, transactional replacement, and rollback.

## 2026-08-28 — DarkGrey_RPG 0.3.0.0 scope and aggregate ports

- Scope policy is separate from generic graph validation. Task rejects every
  flow port/edge; Story and Session may use both interface kinds.
- Story Start, Session Start, Task Activate, and Task Settle are unique required
  nodes and non-deletable editor contracts.
- Session End, Session Logic Output, Task settlement slots, and Task Logic Output
  are boundary declarations. Their stable IDs are projected unchanged to the
  aggregate Story node; labels and order are presentation only.
- Every Story Flow Session aggregate always exposes fixed `flow_in` and
  `logic_in` ports. Connecting `logic_in` is optional; omitting the port is not.
- Public output names are unique across flow and logic kinds. Interface shape is
  not used to excuse an ambiguous duplicate name.
- Legacy Jump may be loaded in Session compatibility mode but is not exposed as
  a primary authoring node.

## 2026-08-28 — DarkGrey_RPG 0.3.0.0 editor endpoint compatibility

- WPF connection gestures address `EffectivePortId`; `DisplayName` is never a
  connection key. Legacy `PortName` remains the fallback during migration.
- A UI drag target must have the same interface kind and opposite direction on
  another node before Core scope/cardinality/cycle validation runs.
- Flow and Logic wire colors/thickness come from one shared style source. Logic
  does not reuse a gray or Flow-colored line.
- Existing Story Flow controls default to Flow, preserving 2.1.3 behavior and
  data while the generic editor is introduced incrementally.

## 2026-08-28 — DarkGrey_RPG 0.3.0.0 graph edit transactions

- Candidate-edge validation is local to the edit and its affected endpoints;
  unrelated Problems in a repairable draft do not block corrective work.
- Reconnect is one transaction. Failure preserves the original connection and
  does not create an Undo entry.
- Connect, disconnect, reconnect, port label rename, and port reorder each form
  one full-document Undo snapshot; a new edit clears Redo.
- Logic cycle and scope/cardinality checks run before mutation, not only during
  Save validation.

## 2026-08-28 — DarkGrey_RPG 0.3.0.0 dynamic port ownership

- Dynamic-port permission comes from the canonical scope/node-role policy, not
  from labels or a user-editable persisted flag.
- Removing a referenced dynamic port is fail-closed by default; confirmed
  removal deletes the port and its incident edges as one Undo unit.
- Stable port IDs are opaque identities. Rename, reorder, Undo, and Redo never
  silently allocate a replacement ID.

## 2026-08-28 — DarkGrey_RPG 0.3.0.0 shared graph command routing

- One WPF command bridge normalizes input-first/output-first gestures and routes
  Story, Session, and Task edits to `GraphEditSession`.
- The bridge owns gesture-level eligibility only; Core remains authoritative for
  scope, cardinality, and logic-cycle validation.
- Dropping an existing wire on blank disconnects it; dropping a new draft on
  blank is a no-op. Invalid completion never becomes an Undo unit.

## 2026-08-28 — WPF graph automation identity

- Canvas AutomationIds are part of the real-window regression contract. A
  custom `FrameworkElementAutomationPeer` must explicitly forward the attached
  AutomationId instead of relying on host-specific default exposure.
- Real-window automation uses isolated project copies and must verify the source
  Story JSON hash set before and after the run.

## 2026-08-28 — Canonical graph host projection

- Story, Session, and Task bind through one host ViewModel over
  `GraphDocument`, `GraphEditSession`, and `GraphEditorCommandBridge`.
- Bindable refresh preserves unique node item identity and always re-derives
  ports/connections from stable IDs; it never treats labels as keys.
- Host validation state follows the owner of the command that just ran: bridge
  state for connection gestures and session state for direct graph edits.
- Node positions remain editor layout metadata. Phase 1 does not add WPF layout
  fields to canonical Graph JSON or claim layout-aware Undo persistence.

## 2026-08-28 — Canonical graph visual host

- Story, Session, and Task use the same `CanonicalGraphEditorView`; scope changes
  policy and port kinds, not gestures or layout mechanics.
- A direct port drag always creates a new candidate connection. Reconnect exists
  only when an existing connection explicitly supplies the original edge.
- The typed Host binding is authoritative over inherited shell DataContext.
- Connection-hit proximity selects the endpoint being moved. The opposite
  original endpoint anchors the draft; wrong-direction drops fail closed.

## 2026-08-28 — Canonical node parameter ownership

- `GraphNode.properties` is the shared untyped JSON parameter bag; typed Story,
  Session, and Task rules belong to node-definition schemas, not Graph Core.
- Keys have ordinal identity and ordinal serialization order. JSON values are
  cloned across constructors, history, and WPF projection boundaries.
- Property set/remove commands are atomic Undo units. The host exposes a
  read-only snapshot and does not leak the live mutable dictionary.

## 2026-08-28 — Canonical node shape definitions

- Fixed port templates and top-level property kinds/defaults live in the scoped
  node registry; Graph Core's persisted property bag remains untyped.
- Factory calls clone every fixed port and JSON default. Definition metadata can
  never be mutated through a created node.
- The fixed-shape factory does not allocate dynamic ports. A later semantic
  initializer must create minimum slots and synchronize trigger/choice metadata
  through the existing dynamic-port policy.

## 2026-08-28 — Canonical node CRUD transactions

- Node add accepts and clones a complete caller-supplied node; it validates only
  the candidate and affected scope constraints, not unrelated draft Problems.
- Referenced node removal is fail-closed until explicit confirmation, then
  removes the node and every incident edge as one Undo unit.
- Required/non-deletable protection applies only to a matching canonical
  definition in the current scope. Unknown and wrong-scope nodes remain
  deletable so malformed or partially migrated drafts can be repaired.

## 2026-08-28 — Canonical node selection and Delete routing

- Node and connection selection are mutually exclusive transient view state;
  selection never enters canonical graph JSON or layout metadata.
- Rebuild preserves a node selection only by one non-blank ID that remains
  unique in the current document. Missing, blank, and duplicate identities
  clear selection rather than guessing.
- Delete removes a selected connection first. Node Delete delegates to host
  CRUD without inferred confirmation, so referenced/protected failures retain
  selection and Problems evidence; callers must explicitly confirm referenced
  cleanup through the public seam.

## 2026-08-28 — Canonical node authoring metadata

- Author-facing name and category are immutable registry metadata, not
  persisted node-type aliases. Every scoped definition supplies both values.
- Authoring enumeration preserves canonical order and filters compatibility-only
  definitions; `legacy_jump` remains loadable only through compatibility paths.
- Factory-created nodes fall back to the definition's author name for null or
  blank overrides. Dynamic-slot initialization remains separate because the
  plan does not yet name every slot container in persisted properties.

## 2026-08-29 — Canonical node shape validation

- Shape validation is a standalone opt-in Core layer until new resource
  adapters and semantic creation own complete canonical shapes. Existing
  legacy/scope validation is not silently tightened in this slice.
- Fixed port identity, direction, and interface kind are structural. Fixed
  labels and order remain presentation metadata; extra properties remain open
  for deferred node-specific schemas.
- Only non-fixed ports count toward a registered dynamic role's minimum, so a
  mutated fixed port cannot masquerade as a required dynamic slot.

## 2026-08-29 — Safe canonical node authoring

- Authoring candidate creation is non-mutating. Callers receive either one
  detached, shape-valid node or stable diagnostics, then explicitly route the
  candidate through graph CRUD.
- And/Or minimum Logic inputs are structurally self-contained and can be safely
  initialized with opaque IDs. Session Choice is also authorable through its
  frozen paired option schema. Story Start triggers and Task Settle results
  still require semantic property mappings and fail closed.
- Duplicate ID and unique-type diagnostics take precedence over missing
  semantic initialization because they are immediately actionable and require
  no ID-source consumption.
- New Session End and Logic Output nodes receive an opaque stable `port_id`
  during authoring plus a localized default `display_name`. The Inspector edits
  only the display name; raw public IDs are not a primary author interaction.
  Reserved aggregate input IDs and existing Session public IDs fail closed.

## 2026-08-29 — Canonical blank-canvas node menu

- One registry-backed menu serves Story, Session, and Task scopes. It preserves
  registry/category order and never exposes compatibility-only definitions.
- Semantic-initializer roles and an already-present unique node stay visible but
  disabled; checking availability never consumes opaque node or port IDs.
- The blank-canvas click position is transient editor layout metadata. A click
  builds one detached candidate and commits it through the host; any failure is
  non-mutating and retains stable Problems diagnostics.

## 2026-08-29 — Parallel canonical resource envelope

- 0.3.0.0 canonical graph resources use a new schema-version-1 envelope beside
  legacy Story/Dialogue/Quest schema version 2; no legacy root is extended in
  place and no implicit conversion is allowed.
- The initial strict root owns only resource kind, stable identity, author name,
  and canonical graph. `story`, `session`, and `task` map one-to-one to their
  graph scopes and a mismatch is invalid.
- Semantic slot containers, membership, journal data, metadata, and persistent
  editor layout remain explicitly deferred. Adding guessed fields here would
  turn an incomplete schema into an accidental persistence contract.

## 2026-08-29 — Canonical resource workspace adapter

- A successful canonical graph mutation advances host revision and emits one
  graph-change event. Preview, failure, refresh, and layout-only movement do
  not, because they do not change the persisted canonical envelope.
- Workspace dirty state compares current canonical envelope content with the
  last explicitly saved baseline. Snapshot creation is detached; only an
  external durable-save success may call `MarkSaved`.
- Persistent node layout is still deferred and therefore cannot make this
  envelope dirty. Live workspace integration must not claim layout persistence
  until a separate editor-metadata schema exists.

## 2026-08-29 — Canonical resource repository boundary

- Core persistence is kind-scoped and receives its exact directory from the
  caller. It does not freeze `stories`/`sessions`/`tasks` project paths before
  the Story-first workspace and migration layout are resolved.
- Create and Replace are distinct operations. Stable filename identity cannot
  be renamed implicitly, and wrong-kind, legacy, malformed, or filename/ID
  mismatched documents fail closed.
- Writes use the existing atomic writer with staged strict deserialization.
  Repository success returns a detached reloaded resource; editor code may mark
  its baseline saved only after that success.

## 2026-08-29 — Parallel Story-first workspace state

- Story Flow is the canonical home graph and remains the default middle
  workspace. Actor selection is Inspector-only; Session and Task require an
  explicit open action rather than behaving like permanent page tabs.
- Folder expansion, single-click resource selection, and graph opening are
  separate state transitions. This preserves the plan's resource-tree behavior
  and avoids reusing the legacy four-route semantics under new labels.
- Returning through the Story breadcrumb reuses retained editor instances.
  A shell-owned leave gate decides unsaved-change transitions; the parallel
  state does not silently discard or save graph data.

## 2026-08-29 — Canonical Story-first workspace view boundary

- The new three-column resource-tree/graph/Inspector surface is a reusable
  typed control bound to the parallel workspace state, not an in-place rewrite
  of legacy route views.
- Session/Task activation is explicit through double-click or Enter. Actor
  clicks remain Inspector-only, and the breadcrumb returns to the retained
  Story host.
- Isolated WPF layout and binding tests are not MainWindow or real-window
  acceptance. This boundary intentionally waited for project-owned canonical
  resource paths and durable save wiring; the later live-mount decision below
  supplies both without changing this control's ownership.

## 2026-08-29 — Separate canonical Story membership manifest

- Story membership is not added to the frozen five-field graph envelope and
  does not mutate legacy Story schema version 2. A separate strict manifest
  owns the Story ID plus owned/referenced Actor, Session, and Task ID lists.
- Duplicate IDs and owned/reference overlap in the same resource kind are
  invalid. Array order is persisted but does not define resource identity or
  graph execution order.
- Loading or listing canonical resources never edits membership. Reference
  changes remain explicit commands and will use the plan-required confirmation
  before a resource is added to a Story.

## 2026-08-29 — Atomic Story membership persistence

- Membership persistence receives its exact directory from the caller until
  the canonical project layout is frozen. File identity is exactly `story_id`.
- Create and Replace are separate atomic operations with strict staged
  deserialization. Malformed, legacy, mismatched, or failed writes cannot be
  treated as a saved membership state.

## 2026-08-29 — Canonical project store layout

- New 0.3.0.0 resources live under
  `resources/canonical/{stories,sessions,tasks,memberships}`. The isolated root
  prevents strict canonical repositories from guessing among legacy JSON
  shapes or overwriting 2.1.3 files.
- Store construction and inspection are read-only; initialization is explicit.
  Migration is the only future operation allowed to copy legacy content into
  this layout, and it must retain preview/backup/rollback guarantees.

## 2026-08-29 — Canonical Story workspace loading

- Opening a canonical Story requires both its strict Story envelope and its
  membership manifest. Failure at either root does not create a partial
  workspace.
- Member IDs retain owned/reference provenance and manifest order. A missing
  Actor, Session, or Task remains visible as an unresolved entry and produces a
  stable error Problem while other requested members continue loading.
- Actor resolution loads only explicitly requested IDs. The loader never scans
  legacy Dialogue/Quest roots and never performs an implicit conversion.

## 2026-08-29 — Missing members remain workspace state

- Snapshot adaptation preserves manifest order and owned/reference provenance
  in the Story resource tree instead of re-sorting or flattening membership.
- Missing members remain selectable tree entries with an explicit Inspector
  error state. They cannot open a blank Session/Task editor, because that would
  falsely present missing data as a valid resource.
- Loader validation issues remain exposed on the workspace for shell Problems
  publication during live mounting.

## 2026-08-29 — Canonical editor durable save boundary

- A canonical editor never marks itself saved before its kind-scoped repository
  atomically replaces and reloads the resource. Failed persistence leaves both
  disk content and editor dirty state unchanged.
- Saving a clean editor is an explicit no-write operation. This package does
  not invent partial or transactional Save All behavior across multiple files;
  that user-visible policy remains a separate shell decision.

## 2026-08-29 — Live canonical Story mount

- Selecting a legacy-listed Story ID mounts the Story-first canonical workspace
  only when a same-ID canonical Story envelope or membership file already
  exists. Absence of both preserves the legacy workflow; presence of only one
  required root fails closed and never falls back or converts implicitly.
- While mounted, the canonical three-column workspace owns the central surface
  and the global legacy resource browser is effectively hidden without changing
  the user's stored visibility preference.
- Save Current persists only the active canonical graph through its strict
  repository. Any dirty canonical editor blocks Story, project-home, project-
  graph, and close navigation until individually saved; cross-file Save All is
  disabled because no transactional multi-resource contract exists.
- Loader missing-member issues and active-editor validation are published into
  Problems. Build and automated shell lifecycle coverage are evidence for the
  mount; real-window interaction acceptance remains separate.

## 2026-08-29 — Canonical Session/Task lifecycle boundary

- Canonical Session and Task creation is Core-owned and produces the minimum
  scope-valid blank graph before appending the resource ID to the current
  Story's owned membership. Story resources and legacy Actor ownership remain
  outside this service.
- Reference add/remove changes only the selected Story membership and preserves
  unrelated list order. Removing a reference remains possible when its resource
  file is missing, because broken membership must be repairable.
- Owned deletion fails closed when any other canonical Story lists the resource
  as referenced or owned. The latter also protects against corrupt duplicate
  ownership instead of deleting shared data.
- Resource and membership repositories remain individually atomic. Cross-file
  create/delete uses explicit in-process compensation with an independent
  atomic writer for restoration; this is not a crash-durable transaction and
  must not be presented as one.

## 2026-08-29 — Canonical Session/Task resource command ownership

- The resource tree owns selection scope and routes Create, Reference, and
  Delete from both the Story-title action bar and equivalent context menus.
  Selecting a Session or Task item also selects its containing resource kind;
  folder selection never replaces the middle graph.
- Shell owns all lifecycle side effects. It blocks mutation when any canonical
  editor is dirty, opens dedicated Session/Task dialogs, invokes the Core
  lifecycle service, and rebuilds the detached workspace only after an
  accepted mutation.
- Reference removal never deletes its resource file. Owned deletion first
  displays all other Story blockers, then requires destructive confirmation.
  An owned membership whose file is already missing remains fail-closed rather
  than silently deleting provenance.
- The canonical dialogs intentionally do not reuse legacy Dialogue/Quest UI
  types: Session is labelled `会话`, never `对话`. A filtered-out picker
  selection is cleared so an invisible candidate cannot be confirmed.

## 2026-08-29 — Canonical Actor lifecycle over shared project resources

- Actor files remain project-level resources under `actors/`; canonical Story
  membership records ownership/reference provenance without duplicating Actor
  JSON under `resources/canonical/`. A newly owned Actor uses the canonical
  Story ID as `home_story_id`.
- Reference removal changes membership only and remains available when the
  Actor file is missing. Owned deletion is fail-closed for missing Actor data
  and for every other canonical or legacy owned/reference membership.
- The same-ID legacy owned membership generated from `home_story_id` during
  2.1.3 migration is a transition mirror of the canonical owner and does not
  block deletion. A same-ID legacy reference still blocks, as do all legacy
  memberships belonging to a different Story.
- Shell releases a clean cached Actor document before Core deletes the shared
  file. A dirty cached Actor cannot be released and therefore blocks deletion;
  this prevents stale in-memory state from surviving an external repository
  mutation.
- Actor create/delete and canonical membership persistence compensate ordinary
  in-process failure, but do not claim crash-durable multi-file atomicity.

## 2026-08-29 — Canonical Story discovery independent of legacy aliases

- Project Home discovery unions canonical Story-envelope and membership
  filenames without creating directories. Strict loading is isolated per Story
  ID so incomplete or invalid roots remain visible with diagnostics and cannot
  hide unrelated valid canonical Stories.
- Legacy and canonical Story sources merge by ordinal ID. Canonical-only
  Stories are selectable, while a same-ID pair appears once with canonical
  display/overview data and canonical opening precedence; no implicit legacy
  conversion is performed.
- Failed opening of an incomplete or invalid canonical Story clears the prior
  clean canonical workspace instead of leaving stale content under a new list
  selection.
- Project Graph remains derived from legacy Story resources in this slice.
  Legacy deletion is disabled for every list item that also has canonical data,
  including same-ID pairs, until canonical Story creation/deletion and graph
  derivation have their own explicit contracts.

## 2026-08-29 — Bound aggregate placement from the canonical resource tree

- Story Flow `session` and `task` placements are semantic nodes, not generic
  palette nodes. They require `resource_id` and can only be initialized from a
  resolved canonical Session/Task envelope; repeated placements are valid.
- Public aggregate outputs are snapshots of child boundaries at placement:
  Session `end` and `logic_output`, plus Task `settle` result slots and
  `logic_output`. Their identities and order are preserved, and direct dynamic
  add/remove/rename/reorder is rejected because these ports are projections.
- The current tree is membership-scoped, so drag/drop never changes membership.
  Actor and missing-resource drops are not accepted. A future global source
  must ask before adding Referenced Membership, then create the node, and must
  never copy the resource implicitly.
- Automated routed-state coverage and a real-window render prove the integrated
  surface. Manual physical mouse-drag behavior remains a distinct acceptance
  category and is not inferred from those results.

## 2026-08-29 — Session Choice state outputs and aggregate activation

- Each persisted Session Choice option has exactly `option_id`, `display_text`,
  and `flow_port_id`. `flow_port_id` identifies its Flow branch output;
  `option_id` also identifies its visible Logic state output. The two IDs must
  be distinct and ordinal-unique in their respective sets.
- One option maps one-to-one to one Flow output and one Logic output. The Flow
  label is `display_text`; the Logic label is `已选择：{display_text}`; both
  orders follow the options array. Shape validation fails closed on any drift.
- Choice add/remove/rename/reorder is owned by one Core semantic transaction
  surface that updates options and both output sets atomically. Referenced
  option removal requires explicit confirmation and cleans both Flow and Logic
  edges in the same Undo unit. Generic dynamic-port editing is read-only for
  these outputs and cannot create a partially synchronized Choice.
- Session Start has exactly one fixed Flow output and one fixed Logic output.
  Story Start owns Story entry triggers; Session Start never multiplexes those
  trigger ports. A Story Session aggregate's fixed `logic_in` maps to Session
  Start `logic_out`, while whether that input is connected remains optional.

## 2026-08-29 — Shared Session node Inspector authoring

- Canonical graph node/connection/clear selection is surfaced by the shared
  editor view and routed through the Story-first workspace. Clearing a graph
  selection restores the active resource Inspector; it does not create another
  editor or document state.
- Session Line text, Choice prompt/options, End display names, and Logic Output
  display names edit the active host-backed canonical document. Stable option
  and port IDs remain command identity only and are not author-editable fields.
- Choice option commands call the Core paired Flow/Logic transactions. Boundary
  move commands are disabled at the first/last item. Referenced removal first
  fails closed with Problems; only the exact confirmation-required result opens
  the owner-bound WPF confirmation, and acceptance retries the same atomic Core
  transaction with explicit authorization.
- This is automated WPF/Core evidence only. It does not prove manual window,
  network, Dedicated Server, or Minecraft client behavior.

## 2026-08-29 — Canonical Session Logic evaluation and snapshot state

- Session Start `logic_out` equals the aggregate activation input. An omitted
  Story connection supplies false; the fixed port itself remains mandatory.
- Each Choice exposes exactly one true option state after selection. And/Or/Not
  and Condition read the same deterministic acyclic Logic network; unconnected
  inputs are false, outputs may fan out, and each input accepts at most one
  source.
- Public Logic Output values are keyed by stable `port_id`. Logic evaluation
  never advances Flow; Condition alone selects one of its two Flow branches.
- Session snapshots retain Choice-node trajectory, latest per-Choice selection,
  internal/public Logic maps, activation input, cursor, status, and final End.
  Restore rejects mismatched resources and contradictory, stale, unknown, or
  incomplete state. Legacy constructors remain source-compatible but cannot
  restore a non-empty Choice history without its full trajectory metadata.
- This slice remains resource-local. Project/Story orchestration, disk/NBT,
  network, UI, and Minecraft execution are separate Stage 3 evidence gates.

## 2026-08-29 — Session public boundary and Line speaker validity

- End and Logic Output IDs and display names form one shared public Session
  namespace. Runtime rejects duplicates across both kinds and reserves the
  aggregate input IDs `flow_in` and `logic_in`.
- Session Line speaker authoring uses a non-editable selector over the current
  Story's resolved Actor items and persists only the stable Actor ID. A blank
  value is an explicit invalid-until-selected state; a missing stored ID remains
  visible as unresolved and is never silently erased.
- Referencing a project Actor outside the current Story remains a separate
  confirmation-and-membership mutation and is not inferred by the selector.

## 2026-08-29 — Canonical SessionInstance persistence boundary

- One player UUID plus Story ID owns at most one unconsumed SessionInstance.
  Its identity also retains the aggregate placement and Session resource, while
  all client-facing actions must match the positive transport ID and current
  node before the runtime can mutate.
- Completion stays stored until the Story consumes the single End port and
  detached public Logic outputs. Consumption removes the instance but never
  rewinds its transport counter.
- Strict NBT persists `next_transport_id` separately from live instances so
  consumed or empty stores cannot reuse a stale packet identity after restart.
  Restore resolves and validates the entire batch before replacing live state.
- This is server-neutral evidence. Forge storage, canonical packets/GUI, Story
  execution, and real client/server acceptance remain separate gates.

## 2026-08-29 — Canonical Session wire identity

- Canonical Session uses a separate strict wire contract instead of extending
  legacy Dialogue packets. Client actions identify the Story, current node, and
  non-reusable transport ID; Choice submits the stable `option_id`, never its
  display order or array index. Player identity is taken from server context.
- A Line frame has a nonblank speaker and no choices. A Choice frame has no
  speaker and at least one ordered stable-ID/display-text pair. This mirrors the
  canonical node schema rather than fabricating a legacy narrator.
- Close packets do not expose End `port_id` or public Logic outputs. Those are
  server-side Story results; the client only receives enough identity to close
  the matching screen.
- Codec probes establish only the wire shape. Later bounded packages added
  Forge registration/handlers, manager routing, GUI/client state, and Forge
  persistence; Story consumption and real network behavior are still not
  inferred from those code-level probes.

## 2026-08-29 — Canonical Session Forge persistence

- Canonical Session state is stored once in the overworld `MapStorage` under
  `darkgrey_rpg_canonical_sessions`; per-player/per-Story ownership remains in
  `CanonicalSessionInstanceStore` rather than separate dimension files.
- `readFromNBT` validates the strict NBT immediately but retains an exact pending
  copy until a Session-resource resolver can restore the whole store. Missing
  resources are retryable and cannot silently erase persisted sessions.
- Successful mutations mark `WorldSavedData` dirty. If a runtime mutation
  throws after changing the store, including a cycle guard changing the runtime
  to `FAILED`, the changed state is also marked dirty and survives restart.
  Rejected stale/forged requests that leave state unchanged remain clean.
- Forge storage exposes detached snapshots and immutable current steps only.
  Network handlers, client GUI, Story result consumption, and real integrated
  server acceptance remain distinct Stage 3 gates.

## 2026-08-29 — Canonical Session server orchestration boundary

- A Session can start only from an exact canonical Story `session` placement
  whose `resource_id` resolves to a Session present in exactly one of that
  Story's owned/referenced Session lists. All Line Actors must likewise resolve
  globally and belong to exactly one of the same Story's Actor lists.
- Client actions never supply player identity. The trusted server UUID plus the
  action's Story, transport ID, current node, kind, and stable option ID are
  checked through `CanonicalSessionSavedData` before advancing.
- Active Line/Choice state projects to a wire frame. Completed state projects
  to a client Close and a detached server-only result containing placement,
  resource, unique End port, and immutable public Logic outputs. Projection
  never consumes the persisted completion; only Story orchestration may do so.
- Server dispatches defensively copy mutable Forge `IMessage` objects. Missing
  membership, resource drift, stale/forged actions, incoherent cursors, and
  `FAILED` snapshots fail closed. This still does not prove handlers, GUI,
  Dedicated Server, Story continuation, or live Minecraft behavior.

## 2026-08-29 — Canonical Session Forge routing and client presentation

- Legacy packet discriminator IDs 0-4 remain unchanged. Canonical Action,
  Frame, and Close packets use IDs 5-7 with Action server-bound and presentation
  packets client-bound; registration is guarded against duplicate setup.
- The server Action handler derives `EntityPlayerMP` from the message context
  and schedules the Forge manager on the main thread. The manager binds the
  current project snapshot and overworld SavedData, then sends exactly the
  Frame or Close contained in the server-service dispatch.
- Client presentation is a separate canonical screen backed by a detached model,
  not the legacy Dialogue GUI. Active updates require matching transport,
  Story, and Session resource identity; stable `option_id`, never button index,
  is returned to the server. Matching Close clears only that canonical session.
- The GUI disables input while awaiting the server, ignores Escape as a local
  completion mechanism, and supports bounded scrolling for long Line/Choice
  content. It never receives or displays End or Logic results.
- Focused model and routing probes plus Java 8 bytecode checks are accepted as
  implementation evidence. Start/resume entry, Story result consumption, real
  GUI interaction, live networking, Dedicated Server, and Minecraft client
  acceptance remain distinct Stage 3 gates.

## 2026-08-29 — Canonical Session acceptance command boundary

- `/dgrpg session play <story_id> <aggregate_node_id>` and
  `/dgrpg session resume <story_id>` are operator-only acceptance/debug entries.
  They do not replace Story Start triggers and are not evidence of Story Flow
  orchestration.
- Both paths require `EntityPlayerMP`; the Forge manager derives the trusted UUID,
  uses the current canonical snapshot and overworld Session SavedData, and routes
  exactly one server-service Frame or Close. Completion remains unconsumed and
  End/Logic results never appear in command chat.
- Completion candidates are deterministic: canonical Story IDs and matching
  Session aggregate placement IDs are sorted. Legacy `/dgrpg` categories and
  canonical packet discriminator IDs remain unchanged.
- Pure command probes and a production-faithful trusted-UUID service seam are
  accepted as code-level evidence only. A live player, Dedicated Server packet
  exchange, GUI interaction, and Story consumption remain separate gates.

## 2026-08-29 — Pure Story routing of completed Session results

- A completed canonical Session selects a Story transition only by the stable
  aggregate placement ID and selected End `port_id`; display-name changes and
  public-port ordering do not change connection identity.
- Project-bound routing revalidates the Story, Session resource kind/ID, and
  exactly-one owned/referenced Session membership at route time. Resource or
  membership drift fails closed instead of trusting an older completion.
- The selected End Flow output must have exactly one outgoing edge to an
  existing Flow input. Public Logic values must exactly match the aggregate's
  Logic output IDs and are detached in deterministic port order.
- This router is deliberately pure. It does not mutate a Story cursor, consume
  `CanonicalSessionSavedData`, claim crash ordering, or prove live Minecraft
  continuation; those remain the next Stage 3 integration boundary.

## 2026-08-29 — Bounded live acceptance of canonical Session execution

- An actual Forge 1.7.10 Dedicated Server and Minecraft client completed the
  `stage3_tavern` canonical Session through Line, Choice `Accept`, Condition true
  branch, accepted Line, selected End, and GUI Close. This is stronger evidence
  than the earlier pure packet/model probes, but it is not evidence for the
  unclicked Decline branch or full Story execution.
- `save-all`, graceful server restart, player reconnect, and `/dgrpg session
  resume stage3_tavern` reproduced the completed state as a Close envelope. The
  SavedData file remained byte-identical across the resume, confirming that the
  current implementation still retains an unconsumed completed Session.
- The client-side canonical project copy was absent and emitted a local reload
  error; the successful path was server-authoritative. Earlier malformed-address
  connection attempts and the final forced client-process exit are operator
  automation artifacts and are excluded from acceptance claims.
- Therefore the PLAN's real-Minecraft Session requirement now has bounded live
  evidence, while the Stage 3 Gate remains open until one Story continuation is
  durably accepted and the matching Session completion is consumed without a
  crash window.

## 2026-08-29 — Atomic persisted Session-to-Story handoff

- Canonical Session world state is now one versioned wrapper containing Session
  snapshots and pending Story continuations. A legacy direct Session root is
  still accepted and is migrated on its next write.
- For one player UUID plus Story ID, accepting completion revalidates the current
  Project snapshot, removes the exact completed Session, and installs one
  immutable continuation in the same synchronized `WorldSavedData` mutation.
  The continuation records stable placement/resource/transport/End identity,
  one target Story node/port, and the exact detached public Logic map.
- Exact replay is idempotent. A conflicting continuation, Session/continuation
  overlap, or new Session start while a continuation is pending fails closed.
  The wrapper is the atomic persistence unit; no stronger disk-fsync guarantee
  is inferred.
- A real legacy completed-session file migrated to zero Sessions plus exactly
  one accepted continuation. After graceful restart, resume failed because the
  Session was consumed, new play failed because the continuation was pending,
  and a subsequent save preserved the exact file hash.
- This closes the Stage 3 single-result handoff gate. It does not execute or
  acknowledge the target Story node; that responsibility remains with the
  Stage 5 Story Flow runtime.

## 2026-08-29 — Save-time aggregate interface synchronization

- Session/Task public-boundary changes are synchronized when the child is
  explicitly saved, across every repeated Story placement with the matching
  aggregate kind and ordinal-exact `resource_id`.
- Stable compatible port IDs preserve connections. Removed or retyped ports
  expose all affected connections and require one explicit confirmation; an
  accepted change replaces every placement and removes obsolete edges in one
  Story Undo unit.
- Analysis is detached and apply fails closed when placements or obsolete-port
  references drift before commit. Malformed and ambiguous bindings never
  receive a partial repair.
- Story synchronization precedes the child disk write. A write failure rolls
  back exactly that derived edit and restores displaced Undo/Redo state. A
  successful child write leaves Story dirty for a later explicit Story save.

## 2026-08-29 — Canonical-derived Project Story Graph

- Canonical Project Story Graph derivation is a read-only Core service over the
  strict Story/membership discovery union. Only exact `enter_story` nodes with
  string `target_story_id` contribute transitions; inspection creates no
  directories and performs no persistence mutation.
- An `enter_story` node ID must be ordinal-unique across every node in its Story
  graph. Missing, wrong-kind, blank, unknown, malformed, or ambiguous targets
  fail closed with stable Story/node diagnostics rather than a guessed edge.
- The WPF Project Graph merges canonical identities with legacy-only Stories.
  Canonical data wins for a same-ID pair, so legacy transitions cannot shadow
  or duplicate the canonical Story. Project Graph remains logic-read-only.
- Problems and edge details retain the canonical source node identity. Opening
  one returns to canonical Story Flow and requests selection of the unique
  source node; isolation/cycle state is derived, not persisted.

## 2026-08-29 — Canonical Story create/delete lifecycle

- New Story authoring now creates a canonical Story envelope with the required
  `start` node and an empty same-ID membership. Legacy Story creation remains
  available only through the legacy service boundary, not the primary 0.3 UI
  command.
- Deletion is planned from strict canonical roots. Owned Actor/Session/Task
  files, incoming `enter_story` transitions, malformed graphs, and cross-Story
  canonical/legacy membership use are enumerated deterministically and fail
  closed before confirmation.
- Referenced resources are retained. If canonical and legacy Stories share an
  ID, canonical deletion removes only canonical roots and owned resources; the
  legacy Story remains visible and eligible for its existing delete workflow.
- Every delete target is snapshotted before mutation. Ordinary failure restores
  exact bytes and validates identities, but this is not crash-durable atomicity.
  Shell additionally rejects dirty canonical editors and dirty owned Actor
  caches before the Core delete begins.
- An Actor owned by a canonical Story can be edited and saved without a legacy
  alias. The strict canonical membership proves ownership before the shared
  top-level Actor repository is written.

## 2026-08-29 — Canonical Task instance, event, and persistence boundary

- A TaskInstance is identified by trusted player UUID, Story instance ID, Story
  Task-placement ID, and the placement's exact Task resource. The store key uses
  the first three fields and rejects resource drift, so the same placement cannot
  create a second instance or Journal entry in one Story instance.
- Task-local evaluation remains pure Logic. Activate fans out, Objective completion
  may enable later Objectives, Logic Output only snapshots a named boolean, and
  Settle persists the first true slot in visible order as the sole result.
- Active Objectives subscribe through a player/event/selector index. Forge death,
  pickup, and Actor-interaction events dispatch only to candidates returned by that
  index; Stage 4 adds no per-player/per-Task/per-Objective Tick scan.
- Overworld `CanonicalTaskSavedData` persists the strict TaskInstance store and
  rebuilds subscriptions from restored active Objective state after resources bind.
  Mutating dispatches mark data dirty; rejected/no-op events do not fabricate writes.
- Canonical Journal state is projected from persisted snapshots, then adapted to the
  existing Quest Journal transport through a bounded digest ID. The compatibility
  layer does not become Task identity or runtime authority.

## 2026-08-29 — Bounded live acceptance of canonical Task execution

- An actual Forge 1.7.10 Dedicated Server and Minecraft client started one manual
  Stage 4 acceptance Task, advanced a collect Objective to `1/2`, saved, restarted,
  reconnected, and restored exactly `ACTIVE 1/2`.
- A second pickup settled the same instance once as `COMPLETED 2/2`; the Completed
  Journal tab showed one entry with `result=success`. The final SavedData SHA-256 is
  `39CEE515AB9C9DFCA38684C63039E2553A2265A916331D0D92710C28C400D791`.
- The Journal's fixed design height now clamps to the available scaled GUI height,
  keeping title, tabs, content, and Close visible on the real 854x480 scale-2 client.
- A prior run that rebuilt the development JAR while the live server lazily loaded
  the same path is excluded after a transient missing-class failure. The accepted
  clean restart used immutable JAR SHA-256
  `5DF0459C01B8B6A0A243196FA987FFF4373DDCA9A74065547A8FE34BEAC9C09E`.
- This closes the Stage 4 Task Logic Graph gate. The operator start command is not
  Story execution, and no claim is made yet for aggregate Task entry/return, Story
  termination cleanup, repetition, EnterStory, or the tavern-owner vertical slice.

## 2026-08-29 — 0.3.0.0 Stage 5 acceptance is the Runtime execution loop

- The CustomNPC+ tavern-owner chain is retained only as a Runtime Vertical Slice
  test host and Minecraft compatibility artifact. It is not the final in-game
  product form and is not a blocking Stage 5 acceptance path.
- Stage 5 closes when Story Flow, Session, Task, Objective progress, Action,
  termination, persistence/reload, and active Story/Session/Task cleanup form one
  deterministic automated Runtime loop. The loop must not depend on CNPC-specific
  binding, a deployed story pack, final NPC presentation, or final Task UI.
- The coordinator probe now crosses both active Session and active Task persistence
  boundaries, settles the Objective-driven Task, executes the authored Action,
  terminates the Story, and proves active child cleanup. Existing client evidence
  through Task `ACTIVE 0/10` remains compatibility evidence and is not upgraded into
  an unperformed client-side 10/10/reward/cleanup claim.
- CNPC product binding, final NPC experience, final story-pack deployment, and final
  Task UI are deferred to 0.3.1.0.

## 2026-08-29 — Legacy mid-flow events become persisted Story waits

- Legacy `ActorInteract` and `EnterRegion` nodes migrate to ordinary Story Flow
  nodes persisted as `interact_actor` and `enter_region`, each with fixed
  `flow_in` / `flow_out`. They are not folded into StoryStart trigger metadata.
- `interact_actor` retains one exact `actor_id`. `enter_region` retains exact
  scalar `dimension`, `x`, `y`, `z`, and positive `radius`; unknown fields,
  conflicting aliases, invalid values, and ambiguous topology fail closed.
- The single Story cursor persists the active event wait through NBT. A matching
  event resumes that cursor before Start-trigger matching; without a matching wait,
  existing Start behavior remains available. Multiple matching active waits for one
  player/event are ambiguous and fail closed with child cleanup.
- Region continuation consumes the existing periodic player-position semantics:
  being inside the persisted sphere is sufficient to resume. Region StoryStart
  matching retains its existing outside-to-inside edge tracker.
- The tracked 2.1.3 project is migrated only by explicit preview and confirmation.
  Legacy sources remain byte-identical, canonical destinations never overwrite
  existing data, and complete backup plus log evidence is required.
