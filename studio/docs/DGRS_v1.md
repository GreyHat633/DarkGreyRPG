# DGRS v1 Story Package Format

DGRS is the user-facing single-file Story package format exported by DarkGrey RPG Studio. Version 1 preserves the existing logical Story package layout while adding an explicit container identity, versioned manifest, path rules, and reopen validation.

## Physical container

A `.dgrs` file is a standard ZIP-compatible compressed container using UTF-8 entry names and Deflate-compatible compression. The custom format is defined by the `.dgrs` product extension, the root manifest contract, the entry semantics below, and DGRS validation. Renaming an arbitrary ZIP file does not make it a valid DGRS package.

Writers emit entries in ordinal path order and currently assign the stable ZIP timestamp `2000-01-01T00:00:00Z` to support reproducible package construction.

## Format identity and manifest

Every package contains `manifest.json` at the archive root. JSON text and canonical resource text are UTF-8; the Studio writer emits UTF-8 without a BOM and LF line endings for the manifest.

Required manifest fields:

```json
{
  "format": "dgrs",
  "format_version": 1,
  "producer": "DarkGreyRPGStudio",
  "producer_version": "0.3.2.0",
  "schema_version": 1,
  "package_id": "kill_slimes",
  "package_version": "0.3.2.0",
  "story_id": "kill_slimes",
  "story_schema_version": 1,
  "required_resources": {
    "story": "resources/canonical/stories/kill_slimes.json",
    "actors": [],
    "items": [],
    "item_groups": [],
    "dialogues": [],
    "quests": [],
    "canonical_stories": [],
    "canonical_memberships": [],
    "sessions": [],
    "tasks": []
  }
}
```

`required_resources.story_logic_graph` is optional and is omitted when absent. The manifest parser is strict: unknown fields are rejected, identifiers must be stable resource IDs, required versions must be supported, and every listed resource path must be safe and unique within its list.

## Entry layout

The root always contains:

```text
manifest.json
project.json
```

The remaining entries are the resource closure declared by `required_resources`. Depending on the selected Story, supported paths include:

```text
actors/<id>.json
items/<id>.json
item_groups/<id>.json
dialogues/<id>.json
quests/<id>.json
stories/<id>.json
resources/canonical/stories/<id>.json
resources/canonical/memberships/<story-id>.json
resources/canonical/sessions/<id>.json
resources/canonical/tasks/<id>.json
resources/canonical/story-logic/<id>.json
```

The writer does not include editor-only caches, temporary files, staging directories, or a same-name export directory in the final package.

## Path and encoding rules

Archive entry and manifest resource paths:

- use `/` as the only separator;
- are relative and non-empty;
- contain no drive prefix or `:`;
- contain no empty, `.` or `..` segment;
- are unique after case-insensitive normalized-path comparison.

Backslashes, absolute paths, traversal paths, duplicate normalized paths, invalid UTF-8 JSON text, and missing required entries are rejected.

## Validation behavior

A conforming DGRS v1 validator:

1. opens the archive as ZIP;
2. indexes every entry and applies the path rules;
3. requires and strictly parses `manifest.json`;
4. requires `project.json` and every path declared by `required_resources`;
5. checks canonical membership and resource identity against each entry path;
6. opens Story, Session, and Task graphs under their declared scopes;
7. runs the canonical graph scope and node-shape validators;
8. validates legacy Story payloads when `required_resources.story` points under `stories/`;
9. rejects corrupt archives, unsupported format/version, wrong producer, incomplete closures, and invalid payloads.

Validation is fail-closed. Export first materializes package staging, writes a temporary `.dgrs`, closes and reopens it through the validator, and only then atomically moves/replaces the final file. A failed rebuild does not delete or overwrite the previous successful package, and staging/temporary output is cleaned on failure.

## Compatibility policy

- DGRS `format_version` governs the physical/package contract. This document defines only version `1`.
- `schema_version` and resource-specific schema versions govern manifest and payload evolution independently.
- Readers must reject unsupported `format_version` values rather than guessing.
- Compatible v1 extensions require an explicit reader/writer change because the current manifest parser rejects unknown fields.
- Legacy Story payloads remain valid only when explicitly referenced by the manifest and when they pass legacy validation.
- Minecraft-side consumption is outside the 0.3.2.0_A implementation boundary; future readers should implement this contract and validate equivalent fixtures rather than infer behavior from the ZIP extension alone.

## Non-goals

DGRS v1 does not define signatures, encryption, DRM, a remote package repository, incremental patches, multi-volume archives, a custom compression algorithm, or reverse reconstruction of a Studio authoring project.
