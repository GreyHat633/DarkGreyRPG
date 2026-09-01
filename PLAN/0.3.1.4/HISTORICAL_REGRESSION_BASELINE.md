# DarkGrey_RPG 0.3.1.4 — Historical Regression Baseline (Gate 0E)

> This is a contract inventory, not an acceptance report. `Current valid` records the
> 0.3.1.4 interpretation of the historical material. It does **not** claim PASS,
> `AGENT_VERIFIED`, or `USER_ACCEPTED`; every Release EXE item still needs the
> evidence described below.

## Evidence boundary and authority

- Authority order is: current user feedback, the 0.3.1.4 Construction PLAN, the
  2026-09-01 handoff decisions, then older plans/reports. Current source is evidence
  of implementation only; it does not redefine the contract.
- Automated tests/probes may prove schema, state transitions, IDs, cardinality,
  serialization, and deterministic projections. They cannot prove real pointer
  hit-testing, cursor appearance, drag geometry, DPI/theme legibility, or an
  end-to-end authoring gesture.
- The Release EXE boundary means the authoritative self-contained Windows x64
  executable at `E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`,
  operated in a clean project. A candidate under `.tooling` or `bin\Release` is not
  sufficient. EXE metadata/hash must be recorded when a release candidate is tested.
- `Not superseded` means a regression remains a NO-GO until the required evidence
  exists. `Superseded` means the old assertion must not be restored; the replacement
  rule is the contract.

## Minimum contracts from 0.3.1.4 section 19

| Contract ID | Source version | Current valid behavior | Automated-proof boundary | Release EXE proof boundary | Superseded status |
|---|---|---|---|---|---|
| H-TERM | 0.3.1.0; 0.3.1.4 §19 | Author-facing `故事`; hierarchy is `图谱 → 流程图 → 会话图/任务图`. | Assert labels/route metadata and persisted graph kind. | Navigate Project → Story → Session/Task and inspect every visible label. | Not superseded; old “剧情/局部图” assertions are replaced (see below). |
| H-FLOW-CARD | 0.3.1.0/1.1; 0.3.1.4 §19 | Flow: output max 1, input may be many. | Core connection validator and serialization tests. | Real drag matrix: Flow output-one and input-many, including reconnect/cancel. | Not superseded. |
| H-LOGIC-CARD | 0.3.1.0/1.1; 0.3.1.4 §19 | Logic: output may be many, input max 1. | Core validator and connection mutation tests. | Real drag matrix: Logic output-many and input-one, including reconnect/cancel. | Not superseded. |
| H-MULTI-DRAG | 0.3.1.3; 0.3.1.4 §19 | Ordinary drag on a multi-capacity port adds one connection; `Ctrl` drag moves the complete existing bundle. | Gesture mode/state and resulting connection-set tests. | Continuous-frame evidence for ordinary add and Ctrl bundle move; cancel leaves canonical graph unchanged. | Not superseded. |
| H-SINGLE-RECONNECT | 0.3.1.3; 0.3.1.4 §19 | Occupied single-capacity port grabs the real existing endpoint; fixed opposite endpoint stays fixed. Valid same-direction drop commits atomically; Esc restores; blank drop disconnects; invalid drop leaves graph unchanged. | Transaction/rollback/hash tests; no fake second connection. | Continuous frames: original wire, mouse-down without collapse, endpoint movement, target hover, commit/cancel. | Not superseded. |
| H-HITBOX | 0.3.1.2B, revised 0.3.1.3/1.4 | Visual anchor ≈ 9–11 px; interaction hitbox ≈ 18–20 px; label text is not a wire target and hitbox cannot capture adjacent ports. | Geometry/unit and non-overlap tests. | Real pointer probes around anchor, label, and neighboring ports at required DPI/resolutions. | The 0.3.1.2B “9 px is the hit target” assertion is superseded. |
| H-DIRTY | 0.3.1.1/2A; 0.3.1.4 §19 | Unsaved/dirty state is not a navigation or ordinary-authoring lock. Project/Story/Session/Task navigation and resource creation remain available. | VM dirty-state transition and command enablement tests. | Edit, leave, return, create resources, and save/close/reopen in the Release EXE. | Not superseded. |
| H-DRAFT | 0.3.1.2A/3; 0.3.1.4 §19 | UI draft is distinct from canonical value: invalid/uncommitted edits do not silently enter canonical JSON; commit/cancel semantics are explicit and last input is not lost on focus change. | Draft/commit/cancel and serialization tests, including `LostFocus`. | Edit inline and Inspector fields, switch focus, cancel/commit, save/reopen, and compare displayed/canonical values. | Not superseded. |
| H-TARGETED-REFRESH | 0.3.1.3 audit #20/#21; 0.3.1.4 G8/G12 | Editing one node/Objective refreshes only the affected projection; valid focused text is committed before Save/navigation/close, while invalid staged input remains an error and does not mutate canonical JSON. | Targeted notification, source-update, and canonical-hash tests. | On a large graph edit one Objective and save with focus still in a field; inspect unaffected nodes and persisted values. | Not superseded. |
| H-PROJECT-HIERARCHY | 0.3.1.1/2B; 0.3.1.4 §19 | Project workspace is Story list / graph / selected Story Inspector; Story workspace is resource library / Flow or local graph / Inspector. | View-model composition and route tests. | Real navigation and visible three-column layout in the Release EXE. | Not superseded. |
| H-AGGREGATE | 0.3.1.0/2B; 0.3.1.4 §19 | Session/Task aggregate placement is created by dragging a resource into Story Flow; blank-canvas palette does not manufacture an empty aggregate. | Palette filtering and placement serialization tests. | Drag Session and Task from resource library into Story Flow; verify one placement and entry into local graph. | Not superseded. |
| H-RESOURCE-DELETE | 0.3.1.3; 0.3.1.4 §19 | Deleting a resource removes its placements and incident wires; deleting a placement keeps the resource. Lifecycle operation is atomic/rollback-safe. | Transaction, referential-integrity, and rollback tests. | Delete resource/placement separately, inspect wires and resource library, then save/reopen. | Not superseded. |
| H-PALETTE | 0.3.1.0/1.1/2A; 0.3.1.4 §19 | Palette hides fixed/unique/compatibility-only/aggregate nodes: Story Start, Session Start, Task Settlement, Task Activate, and aggregate Session/Task. Condition is categorized/orderly. | Palette registry/filter tests. | Open each blank-canvas add menu in Story, Session, and Task; verify exact entries and ordering. | Not superseded. |
| H-SESSION-START | 0.3.1.0/1.1; 0.3.1.4 §19 | Session fixed `【起始】` exposes Flow only; no legacy Logic Output in normal authoring. | Node schema/port registry tests. | Create a new Session in the Release EXE and inspect its Start ports and saved JSON. | The old Session Start legacy Logic Output is superseded. |
| H-TASK | 0.3.1.0/1.1; 0.3.1.4 §19 | Task is a pure Logic graph: no Flow, Start, Activate, Action, or Session nodes. | Palette/schema/cardinality tests. | Create Task, inspect palette and graph, connect only legal Logic nodes, save/reopen. | Not superseded. |
| H-SETTLEMENT | 0.3.1.0/1.1; 0.3.1.4 §19 | Exactly one fixed Settlement; named Logic inputs; uniquely generated result 1/2/3…; first true wins; rename/reorder preserves stable port IDs. | Settlement model, uniqueness, and stable-ID tests. | Create/reorder/rename results in the Release EXE and exercise the resulting visible ports after restart. | Not superseded. |
| H-OBJECTIVE-INTERACT | 0.3.1.0/3; 0.3.1.4 §19 | `角色交互` accepts Actor/Actor Group identity and has no quantity field. | Objective schema/type-specific validation tests. | Create all three objective types and verify only kill/collect show quantity. | Not superseded. |
| H-ACTOR-ID | 0.3.1.0/1.1/3 audit #25; 0.3.1.4 §19 | Individual Actor is `角色 / NPC_ID`; collective Actor is `角色组 / Group_ID`; rename changes DisplayName only. Actor resources may populate an existing node parameter slot, but never create a graph node; `interact_actor`/`enter_region` are Start-condition authoring types only. | Resource schema, rename round-trip, palette exclusion, and parameter-drop/node-count tests. | Create/rename/reopen Actors; drag one into an existing Objective parameter and verify node count/JSON; inspect Start trigger choices. | Not superseded. |
| H-ITEM-ID | 0.3.1.0/1.1; 0.3.1.4 §19 | Individual Item is `物品 / Item_ID`; collective Item is `物品组 / Group_ID`; Item ID is exact identity, Group ID uses declared matching semantics. | Item schema/membership/matching tests. | Create/rename/reopen individual and collective Items; inspect compact rows and targets. | Not superseded. |
| H-GIVE-ITEM | 0.3.1.0/1.1; 0.3.1.4 §19 | Give Item target accepts only an individual `Item_ID`; Group_ID is unavailable. | Target projection/type-filter tests. | Open Give Item in the Release EXE with both individual and group Items present; verify dropdown and save/reopen. | Not superseded. |
| H-RESOURCE-ORDER | 0.3.1.2B/3; 0.3.1.4 §19 | Resource rows support drag reorder with clear insertion preview; order persists across save/restart; no automatic name sort; owned/referenced meaning is unchanged. | Ordering projection and persistence tests. | Perform reorder in each relevant library, save/close/reopen, and inspect order and aggregate semantics. | Not superseded. |
| H-VIEWPORT | 0.3.1.1/3; 0.3.1.4 §19 | Each graph has isolated zoom/pan/fit state; navigating between Story Flow, Session, and Task does not leak viewport state. Permanent cross-process viewport persistence is not newly required by 0.3.1.4. | Viewport-controller isolation tests. | Pan/zoom each graph, navigate among Story/Session/Task, and verify each graph restores its own in-process view. | Not superseded. |
| H-NODE-LAYOUT | 0.3.1.4 §19 | Studio-only node positions/layout are authoring metadata separate from canonical RPG data and persist across a full close/restart. They must not enter Story Package or Java Runtime input. | Sidecar round-trip/isolation tests plus canonical JSON and Story Package hash/content checks. | Move multiple nodes, Save All, close Studio, reopen, and visually compare positions. | New 0.3.1.4 contract; not superseded. |
| H-THEME | 0.3.1.1/2A; 0.3.1.4 §19 | Light and Dark both keep readable borders, panel boundaries, selection, ports, wires, controls, popups, Problems, and Inspector labels/values. | Resource/theme binding and non-null style tests. | Switch themes in the Release EXE and inspect all listed surfaces at required DPI/resolutions. | Not superseded. |
| H-ERRORS | 0.3.1.1/3; 0.3.1.4 §19 | Ordinary authoring shows short Chinese plus an actionable suggestion; raw graph paths, exceptions, internal types, and field paths belong in Problems technical details. | Validation-message routing/content tests. | Trigger representative invalid edits and inspect normal UI plus Problems details in the Release EXE. | Not superseded. |
| H-ACCEPTANCE | 0.3.1.0–1.4; 0.3.1.4 §19 | Codex may produce only an RC evidence package; only the user grants `USER_ACCEPTED`. | Test/build reports can establish only automated portions and artifact identity. | User must operate the authoritative Release EXE and provide acceptance; no report may infer it. | Not superseded. |

## Broader G1–G14 regression coverage

These rows retain the detailed requirements in Work Package G. The H-* rows above
are the traceability anchors for section 19; this matrix prevents narrower tests
from silently omitting a G requirement.

| Contract ID | Source version | Current valid behavior | Automated-proof boundary | Release EXE proof boundary | Superseded status |
|---|---|---|---|---|---|
| G1-NAV | 0.3.1.1/2B/1.4 G1 | Breadcrumb is `<Project> > <Story> > <Session/Task>`; Project and Story links return to their workspaces; dirty state does not block ordinary navigation/creation; Inspector scrolls; selection is visible; resource right-click hits the resource, not its folder. | Route, command, selection, and hit-region model tests only. | Click every breadcrumb/row/menu and verify real window, scrolling, selection, and right-click behavior. | Not superseded. |
| G2-LIBRARY | 0.3.1.0/2B/1.4 G2 | Library has 角色/物品/会话/任务; rows are compact (including IDs/groups); Session/Task are one line; reorder has insertion/drop preview, persists, and is not name-sorted. | Resource projection/order/persistence tests. | Inspect all four folders, perform reorder, save/restart, and verify visible rows/preview. | Not superseded. |
| G3-LIFECYCLE | 0.3.1.3/1.4 G3 | Rename only changes DisplayName; stable resource/NPC/Item/Group/dynamic port IDs remain; resource deletion cleans placements/wires; placement deletion keeps resource; reorder does not alter owned/referenced semantics. | ID, referential-integrity, transaction, and round-trip tests. | Rename/delete/reorder through UI, then save/reopen and inspect all affected views. | Not superseded. |
| G4-PALETTE | 0.3.1.0/2A/1.4 G4 | Blank-canvas menus exclude Start/Session Start/Settlement/Activate and compatibility/aggregate nodes; aggregate placement is resource-drag only; `【条件判断】` classification/order is correct. | Registry filter/order tests. | Open each graph menu and drag aggregates from library in the Release EXE. | Not superseded; old `【条件】` label is replaced by `【条件判断】`. |
| G5-START | 0.3.1.0/1.1/2B/3/1.4 G5 | Author-facing `启动条件`, 1..N, unique defaults `启动条件 1/2/3…`; DisplayName is Flow Output label; rename keeps port ID; supported types only; no new legacy EnterStory; multiple starts are OR; cannot delete final item; region XYZ stays grouped; cards are clearly grouped. | Start schema, uniqueness, delete guard, stable-ID, and type projection tests. | Add/rename/delete/mutate start conditions, inspect dropdowns/cards/ports, save/reopen. | Legacy EnterStory authoring is superseded. |
| G6-SESSION | 0.3.1.0/1.1/2B/1.4 G6 | Independent Session graph; fixed Start Flow-only; Dialogue/Choice/Narration/Condition/Logic author; Choice ports refresh live; aggregate enter/exit ports stable; Session resource drags into Story Flow. | Node registry, dynamic-port, and aggregate serialization tests. | Author a Session and Choice, inspect live ports, and drag Session into Story Flow. | Not superseded. |
| G7-TASK | 0.3.1.0/1.1/1.4 G7 | Pure ◆ Logic graph; no Flow/Activate/Start/Action/Session. Fixed Settlement has unique named Logic slots, first-true semantics, and stable IDs through rename/reorder. | Schema, palette, semantics, and ID tests. | Author Task and Settlement in the Release EXE, then restart and inspect. | Not superseded. |
| G8-OBJECTIVE | 0.3.1.0/1.1/3/1.4 G8 | Author types are exactly 实体击杀, 物品收集, 角色交互; kill/collect use identity/group plus quantity; interact uses identity/group without quantity; no normal `minecraft:slime`/`minecraft:stone` raw authoring. | Type-specific schema/validation and migration tests. | Complete all three Objective editors and verify fields and persisted IDs. | Not superseded. |
| G9-IDENTITY | 0.3.1.0/1.1/3/1.4 G9 | Actor/Item individual and group display contracts hold; resources populate existing parameter slots rather than create nodes; Give Item only accepts Individual Item_ID, never Group_ID. | Registry/schema/filter and parameter-drop/node-count tests. | Create resources, drag Actor into an existing parameter, and inspect every target selector in the Release EXE. | Not superseded. |
| G10-GRAPH-UX | 0.3.1.1/2A/2B/3/1.4 G10 | Flow is ● circle, Logic ◆ diamond; input anchors left/output right; label length does not move anchor X; 18–20 px hit target; labels do not start wires; ghost/drop position is pointer-relative; formal wire language; scissors cursor and Left Alt; viewport isolation. | Geometry, state-machine, and viewport tests; not visual acceptance. | Real pointer/drag/cursor/ghost/wire evidence at required DPI and graph contexts. | 9 px-only target is superseded; all other listed rules remain. |
| G11-THEME | 0.3.1.1/2A/1.4 G11 | Light and Dark are both usable across nodes, panels, resources, ports, wires, controls, popups, Problems, and Inspector. | Style binding/resource tests. | Visual inspection in both themes and required window sizes/DPI. | Not superseded. |
| G12-ERRORS | 0.3.1.1/3/1.4 G12 | Normal surface is concise Chinese plus action; technical graph/exception/type/path details are Problems-only. | Message routing tests. | Trigger invalid authoring and inspect both surfaces. | Not superseded. |
| G13-PERSIST | 0.3.1.0/2B/3/1.4 G13 | Save + full restart preserves canonical nodes/connections, dynamic ports, stable IDs, resource identities/order, 0.3.1.4 node layout, Start conditions, Choice options, and Settlement order. | JSON/schema/hash/round-trip tests. | Full close/reopen in the authoritative EXE and visual/semantic comparison. | Not superseded. |
| G14-SMOKE | 0.3.1.0/1.4 G14 | From empty project: Story `史莱姆清理委托`; group `slimes`; Actor `tavern_boss`; Item `copper_coin`; Session Dialogue+Choice (≥3 options, each wired); Task kill `slimes` quantity 10 → Settlement; drag Session/Task to Story Flow; Start→Session→Task; optional Give Item uses `copper_coin`; move nodes; Save All; close/reopen; verify resources, wires, option mapping, layout, and order. | Fixture/schema and end-to-end model tests can prove data shape only. | Entire 18-step authoring smoke must be performed in the Release EXE; no Minecraft runtime is required for this Studio smoke. | Not superseded. |

## Superseded Historical Contracts

These are retained to stop old assertions from re-locking behavior that later
requirements deliberately changed.

| Historical contract | Former source | Replacement now enforced | Test consequence |
|---|---|---|---|
| Node deletion always asks for confirmation. | 0.3.1.1 C10 / early A15 evidence. | Ordinary graph placement/node: immediate delete; `Ctrl+Z` restores. Resource deletion may still confirm because it is destructive lifecycle work. | Do not assert a node-delete confirmation dialog; do assert immediate deletion and Undo. |
| Port hit target is only the ~9 px visual dot/diamond. | 0.3.1.2B B6. | Visual ≈9–11 px, hitbox ≈18–20 px, label is not a target, adjacent ports are not captured. | Do not shrink hitbox to 9 px; test the expanded bounded target. |
| Condition author-facing name is `【条件】`. | 0.3.1.0 §19 and older UI assertions. | Author-facing name is `【条件判断】`; internal stable type may remain `condition`. | Update labels/palette assertions while keeping internal type compatibility. |
| Session `【起始】` has a legacy Logic Output. | 0.3.1.0/1.1 compatibility behavior. | Normal new Session authoring exposes Flow only. | Do not restore/assert the legacy Logic port in a new Session. |
| “局部图” is the formal graph-layer term. | 0.3.1.1 navigation wording. | Use `图谱 → 流程图 → 会话图/任务图`. | Do not fail current UI for removing “局部图”; update terminology assertions. |

## Source documents inspected

- `PLAN/DarkGrey_RPG_0.3.1.0_Construction_Plan.md`
- `PLAN/DarkGrey_RPG_0.3.1.0_Development_Report_2026-08-30.md`
- `PLAN/DarkGrey_RPG_0.3.1.0_Handoff_2026-08-30.md`
- `PLAN/DarkGrey_RPG_0.3.1.1_Studio_Repair_Construction_Plan.md`
- `PLAN/DarkGrey_RPG_0.3.1.1_Development_Report.md`
- `PLAN/DarkGrey_RPG_0.3.1.1_Root_Cause_Record.md`
- `PLAN/DarkGrey_RPG_0.3.1.2A_Studio_0311_Rework_Plan.md`
- `PLAN/0.3.1.2A/BASELINE.md`
- `PLAN/0.3.1.2A/DEVELOPMENT_REPORT.md`
- `PLAN/0.3.1.2A/SECOND_PASS_ACCEPTANCE_2026-08-31.md`
- `PLAN/DarkGrey_RPG_0.3.1.2B_Studio_New_Feedback_Plan.md`
- `PLAN/0.3.1.2B/DEVELOPMENT_REPORT.md`
- `PLAN/0.3.1.2B/EVIDENCE_INDEX.md`
- `PLAN/DarkGrey_RPG_0.3.1.3_Studio_Repair_Construction_Plan.md`
- `PLAN/0.3.1.3/DEVELOPMENT_REPORT.md`
- `PLAN/0.3.1.3/ROOT_CAUSE_RECORD.md`
- `PLAN/0.3.1.3/SOURCE_AUDIT_FINAL_REVIEW.md`
- `PLAN/0.3.1.3/MANUAL_ACCEPTANCE.md`
- `PLAN/DarkGrey_RPG_0.3.1.4_Studio_Regression_Repair_Construction_Plan.md` (especially §§1.2, G1–G14, and §19)

## Uncertain or intentionally unclosed items for Main

1. This baseline resolves historical wording using the 0.3.1.4 PLAN, but it does
   not decide whether any implementation currently meets a contract. Main must
   reconcile the baseline with fresh source/test/Release EXE evidence.
2. The 0.3.1.2A reports retain a real single-Flow reconnect failure and several
   blocked manual gates; the 0.3.1.3 report retains blocked user-verification
   items. These remain evidence risks, not PASS claims.
3. The exact authoritative EXE metadata/hash for the 0.3.1.4 candidate is not
   established by this read-only baseline task; Main must record it after promotion
   before reporting delivery.
4. Legacy data migration compatibility (including old `condition` type and old
   EnterStory data) is distinct from new authoring. Main must preserve readable
   migration behavior without reintroducing superseded author-facing nodes/ports.
