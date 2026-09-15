# Viewport resize and image position follow-up

The user rejected the earlier border-only resize validation and requested research into established editors. The accidentally chosen auto-shrink option is explicitly disregarded.

Primary references consulted:
- Qt QGraphicsView resizeAnchor: https://doc.qt.io/qt-6/qgraphicsview.html#resizeAnchor-prop . Qt explicitly supports AnchorViewCenter; it is an option, not the default. Default NoAnchor keeps the scene position unchanged.
- React Flow resize handler: https://github.com/xyflow/xyflow/blob/main/packages/react/src/hooks/useResizeHandler.ts . Container resize updates dimensions, not automatically fitting all nodes.
- Godot GraphEdit: https://github.com/godotengine/godot/blob/master/scene/gui/graph_edit.cpp . Resize updates scrollbars/minimap/connection layer; zoom is a separate operation.
- Figma positions: https://help.figma.com/hc/en-us/articles/360039956914-Adjust-alignment-rotation-position-and-dimensions . Image/layer X/Y use the top-left bounds.

Decision: use centre preservation at unchanged zoom for visible graph viewport resizing. This is an adaptation of Qt's explicit supported policy to the user's feedback, not a claim that every mature editor uses the same default. Fit-all remains an explicit command. Graph data and node layout coordinates must not change when a sidebar changes width.

Image editor: remove anchor X/Y controls. X/Y expose top-left coordinates by subtracting legacy anchor times dimensions. Editing X/Y converts back to the existing Runtime representation; editing width/height preserves top-left coordinates. Opening legacy data is read-only, with no migration or automatic movement. New imports use a fixed zero anchor and the same initially centred image rectangle as before. Runtime schema remains unchanged.

Validation: full WPF suite passed 559/559, zero failures or skips. Log: .tooling/0.3.3.1/viewport-top-left-tests.log. Tests verify actual node transforms during resize, stable zoom/size, unchanged graph, graph camera swaps, both splitters, hidden anchor controls, no-write legacy opening, top-left editing and width resizing for anchors (0,0), (0.5,0.5), (0.2,0.7), precision and undo.

Real client: ran the authoritative dist EXE with isolated .tooling/0.3.3.1/viewport-anchor-project. Dragging Inspector left by 100 pixels changed graph width from 987 to 887, node X from 746 to 696, and node width stayed 288. Runtime resource SHA-256 stayed C8AAECE9BFECBCC9AA68C8DDB2BA25A55D99C2E2C35917BB21A29543DC94E97E. Bounds evidence: viewport-real-bounds.json and library-real-bounds.json in the tooling directory. Removed-anchor UI had zero anchor controls. Editing legacy image width to 0.6 kept its left coordinate at 0.246032511365201; both editors displayed X=0.25. Changes were saved and undone in the fixture. Screenshots: evidence/live/viewport-centre-final.png and top-left-inspector-final.png.

Delivery: dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe, self-contained Windows x64 Release; ProductVersion 0.3.3.1; 141956310 bytes; SHA-256 697F130C8BE0388E82799696F71397A0C7AB560997225A50280157712824CB3B.

USER_ACCEPTED=NO. These are implementation/test results, not personal user acceptance. No GitHub push performed for this follow-up.
