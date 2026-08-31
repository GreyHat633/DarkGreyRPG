# DarkGrey RPG Studio 0.3.1.2A Manual Acceptance

Overall status: **0.3.1.2A PLAN A — USER_CLOSED_FOR_HANDOFF (PROVISIONAL)**

## 2026-09-01 User closure for handoff

- The user explicitly instructed that PLAN A be recorded as complete for now so the next session can continue subsequent work.
- This is a **user-authorized provisional closure**, not a claim that every historical strict Gate was re-run or that every earlier evidence gap became agent-verified.
- A1 is accepted at 125% with the unavailable 100% run waived; A2 is user-accepted on the clarified held-Alt plus middle-pan path; the replacement A8 scissors cursor is accepted for PLAN A handoff.
- Remaining historical evidence gaps in A3/A5/A6/A9/A10/A12/A13/A15 are deferred by the user and do not block this handoff status. Their original records remain below for traceability.
- This closure does not authorize commit, push, merge, tag, Release creation, or replacement of the old `dist` RC.

## 2026-09-01 A8 replacement candidate

- **A8: REPAIRED_IN_CANDIDATE / NEED_USER_VERIFICATION.** The rejected thin-line scissors artwork was replaced with a compact 32x32 pointer icon: two separated tapered blades, two small finger loops with transparent openings, a blue-gray outline that remains visible on the dark Flow canvas, and an explicit `(11,17)` cutting-pivot hotspot.
- Candidate EXE: `.tooling/0312a-cursor-icon/publish/DarkGreyRPGStudio.exe`, SHA-256 `411485E355885AAE9CD95FA31E41B5C6A9B5EF5FEB192508C28002EB1C2D41AD`.
- Validation: focused cursor payload/geometry tests 2/2 PASS; complete WPF suite 360/360 PASS; the real candidate window returned hotspot `(11,17)` and was captured at `.tooling/0312a-cursor-icon/live-scissors-cursor.png`.
- Final A8 visual acceptance remains with the user; the historical Gate A8 record below describes the earlier rejected cursor and is retained unchanged.

## 2026-08-31 User follow-up

- **A1: USER_ACCEPTED.** The user confirmed that this workstation cannot provide a separate 100% DPI run and explicitly accepted the verified 125% results as sufficient. The earlier 100% DPI evidence requirement is waived for this release candidate.
- **A2: USER_ACCEPTED.** The user manually exercised the latest candidate using the clarified gesture (hold Left Alt, then middle-mouse pan the Flow graph) and reported that the white rectangle no longer appears. The earlier BLOCKED result came from an incomplete short Alt down/up reproduction path and is superseded by this user check.
- **A8: REOPENED / USER_REJECTED.** The custom scissors cursor is functionally present, but the user rejected its current visual design as too ugly and requested a compact pointer-oriented scissors icon. A8 must be re-verified after the cursor artwork is replaced.

The historical Gate entries below are retained as the evidence state of the earlier RC and are not rewritten retroactively.

Build source SHA for every Gate below: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`.

EXE for every Gate below: `E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio-0.3.1.2A\DarkGreyRPGStudio.exe`, SHA-256 `A460437194662A3E0F4E624B0F5C541EB4A0312352506CDEA05DD64D109D2971`.

## Gate A1

Gate ID: A1 — empty-story layout
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Opened a real empty project; captured logical `1100x700` and `1700x980` windows on the 125% host; measured button and content-panel centers.
Expected: Empty-state title, description, and primary button share the main content center at both sizes and both 100%/125% DPI.
Actual: At 125%, measured center error was at most 0.5 px and both captures were visually centered. A 100% DPI session was not available.
Evidence: `evidence/live/A1-empty-project-1100x700.png`; `evidence/live/A1-empty-project-1700x980.png`; `LIVE_ACCEPTANCE_LOG.md`.
Status: BLOCKED — NEED_USER_VERIFICATION for 100% DPI.

## Gate A2

Gate ID: A2 — Alt white rectangle
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Repeated Left Alt down/up ten times while rotating focus targets among blank canvas, node, and wire-near positions.
Expected: No abnormal white rectangle or focus visual.
Actual: The defect did not reproduce in ten screenshots. PLAN explicitly requires BLOCKED when the original issue cannot be reproduced; complete focus/access-key/mouse-capture instrumentation was not recorded.
Evidence: `evidence/live/A2-alt-cycle-01.png` through `A2-alt-cycle-10.png`; `A2-alt-10-cycle-contact-sheet.png`.
Status: BLOCKED — NEED_USER_VERIFICATION.

## Gate A3

Gate ID: A3 — resource-library theme readability
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Inspected resource labels and selected state in Dark and Light while performing resource operations.
Expected: Primary/secondary text readable in Dark/Light, including selected/hover and missing/disabled when present.
Actual: Normal and selected actor/item/session/task labels were readable in both themes. A separate hover capture and a disabled/missing resource row were not produced.
Evidence: `evidence/live/A3-A11-fixed-folder-headers.png`; `A4-item-resource-context-menu.png`; `A6-light-objective-dropdown.png`.
Status: BLOCKED — NEED_USER_VERIFICATION for the uncaptured states.

## Gate A4

Gate ID: A4 — resource right-click ownership
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Right-clicked one actor, item, session, and task resource in the real resource library.
Expected: Resource menu, never folder menu.
Actual: All four showed resource-specific Edit/New/Reference/Delete entries; the item menu remained an item menu.
Evidence: `evidence/live/A4-item-resource-context-menu.png`; full menu text in `LIVE_ACCEPTANCE_LOG.md`.
Status: AGENT_VERIFIED.

## Gate A5

Gate ID: A5 — true inline node parameters
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Used Start, Objective, and Line controls directly inside unselected nodes; changed ComboBox/TextBox values; collapsed/restored Objective parameters; saved and navigated away/back.
Expected: Start, Objective, Action, and Line all edit inline; collapse changes real height; persistence survives reopen.
Actual: Start/Objective/Line worked. Line text persisted to disk; Objective height changed `319 -> 118 -> 319`. Action was not exercised live in this fixture.
Evidence: `evidence/live/A5-story-start-inline.png`; `A5-task-objective-inline-unselected.png`; `A5-session-line-direct-edit-reopened.png`; `LIVE_ACCEPTANCE_LOG.md`.
Status: BLOCKED — NEED_USER_VERIFICATION for Action and the full eight-step matrix.

## Gate A6

Gate ID: A6 — themed Inspector/ComboBox
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Opened the Objective target popup in Dark and Light themes.
Expected: Start, Objective, Action, Line, Choice, Settlement inspectors and popup content remain readable in both themes.
Actual: Objective popup content was readable in both themes; the Dark capture also covered the unresolved legacy item. The other five node types were not all opened live in both themes.
Evidence: `evidence/live/A6-dark-objective-dropdown.png`; `A6-light-objective-dropdown.png`.
Status: BLOCKED — NEED_USER_VERIFICATION.

## Gate A7

Gate ID: A7 — fixed nodes absent from authoring source
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Opened blank-canvas Add Node menus in Story, Session, and Task.
Expected: Story Start, Session 起始, Task 结算/激活, and compatibility-only definitions do not appear.
Actual: Menus showed only authoring categories; all fixed definitions were absent rather than disabled.
Evidence: `evidence/live/A7-story-add-node-menu.png`; `A7-session-add-node-menu.png`; `A7-task-add-node-menu.png`.
Status: AGENT_VERIFIED.

## Gate A8

Gate ID: A8 — real scissors cursor
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Compared Win32 cursor handles before/after clicking scissors; captured the actual current cursor icon over the graph.
Expected: Visible scissors; Alt temporarily activates it; release/leave restores cursor.
Actual: Click mode visibly used custom scissors and not Cross/Arrow. Complete Alt-temporary and graph-leave visual lifecycle was not recorded.
Evidence: `evidence/live/A8-real-scissors-cursor.png`; handle measurements in `LIVE_ACCEPTANCE_LOG.md`.
Status: BLOCKED — NEED_USER_VERIFICATION for the remaining lifecycle.

## Gate A9

Gate ID: A9 — formal live wire renderer
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Dragged a new Flow wire from `interact_detective.flow_out`, captured while held, released on blank canvas, and compared disk hashes.
Expected: Same formal Flow style during drag; cancellation removes it without persistence; reconnect reuses existing visuals.
Actual: An initial white-wire FAIL was found and fixed. Republished EXE shows formal blue Flow style and clean cancellation with unchanged JSON. Existing single-wire and multi-wire reconnect were not recorded live.
Evidence: `evidence/live/A9-wire-drag-transient.png`; `A9-wire-drag-cancelled.png`; `LIVE_ACCEPTANCE_LOG.md`.
Status: BLOCKED — NEED_USER_VERIFICATION for reconnect/multi-wire cases.

## Gate A10

Gate ID: A10 — dirty state does not lock authoring
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Navigated Story/Session/Task and created an item during the live acceptance fixture; automation executed the complete identity-preserving dirty invariant.
Expected: The exact ten-step live sequence never requests save and retains the original dirty draft.
Actual: No save-first block appeared in sampled live operations, but the full ordered live sequence was not recorded. Automated coverage is not used to elevate this Gate.
Evidence: `evidence/live/A4-item-resource-context-menu.png`; final WPF TRX.
Status: BLOCKED — NEED_USER_VERIFICATION.

## Gate A11

Gate ID: A11 — folder collapse/expand motion
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Collapsed/expanded Item folder ten consecutive times while measuring the following Session folder Y.
Expected: Instant clean collapse or layout-aware animation, with no delayed jump.
Actual: Session Y was consistently `417` collapsed and `489` expanded; max toggle call `4.571 ms`; no delayed second movement.
Evidence: metrics in `LIVE_ACCEPTANCE_LOG.md`; `evidence/live/A3-A11-fixed-folder-headers.png`.
Status: AGENT_VERIFIED.

## Gate A12

Gate ID: A12 — resource ghost/drop anchor
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Source and WPF regression covered the shared anchor at zoom/pan combinations.
Expected: Live ghost and final node keep the same mouse-relative anchor at 50/100/150% with and without pan.
Actual: No complete real-EXE five-case drag recording was produced; automation is not substituted for the live Gate.
Evidence: final WPF TRX.
Status: BLOCKED — NEED_USER_VERIFICATION.

## Gate A13

Gate ID: A13 — DGR Objective identity
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Created DGR item `evidence_token`, selected it in Objective, saved, and inspected JSON.
Expected: Empty-project actor/group/item/group and three Objectives save no new `minecraft:slime/stone` defaults.
Actual: Item target saved as `evidence_token`, required `3`, with no new `minecraft:*`; the full groups/three-objective sequence was not run live.
Evidence: `evidence/live/A13-DGR-item-saved.png`; `LIVE_ACCEPTANCE_LOG.md`; final Core/WPF TRX.
Status: BLOCKED — NEED_USER_VERIFICATION.

## Gate A14

Gate ID: A14 — remove initial Start trigger after replacement
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Added `角色交互`, selected `侦探`, invoked removal of connected `进入区域`, confirmed the warning, saved and inspected JSON.
Expected: Initial removal immediately enabled after add; removal succeeds; remaining trigger cannot be deleted as the last trigger.
Actual: Removal succeeded after a live-found WPF confirmation-boundary fix. JSON retained one `interact_actor` trigger/port and removed the original connection.
Evidence: `evidence/live/A14-new-binary-role-before-remove.png`; `A14-start-remove-connected-confirmation.png`; `A14-start-role-only-after-connected-remove.png`; `LIVE_ACCEPTANCE_LOG.md`.
Status: AGENT_VERIFIED.

## Gate A15

Gate ID: A15 — full 0311 regression acceptance
Build SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`
EXE: self-contained Release EXE identified above
Steps: Sampled node/resource menus, aggregate navigation, selected state, item menu, Start options, DGR item target, localized ports, save/reopen, plus full Core/WPF suites.
Expected: Every item listed in PLAN section 18 is re-run in the Release EXE.
Actual: Sampled cases and all automation passed, but the complete live list (including individual/collective actor creation, Choice creation, Settlement 1/2/3, cardinality and stable-ID restart matrix) was not fully recorded.
Evidence: `EVIDENCE_INDEX.md`; final Core/WPF TRX.
Status: BLOCKED — NEED_USER_VERIFICATION.

## User Gate

PLAN A is recorded as `USER_CLOSED_FOR_HANDOFF (PROVISIONAL)` by explicit user instruction on 2026-09-01. Historical Gate statuses above remain evidence records rather than current blockers. No publication action is implied.
