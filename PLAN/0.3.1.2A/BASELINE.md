# DarkGrey_RPG 0.3.1.2A Gate A0 Baseline

## Locked baseline

- Source branch: `codex/0.3.1.1`
- Source commit: `33c1168b2d80d9225146c5339b80274624f84b40`
- Construction branch: `codex/0.3.1.2A`
- PLAN: `PLAN/DarkGrey_RPG_0.3.1.2A_Studio_0311_Rework_Plan.md`
- Baseline date: `2026-08-31` (Asia/Shanghai)

The branch was created without resetting or cleaning the worktree. Existing user-owned changes and untracked files were preserved, including `.codex/config.toml`, `AGENTS.md`, `.dotnet-home/`, and pre-existing PLAN documents.

## Fresh baseline verification

Environment:

- .NET SDK: `E:\Java\dotnet-sdk-10\dotnet.exe`
- `DOTNET_CLI_HOME`: `E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\wpf-build`
- `NUGET_PACKAGES`: `E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\wpf-build\nuget-packages`
- `TEMP` / `TMP`: `E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\wpf-build\temp`
- `windir`: inherited from `SystemRoot`

Results:

- Release solution build: **PASS**, 0 errors, 20 existing MSTest analyzer warnings.
- Core tests: **PASS**, 345 passed, 0 failed, 0 skipped.
- WPF tests: **PASS**, 350 passed, 0 failed, 0 skipped.

Raw test records:

- `evidence/baseline/test-results/Core/Core-0.3.1.1-baseline.trx`
- `evidence/baseline/test-results/Wpf/Wpf-0.3.1.1-baseline.trx`

These automated results establish only the 0.3.1.1 code baseline. They do not close any 0.3.1.2A visual or interaction Gate.

## 0.3.1.1 failure evidence retained

Input document: `PLAN/0.3.1.1审计.docx`.

The packaged DOCX renderer could not find LibreOffice on this Windows host. The document was therefore inspected through its readable text plus all 19 embedded PNGs. Microsoft Word reported 12 pages, 19 inline images, and no floating shapes. The original images were copied byte-for-byte into `evidence/baseline/0311-audit/`.

Relevant A-scope evidence:

- `audit-0311-02.png`: new-story empty state and visibly offset primary button (A1).
- `audit-0311-03.png`: abnormal white rectangle over the graph (A2).
- `audit-0311-04.png`, `audit-0311-15.png`, `audit-0311-19.png`: unreadable dark resource text and oversized resource rows (A3; row compaction itself remains B-scope).
- `audit-0311-05.png`: folder context menu appearing while a task resource is targeted (A4).
- `audit-0311-06.png`: read-only node parameter summary and ineffective collapse affordance (A5).
- `audit-0311-07.png`, `audit-0311-08.png`: blank Objective ComboBox popup and mismatched Inspector styling (A6).
- `audit-0311-10.png`: fixed Session start node still visible but disabled in the add-node menu (A7).
- The audit text records that scissors mode did not display scissors and wires still looked like preview wires (A8-A9); the static screenshots do not prove either dynamic state.
- `audit-0311-08.png`, `audit-0311-12.png`: dirty graph blocks ordinary resource operations (A10).
- The audit text records the folder collapse/expand jump (A11); static screenshots cannot prove this dynamic defect.
- `audit-0311-13.png`, `audit-0311-14.png`: ghost preview anchor and final placement visibly disagree (A12).
- `audit-0311-08.png`, `audit-0311-16.png`: new Objective uses unresolved `minecraft:slime` instead of DGR identity (A13).
- `audit-0311-18.png`: initial Enter Region trigger delete command remains disabled after a second trigger exists (A14).
- `audit-0311-11.png` and the remaining screenshots preserve 0.3.1.1 regression scenes for A15.

Items explicitly assigned to 0.3.1.2B by the A PLAN (project-level breadcrumb, rename, compact one-row resource layout, XYZ re-layout, Condition rename, port-only hit target, live Settlement port refresh, and resource ordering) are retained as audit context only and will not be implemented in A.

## Gate A0 status

`PASS` for baseline lock, branch creation, fresh build, fresh Core/WPF tests, and preservation of the 0.3.1.1 failure evidence. No A1-A15 implementation or real Release EXE verification is claimed here.
