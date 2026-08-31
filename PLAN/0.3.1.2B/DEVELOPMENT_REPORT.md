# DarkGrey RPG Studio 0.3.1.2B Development Report

Date: 2026-09-01
Branch: `codex/0.3.1.2B`
Baseline: `9c1e0b0cc6f41947504831fcf9f1fc2cae2b2c52` (`fix(studio): finalize accepted 0.3.1.2A handoff`)
Delivery state: **0.3.1.2B Release Candidate; not USER_ACCEPTED**

## Implemented scope

- B1: project-level breadcrumb with non-destructive Project Home routing and retained dirty canonical workspace.
- B2: display-name-only rename for Actor, Item/Item Group, Session, and Task; stable IDs and membership remain unchanged; aggregate names synchronize.
- B3: Enter Region fields use dimension, one-row X/Y/Z, then radius in Inspector and inline editors.
- B4: user-facing `条件` is now `条件判断` while serialized type stays `condition`.
- B5: resource rows are compact one-line rows; Actor and Item identity labels are exact; Session/Task do not render a second identity line.
- B6: only the circular/diamond port anchor begins a wire drag; label clicks remain ordinary selection input.
- B7: targeted port refresh updates only the changed node and its incident connections, preserving node visual identity, selection, and viewport.
- B8: same-folder resource drag ordering is persisted in canonical membership schema 3 `display_order`; schema 1/2 shapes remain exact until ordering is used; stale deleted handles are filtered and new resources append.
- Product and window version labels updated to `0.3.1.2B`.

## Compatibility and persistence boundaries

- Existing ownership arrays remain authoritative and are not reordered by the display-order feature.
- Item lifecycle operations preserve the original membership schema instead of forcing an ordering-schema upgrade.
- Schema 1 and schema 2 serialization retain their exact historical root shape.
- Session/Task rename updates persisted Story aggregate labels transactionally and keeps active unsaved graph state.
- Port changes publish affected node IDs; the WPF view refreshes only those node ports and incident wire visuals.

## Verification

- Release build: PASS, 0 warnings, 0 errors.
- Core tests: 349/349 PASS.
- WPF tests: 368/368 PASS.
- Focused coverage includes breadcrumb retention, four-type rename, schema compatibility, ordering reload/delete-create behavior, anchor-only hit testing, and targeted dynamic-port refresh.
- Standalone Release EXE: B-S1 through B-S8 AGENT_VERIFIED; see `MANUAL_ACCEPTANCE.md`.

## Candidate artifact

- Path: `.tooling/0312b-acceptance/publish/DarkGreyRPGStudio.exe`
- ProductVersion: `0.3.1.2B`
- SHA-256: `13A993D978A37332DF41C66B4EF21B139CD64CD1EA78A9D8C07E9128CAA1FF55`

## Delivery boundary

- This report does not claim `0.3.1.2B 已完成`, `Studio 已冻结`, or `Studio 最终可用`.
- No merge, tag, or GitHub Release is authorized or performed.
- User acceptance remains required before `USER_ACCEPTED` or the later 0.3.2.0 phase.
