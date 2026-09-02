# DGRS v1 Story Package Format

DGRS is the compiled, single-file delivery format for one DarkGrey RPG Story. It is not a Studio source-project format and cannot be used to reconstruct authoring layout or editor state.

## Physical container

- Extension: `.dgrs`
- Container: ZIP-compatible archive using Deflate-compatible compression
- Entry names: UTF-8, `/` separators, relative file paths only
- Rejected names: absolute paths, drive-qualified paths, `.` or `..` segments, empty segments, backslashes, and duplicate normalized paths

## Format identity

The root `manifest.json` is mandatory. DGRS v1 requires:

```json
{
  "format": "dgrs",
  "format_version": 1,
  "producer": "DarkGreyRPGStudio",
  "producer_version": "0.3.2.0"
}
```

These fields extend the existing Story Package manifest. The existing `schema_version`, package/story identity, package version, and `required_resources` fields remain authoritative for the logical payload.

## Entry layout

The logical payload preserves the established Story Package directory layout inside the archive:

```text
manifest.json
project.json
actors/*.json
dialogues/*.json
quests/*.json
stories/*.json
items/*.json
item_groups/*.json
resources/canonical/stories/*.json
resources/canonical/memberships/*.json
resources/canonical/sessions/*.json
resources/canonical/tasks/*.json
resources/story_logic_graph.json
```

Only resources named by the selected Story boundary are included. Empty legacy roots may be omitted from the ZIP because ZIP archives carry files rather than product-visible staging directories. Studio-only editor layout files are never package entries.

For a canonical-only Story, `required_resources.story` points at its canonical Story entry. A package may include a legacy Story entry, a canonical Story entry, or both, but `story_id` must resolve to the selected Story in the included payload.

## Validation and transaction

Before Studio reports success it must:

1. compile/materialize the selected Story into private staging;
2. write a temporary `.dgrs` beside the destination;
3. close and reopen the archive;
4. validate the manifest identity/version, safe unique entry paths, every required entry, and canonical graph payload;
5. atomically replace the destination only after validation succeeds.

A failed export does not replace a previous valid package. Temporary archive and staging paths are best-effort cleaned. Corrupt ZIPs, missing manifests, unsupported versions, unsafe or duplicate paths, missing required resources, and invalid canonical graphs are rejected.

## Compatibility policy

- DGRS v1 readers must reject unknown `format_version` values.
- Studio projects from 0.3.1.x remain ordinary source directories.
- Future readers may add support for later versions without weakening v1 validation.
- DGRS v1 does not provide signatures, encryption, DRM, incremental patches, multi-volume archives, or source-project recovery.
