# DarkGrey RPG Studio 0.3.1.2B Manual Acceptance

Date: 2026-09-01
Candidate status: **0.3.1.2B Release Candidate**
Agent result: **B-S1 through B-S8 AGENT_VERIFIED**
User result: **NEED_USER_VERIFICATION — USER_ACCEPTED has not been granted**

## Candidate boundary

- EXE: `.tooling/0312b-acceptance/publish/DarkGreyRPGStudio.exe`
- ProductVersion: `0.3.1.2B`
- Size: `141365974` bytes
- SHA-256: `13A993D978A37332DF41C66B4EF21B139CD64CD1EA78A9D8C07E9128CAA1FF55`
- Runtime: self-contained, single-file, `win-x64`, Release configuration.
- Project: isolated copy at `.tooling/0312b-acceptance/project`.
- Method: the standalone EXE was operated through native Windows UIAutomation and Win32 mouse, keyboard, window, and screen-capture APIs. No WPF test host supplied the live results.

## Gate record

| Gate | Status | Real Release evidence and result |
|---|---|---|
| B-S1 Breadcrumb | AGENT_VERIFIED | Entered Story then Session, created an unsaved Line, clicked the project crumb, and returned to the same Story. Project Home opened without a save-first prompt; the same Session, dirty marker, and generated node ID remained. Evidence: `B-S1-session-breadcrumb.png`, `B-S1-dirty-session-before-project.png`, `B-S1-project-home-with-dirty-retained.png`, `B-S1-return-same-story-dirty-session.png`. |
| B-S2 Rename | AGENT_VERIFIED | Renamed Actor `detective`, Item `evidence_token`, Session `final_confrontation`, and Task `evidence`. Their IDs remained unchanged on disk; Story aggregate display names changed immediately; names survived restart. The dialog exposed the stable ID as read-only context. Evidence: `B-S2-actor-rename-dialog.png`, `B-S2-four-type-renamed.png`, `B-S8-after-restart-order-retained.png`. |
| B-S3 Region Layout | AGENT_VERIFIED | Changed the Story Start trigger to Enter Region. Inspector and inline editor both rendered dimension separately, X/Y/Z on one row, and radius below. The exact compact capture used `GetDpiForWindow=119` and a logical `1100x700` window. Evidence: `B-S3-region-inspector.png`, `B-S3-region-inline.png`, `B-S3-119dpi-logical-1100x700.png`. |
| B-S4 条件判断 | AGENT_VERIFIED | Story and Session palettes both displayed `条件判断`; newly created nodes displayed `条件判断`. Saved JSON retained stable internal type `condition`. Evidence: `B-S4-story-palette-condition.png`, `B-S4-story-condition-node.png`, `B-S4-session-palette-condition.png`, `B-S4-session-condition-node.png`. |
| B-S5 Compact Resource | AGENT_VERIFIED | A production-Core-generated isolated fixture contained exactly 10 Actor rows, 10 combined Item/Item Group rows, 10 Session rows, and 10 Task rows. Native UIAutomation enumerated all rows at 37 px height; Release screenshots show the compact one-line layout and scrolling. Evidence: `B-S5-four-folders-10-items.png`, `B-S5-item-session-10-items.png`, `B-S5-session-10-items.png`, `B-S5-task-10-items.png`. |
| B-S6 Port Hit | AGENT_VERIFIED | Clicked the Start output label 20 times; no wire draft appeared and the persisted graph hash remained unchanged. Dragging from the circular anchor to the Line input anchor created and saved `start.flow_out -> generated_line.flow_in`. Evidence: `B-S6-after-20-label-clicks.png`, `B-S6-anchor-drag-connected.png`. |
| B-S7 Dynamic Port | AGENT_VERIFIED | Without leaving the active resource: Choice add created ports and enlarged the node immediately; rename, upward reorder, and delete immediately changed the same node. Story Start add created a second output immediately. Task Settlement add created `结果 4` immediately. Evidence: `B-S7-choice-*.png`, `B-S7-start-*.png`, `B-S7-settlement-*.png`. |
| B-S8 Ordering | AGENT_VERIFIED | Dragged Actor `slimes` ahead of `detective`; UI order changed immediately. Membership upgraded to schema 3 with `display_order.actors = [slimes, detective, reaccept_actor]` while owned IDs stayed `[detective, reaccept_actor, slimes]`. After clean close and restart, the dragged order was retained. Evidence: `B-S8-before-order-drag.png`, `B-S8-after-order-drag.png`, `B-S8-after-restart-order-retained.png`. |

## Automated support evidence

- Core Release suite: **349/349 PASS**, 0 failed, 0 skipped.
- WPF Release suite: **368/368 PASS**, 0 failed, 0 skipped.
- Release Studio build: **PASS**, 0 warnings, 0 errors.
- TRX files: `evidence/test-results/Core/Core-0.3.1.2B.trx` and `evidence/test-results/Wpf/Wpf-0.3.1.2B.trx`.

Automated tests support IMPLEMENTED status only. The Gate statuses above come from the standalone Release EXE interactions.

## Final status separation

- AGENT_VERIFIED: B-S1, B-S2, B-S3, B-S4, B-S5, B-S6, B-S7, B-S8.
- BLOCKED: none known in the B Gate scope.
- NEED_USER_VERIFICATION: the complete `0.3.1.2B Release Candidate`; only the user may grant `USER_ACCEPTED`.
