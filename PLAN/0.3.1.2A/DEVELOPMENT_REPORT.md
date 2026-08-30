# DarkGrey RPG Studio 0.3.1.2A Development Report

## Result

`0.3.1.2A RC — NEED_USER_VERIFICATION`

All A1-A15 work packages are implemented in source. Automated regression and the standalone Release EXE pass the recorded cases, but the PLAN's complete user-facing matrix is not fully closed. No A-scope failure is labeled PASS, and no `USER_ACCEPTED` claim is made.

## Source and artifact

- Baseline: `codex/0.3.1.1` at `33c1168b2d80d9225146c5339b80274624f84b40`.
- Branch: `codex/0.3.1.2A`.
- Build source SHA: `34105dbbfac109df0b3db5f6ac4b8f605e6be98b`.
- EXE: `dist/DarkGreyRPGStudio-0.3.1.2A/DarkGreyRPGStudio.exe`.
- File/Product version: `0.3.1.2` / `0.3.1.2A`.
- Size: `141338326` bytes.
- SHA-256: `A460437194662A3E0F4E624B0F5C541EB4A0312352506CDEA05DD64D109D2971`.

## Implementation matrix

| Package | Source status | Main result |
|---|---|---|
| A1 | IMPLEMENTED | Empty-project primary action is centered in the actual main-content column. |
| A2 | IMPLEMENTED | Graph/viewport/node focus visuals are suppressed; Alt lifecycle is handled without a focus rectangle. |
| A3 | IMPLEMENTED | Resource primary/secondary text uses theme brushes with selected contrast. |
| A4 | IMPLEMENTED | Folder right-click is header-only; resource items own their context menu. |
| A5 | IMPLEMENTED | Start/Objective/Action/Line use real inline editors; only the header is a drag zone; parameter collapse changes node height. |
| A6 | IMPLEMENTED | Inspector TextBox/ComboBox/items/buttons and popups inherit Studio theme styles. |
| A7 | IMPLEMENTED | Fixed and compatibility-only definitions are removed at the authoring registry source. |
| A8 | IMPLEMENTED | Custom scissors cursor replaces `Cursors.Cross`; click and temporary Alt modes share it. |
| A9 | IMPLEMENTED | Existing reconnects reuse formal paths; new transient connections use the normal formal kind style. |
| A10 | IMPLEMENTED | Dirty means unsaved, not locked; resource mutations/navigation keep editor identity and drafts. |
| A11 | IMPLEMENTED | Folder Expander uses instantaneous layout without the pseudo-animation jump. |
| A12 | IMPLEMENTED | Ghost and final placement share one pointer-anchor transform across zoom/pan. |
| A13 | IMPLEMENTED | New Objective defaults are blank/DGR-selectable; legacy `minecraft:*` remains loadable only as unresolved compatibility data. |
| A14 | IMPLEMENTED | Add/remove refreshes command state; WPF confirmation boundary now covers referenced Start triggers and Task results as well as Choice. |
| A15 | IMPLEMENTED | Regression tests were expanded around the repaired 0311 paths. |

No 0.3.1.2B rename, compact-row, project-breadcrumb, XYZ-layout, condition-rename, port-only-hit-target, live Settlement refresh, or resource-ordering work was added.

## Verification

- Core tests: **PASS**, 347 passed, 0 failed, 0 skipped.
- WPF tests: **PASS**, 355 passed, 0 failed, 0 skipped.
- Release solution build: **PASS**, 0 errors; 20 pre-existing MSTest analyzer warnings were emitted on a cleanly recompiled test project.
- Self-contained win-x64 single-file publish: **PASS**.
- Real EXE acceptance: recorded in `MANUAL_ACCEPTANCE.md` and `LIVE_ACCEPTANCE_LOG.md`.

The final WPF suite includes live-found regression coverage for folder-header rendering, referenced Start-trigger confirmation injection, and formal Flow style on a new uncommitted wire.

## Defects found by live acceptance

1. The new instantaneous Expander template initially failed to project its Header into the ToggleButton, hiding all folder labels. The template now binds `Header`/`HeaderTemplate`, and both WPF regression and real EXE evidence pass.
2. Referenced Start-trigger removal had no WPF confirmation callback in inline editors, so Core correctly failed closed. The view now injects Choice/Task/Start confirmations into selected and inline inspectors; real EXE confirmation/removal passes.
3. A new Flow drag still used the selected white color, reproducing the forbidden preview look. New uncommitted wires now use the normal formal Flow/Logic style; the republished EXE shows blue Flow geometry and clean cancellation.

Available intermediate failure screenshots are retained under `evidence/live/diagnostics/` and excluded from passing evidence.

## Remaining manual Gate

The following need user verification or a later fully instrumented acceptance run before `USER_ACCEPTED`:

- A1 at 100% DPI.
- A2 original Alt defect reproduction on the user's interaction path.
- A3 hover and disabled/missing resource states.
- A5 Action inline editing and the complete movement/persistence matrix.
- A6 all six Inspector node types in both themes.
- A8 temporary Alt and graph-leave cursor lifecycle.
- A9 existing single-wire and multi-wire reconnect recordings.
- A10 exact ten-step dirty-authoring sequence.
- A12 five live zoom/pan ghost/drop cases.
- A13 full empty-project groups and three-Objective sequence.
- A15 the complete section-18 real-EXE regression list.

A4, A7, A11, and A14 are `AGENT_VERIFIED`; other Gate statuses and partial evidence are detailed in `MANUAL_ACCEPTANCE.md`.

## Delivery boundary

- The RC branch may be pushed.
- Do not merge, tag, or create a GitHub Release from this report.
- Do not begin 0.3.1.2B until the user explicitly says `0.3.1.2A 通过`.
- User-owned `.codex/config.toml`, `AGENTS.md`, `.dotnet-home/`, and unrelated pre-existing PLAN files remain outside the RC commits.
