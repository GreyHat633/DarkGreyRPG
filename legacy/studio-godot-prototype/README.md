# DarkGrey RPG Studio

Open `project.godot` with Godot 4.x. Phase 5 provides the complete authoring
workflow:

- open an RPG project directory;
- create, duplicate, edit, delete, and save Actor JSON;
- validate schema version, resource IDs, duplicate IDs, and Actor-only fields;
- atomic save through a temporary file and rollback backup;
- Ctrl+S save;
- Project / Workspace / Inspector / Output / Problems shell.
- Line, Choice, Jump, and End Dialogue nodes;
- drag reorder, duplicate, delete, Undo/Redo, search, replace, Speaker picker,
  and node target pickers.
- KillEntity, CollectItem, ReachLocation, and InteractActor Objective
  Inspectors;
- ALL, ANY, and SEQUENCE Objective Groups;
- drag reorder, duplicate, delete, Undo/Redo, atomic save, and strict scope
  validation for Quest resources;
- a GraphEdit Story canvas with pan, zoom, minimap, node dragging, port
  connections, built-in box selection, Copy/Paste, Duplicate, Undo/Redo, and
  an Add Node search covering all 16 initial Story node types;
- Find Next search across node IDs, types, and properties;
- an Inspector with Actor, Dialogue, and Quest resource pickers;
- Problems validation for missing or broken references, unconnected nodes,
  missing exits, invalid connections, and duplicate IDs;
- double-click a node-scoped Story problem to select and center its GraphNode.
- automatic localhost connection to Minecraft with an explicit
  `Minecraft Connected` status;
- Live Actors and Players plus Quest, Story Instance, variable, waiting-event,
  condition, and Why Not Triggered views;
- Current, Executed, Waiting, and Error Story Graph highlighting;
- Actor, Position, Region, Item, and Entity Type Pick modes using the in-game
  Editor Tool;
- Actor Locate through temporary particles and HUD/chat feedback;
- save-triggered runtime reload with `/dgrpg reload` as fallback;
- reversible Test/Stop sessions that snapshot and restore player RPG state;
- Content Pack build output containing project.json and all five content
  directories;
- ten-second autosave for existing resources, retained `.bak` files, and
  atomic writes.

Studio is an authoring tool only. It is not required on a production server or
normal player's client.

## Portable Windows build

The Windows export uses the repository-local Godot executable and a
repository-local Windows release template. Packaging redirects `APPDATA`,
`LOCALAPPDATA`, `TEMP`, and `TMP` into `.tooling/studio-package-env`, so it
does not install templates into or update an existing Godot configuration.

Run:

```powershell
.\scripts\package-studio.ps1
```

The single-file executable is written to
`dist/DarkGreyRPGStudio.exe`. On first launch it creates a writable blank
project beside itself at
`DarkGreyRPGProjects/darkgrey_rpg_project`. The last project path is retained
beside the executable in `DarkGreyRPGStudio.settings.json`; Studio never stores
that setting in the Godot editor configuration.
