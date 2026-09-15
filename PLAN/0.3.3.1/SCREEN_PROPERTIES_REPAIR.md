# Screen image properties and Inspector resizing

The graph toolbar now wraps instead of forcing a minimum viewport width. Inspector resizing reduces the graph column. Image-list clicks explicitly select before mouse capture, and node/Inspector editors share the selection per host/node.

Selected-image properties use a bordered card with name, layer, position, size and anchors. Names are persisted as Studio-only sidecar metadata keyed by resource and node, with geometry snapshots retaining names through reorder/undo; Runtime layer schema is unchanged. Name changes participate in host undo/redo. Long list names and card titles truncate without widening the editor.

Numeric fields display at most two decimals while unfocused and expose the full double-precision value for editing. Focus/blur does not write a rounded value back. Image movement snaps edges/centres to the screen and other images within four canonical preview pixels, shows dashed guides, and supports Alt bypass. Numeric drag previews remain live; each gesture commits one history unit.

Validation: 553 WPF tests passed, zero failures/skips (screen-properties-tests-final.log). Tests cover shared selection, name undo/redo/reopen/reorder, unchanged Runtime schema, full-precision manual input, screen/image snapping, bypass, resizing and existing gesture/history behavior.

Real-client checks used isolated .tooling/0.3.3.1/screen-properties-project. Clicking image two changed both editors to X=1.2. Entering X=0.623456789012345 displayed 0.62 in both editors while JSON retained 0.623456789012345. Renamed image to 前景人物, saved and restarted the client; name survived. Dragged to the screen centre with dashed guides visible and X displayed as 0.5 in both editors. Fixed a focus-transfer/canvas-rebuild interruption found during this real drag test. Inspector widening visibly reduced graph width. Evidence: evidence/live/screen-snap-final.png and screen-properties-final-card.png.

Delivered self-contained Windows x64 Release to dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe. ProductVersion 0.3.3.1; 141952214 bytes; SHA-256 3F5BAD930C634E7580B559EFBF659D7BB771382D801276C028424C7189B7340D.

USER_ACCEPTED=NO. No GitHub publication performed for this repair.
