> 2026-09-14 最终集成更新：本文件保留工作包阶段记录；后续编译修复、真实交互及最终证据以 [DELIVERY_REPORT.md](DELIVERY_REPORT.md) 为准。阶段性待办不能覆盖最终记录，未实测部分也不自动计为通过。

# WP-E screen editor (A23/A24)

`SessionScreenEditor` now owns the bounded screen authoring surface for the
existing `layers` property. The preview remains a fixed 320x180 composition
space and the layer list has its own bounded vertical scroll area, thumbnail,
image label, and z label.

## Implemented behavior

- The selected layer exposes eight 12px resize handles: four edges and four
  corners. Edge handles change one dimension; corner handles change both.
- Resize geometry is calculated from the layer rectangle and its normalized
  anchor. The opposite edge or corner remains fixed, minimum/maximum sizes are
  enforced, and x/y/width/height are written back together.
- A move, resize, or direct list reorder starts from a detached JSON snapshot.
  Release commits one `SetScreenLayers` history unit. Escape restores the
  snapshot without creating an undo unit.
- List dragging updates array order and z values together, so list order,
  preview draw order, and persisted runtime order stay aligned.
- `对话框` and `选项框` are symmetric, read-only references. At the canonical
  320x180 design size they are centered at x=160; the choice reference is
  above the dialogue reference using the game layout's three-row choice area.

## Bounded verification

`SessionScreenEditor0331Tests` verifies the eight-handle contract, fixed-edge
resize geometry for all directions, schema size limits, and centered reference
placement. The full WPF test project currently cannot compile because the
parallel 0.3.3.1 task contract intentionally removed `RegionNote` while
`CanonicalTaskObjectiveInspectorTests` still asserts it; the task work package
owns that test update.

Live desktop drag/resize acceptance, DPI screenshots, and comparison against a
running game remain required checks for the main agent. This package does not
claim those checks or user acceptance.
