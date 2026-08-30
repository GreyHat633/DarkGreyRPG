# DarkGrey RPG Studio 0.3.1.2A Evidence Index

## Automated records

- Baseline Core: `evidence/baseline/test-results/Core/Core-0.3.1.1-baseline.trx` — 345/345.
- Baseline WPF: `evidence/baseline/test-results/Wpf/Wpf-0.3.1.1-baseline.trx` — 350/350.
- Final Core: `evidence/final/test-results/Core/Core-0.3.1.2A-final.trx` — 347/347, SHA-256 `43A8F677EC35900A3497A81B44FF90B570B4B85AE56876EAE06D7F1ED90C46A2`.
- Final WPF: `evidence/final/test-results/Wpf/Wpf-0.3.1.2A-final.trx` — 355/355, SHA-256 `95E90F1A3651966A33B67A32DB21803C6DB4E835007717AAA9FEB1C4A3BF0114`.

## Live Release EXE evidence

| Gate | Evidence |
|---|---|
| A1 | `A1-empty-project-1100x700.png`, `A1-empty-project-1700x980.png` |
| A2 | `A2-alt-cycle-01.png` through `A2-alt-cycle-10.png`, `A2-alt-10-cycle-contact-sheet.png` |
| A3/A11 | `A3-A11-fixed-folder-headers.png` |
| A4 | `A4-item-resource-context-menu.png` plus menu text in `LIVE_ACCEPTANCE_LOG.md` |
| A5 | `A5-story-start-inline.png`, `A5-task-objective-inline-unselected.png`, `A5-session-line-direct-edit-reopened.png` |
| A6 | `A6-dark-objective-dropdown.png`, `A6-light-objective-dropdown.png` |
| A7 | `A7-story-add-node-menu.png`, `A7-session-add-node-menu.png`, `A7-task-add-node-menu.png` |
| A8 | `A8-real-scissors-cursor.png` |
| A9 | `A9-wire-drag-transient.png`, `A9-wire-drag-cancelled.png` |
| A13 | `A13-DGR-item-saved.png` |
| A14 | `A14-new-binary-role-before-remove.png`, `A14-start-remove-connected-confirmation.png`, `A14-start-role-only-after-connected-remove.png` |

All paths above are relative to `PLAN/0.3.1.2A/evidence/live/` unless otherwise stated.

## Baseline audit evidence

`evidence/baseline/0311-audit/audit-0311-01.png` through `audit-0311-19.png` are byte-for-byte copies of the 19 embedded screenshots from the 0.3.1.1 audit DOCX. Their routing to A1-A15 is recorded in `BASELINE.md`.

## Diagnostics excluded from PASS

`evidence/live/diagnostics/` contains intermediate failure captures. They document defects found during acceptance and are intentionally not used to claim a passing Gate.

## Artifact

- Local EXE: `E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio-0.3.1.2A\DarkGreyRPGStudio.exe`
- File version: `0.3.1.2`
- Product version: `0.3.1.2A`
- Size: `141338326` bytes
- SHA-256: `A460437194662A3E0F4E624B0F5C541EB4A0312352506CDEA05DD64D109D2971`

The EXE is larger than GitHub's normal Git object limit and `dist/` is not committed. No tag or GitHub Release was created because the PLAN forbids automatic publication.
