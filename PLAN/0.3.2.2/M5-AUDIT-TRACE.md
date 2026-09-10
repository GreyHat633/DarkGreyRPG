# M5 audit trace (read-only baseline investigation)

Date: 2026-09-11. Scope: locate the audited Task package, identify the actual
objective graph, and trace runtime state through persistence and UI projection.
No source, package, world, or build output was changed by this investigation.

## Governing M5 requirement

`PLAN/DarkGrey_RPG_0.3.2.2_Construction_PLAN.md:482-539` requires the trace

```text
Task event -> CanonicalTaskRuntime.accept() -> refreshState()
-> objective status map -> CanonicalTaskInstance snapshot
-> CanonicalTaskSavedData -> CanonicalTaskJournalProjector
-> Task UI projection -> server push -> CanonicalTaskClientStore
-> GuiCanonicalTaskScreen
```

and explicitly says to use the exact audited `.dgrs` when available, while
separating authored graph, runtime, persistence, projection, synchronization,
cache, and rendering causes.

## Package candidates and provenance

### Recorded 0.3.2.1 real-machine package (strongest audit provenance)

The exact package passed to the documented 0.3.2.1 real-machine command is:

```text
.tooling/0.3.2.0_B2/real-minecraft-20260903/story-packages/kill_slimes.dgrs
size 5559
SHA-256 9D8DA59B83CA61F47E9CB98B2C969F053EB3EBDF80D764AB9C5A542AA64089EC
```

Evidence: `PLAN/0.3.2.1/DEVELOPMENT_STATUS.md:32-39` names this exact
`-PdgrsPath`; `PLAN/0.3.2.0_B2_DEVELOPMENT_REPORT.md:54-62` identifies the
same package as the frozen exact source and records the same hash.

The isolated live copy used by the 0.3.2.1 harness is:

```text
.tooling/0.3.2.1/live-client/darkgrey_rpg_story_packages/kill_slimes.before-final-fixture
size 5559
SHA-256 9D8DA59B83CA61F47E9CB98B2C969F053EB3EBDF80D764AB9C5A542AA64089EC
```

The live file named `kill_slimes.dgrs` is a later 5570-byte copy with
SHA-256 `9B55012BC439DE7B0B272EBD8DBDB960A7218C08C146F9B456D95E9BF518A110`.
Its task and story graph entries are identical to the 5559-byte source; only
`resources/canonical/sessions/tarven_session.json` differs (the harness added
dialogue fixture content). This is an audit-run copy, not a new Task graph.

### Namespaced package present in the user run directory (candidate, not proven
to be the embedded audit document's archive)

```text
run/client/darkgrey_rpg_story_packages/kill_slimes.dgrs
size 6271
SHA-256 82E289D8777C869631B0BA7F96985D2FE1CF0475C5396E1C11AE38C3C0928E03
manifest package GreyHat_:test_story, task GreyHat_:KillSlimes
```

This candidate contains the `GreyHat_:TarvenBoss`, `GreyHat_:Slimes`, and
`GreyHat_:CopperCoin` identities mentioned in `PLAN/0.3.2.1审计.docx` (SHA-256
`2BBFB7066DE0FCF2456D2C2549C762A76A0C9EAE77C9112936E1CB96B58FAEA6`,
2,156,309 bytes). `run/client/logs/fml-client-latest.log:1807` records
`GreyHat_:test_story` loaded from the run client, and lines 1960-1965 record
`GreyHat_:KillSlimes` start plus three changed kill events. This establishes a
strong current-run match, but the document contains no archive filename or
hash, so it does not cryptographically prove that this package produced every
embedded audit image. Do not merge this candidate's provenance with the
document without a direct archive/image link.

Other similarly named archives are fixtures or lifecycle copies, not additional
provenance:

| Path | Size | SHA-256 | Finding |
| --- | ---: | --- | --- |
| `.tooling/0.3.2.0_B3/real-machine-20260904/story-packages/live/kill_slimes.dgrs` | 5561 | `CCDBF83520CEC4D6626318437990489860ECD580298F31011E72BFD6610DC670` | B3 copy; same two-objective graph |
| `.tooling/0.3.2.0_B4/real-machine-20260908/installed-packages/kill-slimes.dgrs` | 6134 | `62E27509356143FDEB7445D1465DC8D372DB738045276FDA978880956C41A48C` | B4 namespaced migration/live fixture |
| `.tooling/0.3.2.0_B4/migrated-packages/x4d6967726174696f6e50726f6265_x6b696c6c5f736c696d6573.dgrs` | 6453 | `E65C87604985DF406F1B3FC13D2514B14DFB4CE93730C70390C4211FEF0BD0FD` | migration probe, package `MigrationProbe:kill_slimes` |
| `.tooling/0.3.2.0_B2/real-minecraft-20260903/package-lifecycle-backup/corrupt.dgrs` | 25 | `009C6022F91853ED0A965AE1DA3BAA0F82142F0293AF1F765362EAEFA2E22BED` | intentionally corrupt; not a source candidate |

The B3/B4 and migration archives have similar text, but their names and
fixture role do not establish that they produced the user's audit.

## Authored graph evidence

The recorded 0.3.2.1 package's internal entries are:

```text
resources/canonical/tasks/kill_slimes.json
resources/canonical/stories/kill_slimes.json
resources/canonical/memberships/kill_slimes.json
```

Task resource `kill_slimes`, title `消灭3只史莱姆`, has these objective nodes:

| Node ID | Type | Description in archive | Target | Required | Initial gate |
| --- | --- | --- | ---: | ---: | --- |
| `node_2c0b762cdddd433d8277c3cd4f0e88af` | `kill_entity` | `消灭史莱姆` | entity/group `slimes` | 3 | active (no prerequisite gate) |
| `node_0d58d6b0697d4ad4a64dd7416ebd669e` | `interact_actor` | `消灭史莱姆` | actor `tarven_boss` | 1 (implicit) | inactive until prerequisite |

Edges are:

```text
node_2c0b762cdddd433d8277c3cd4f0e88af.logic_status
  -> node_0d58d6b0697d4ad4a64dd7416ebd669e.prerequisite
node_0d58d6b0697d4ad4a64dd7416ebd669e.logic_status
  -> settle.dynamic_port_9db70e47e3cd450abee6cfd187998f28
```

The namespaced `run/client` candidate has the same semantics with renamed IDs:
`GreyHat_:KillSlimes`, `node_cfc0a454a1ef4168890d4ce6fbb3e803`
(`kill_entity`, `GreyHat_:Slimes`, 3), and
`node_4feb6ff8c4944e078f9914cd808c9f17` (`interact_actor`,
`GreyHat_:TarvenBoss`, implicit 1). Its second description is also
`消灭史莱姆`. Its story graph is
`GreyHat_:Firest -> GreyHat_:KillSlimes -> GreyHat_:TaskFinish -> give_item
-> terminate`.

Therefore the expected label `与酒馆老板对话` is absent from both the
recorded audit package and the namespaced run candidate. The second node's
*type/target/edge* is interaction with the tavern owner, but its authored
display description was copied from the first kill objective.

## Runtime, persistence, and projection trace

The baseline `HEAD` implementation follows the required state path:

1. `src/main/java/darkgrey/rpg/task/forge/CanonicalTaskEventAdapter.java:29-66`
   normalizes Forge death, pickup, and entity-interact events and dispatches
   them to `CanonicalTaskForgeManager`.
2. `src/main/java/darkgrey/rpg/task/forge/CanonicalTaskForgeManager.java:113-132`
   calls `CanonicalTaskSavedData.dispatch`; the manager reports changed and
   settled counts and invokes Story settlement only for a settled instance.
3. `src/main/java/darkgrey/rpg/task/persistence/CanonicalTaskSavedData.java:285-323`
   queries the subscription index, calls `instance.accept`, snapshots/reindexes
   the instance, and `markIfChanged` persists changed NBT state.
4. `src/main/java/darkgrey/rpg/task/runtime/CanonicalTaskRuntime.java:171-195`
   applies events only to currently ACTIVE objectives, sets the kill objective
   to `COMPLETED` at 3/3, then calls `refreshState()`.
5. `CanonicalTaskRuntime.java:245-272` recomputes logic and changes the gated
   interaction objective from `INACTIVE` to `ACTIVE` when the prerequisite edge
   becomes true. It then checks settlement.
6. `src/main/java/darkgrey/rpg/task/instance/CanonicalTaskInstance.java:175-195`
   updates instance status if the runtime settles and exposes the complete
   runtime snapshot. `CanonicalTaskSavedData.java:377-409` serializes the store
   and marks the WorldSavedData dirty when the serialized state changes.
7. `src/main/java/darkgrey/rpg/task/journal/CanonicalTaskJournalProjector.java:114-152`
   restores the runtime snapshot against the resolved resource, then
   `:155-199` reads each objective's source `description` and pairs it with the
   snapshot status/progress. It does not rename descriptions by objective type.
8. `src/main/java/darkgrey/rpg/creator/CanonicalTaskUiProjection.java:18-42`
   keeps only ACTIVE Task instances and ACTIVE objective rows, copying
   `row.getDescription()` to the NBT `text` field. Completed objectives are
   intentionally omitted.
9. In baseline `HEAD`,
   `src/main/java/darkgrey/rpg/client/gui/GuiCanonicalTaskScreen.java:18-39`
   requests the projection on open and every 20 ticks, correlates the request
   number, and renders the returned text. There was no client read-only store
   in this baseline path; this is the separate M4 polling/cache limitation.

## Real-machine result and root cause

`PLAN/0.3.2.1/DEVELOPMENT_STATUS.md:48-70` records the real framebuffer and
timestamped action evidence. The decisive records are in
`.tooling/0.3.2.1/live-actions.jsonl`:

```text
line 269: objective current=1, text="消灭史莱姆", required=3
line 275: objective current=2, text="消灭史莱姆", required=3
line 298: objective current=0, text="消灭史莱姆", required=1
line 302: after interaction, session task_over_session opens
line 306: task list is empty after settlement
```

The change from `2/3` to `0/1`, while the task identity/title remains the same,
is direct evidence that the first objective completed and the second objective
became the active row. The following tavern interaction and empty task list
show that the interaction objective was accepted and the Task settled. The
same status transition is summarized explicitly in
`PLAN/0.3.2.1/DEVELOPMENT_STATUS.md:58-61`, which says the live gate observed
`0/3 -> 1/3 -> 2/3 -> next interaction objective` and that the fixture's
second objective intentionally retained its authored kill-like description.

Conclusion: the M5 symptom is an authored/exported Task description error
(second objective has `objective_type=interact_actor` but description
`消灭史莱姆`). Runtime prerequisite propagation, instance snapshot/persistence,
Journal status/progress projection, and settlement all behaved correctly in the
available evidence. The baseline UI polling path is independently stale-prone,
but it cannot explain the decisive `current=0, required=1` response; that
response was already a fresh server projection showing the new objective. GUI
rendering likewise received the same source text and therefore made the valid
transition look unchanged.

## Recommended bounded fix and remaining risk

Change the authored second objective description in the actual audited package
to `与酒馆老板对话` (or the user's exact final wording), preserving its
`interact_actor` type, `actor_id`, prerequisite edge, and required count. Export
that package, verify its new archive hash, and exercise the same 3-kill plus
tavern-interaction path. Keep M4 server-push/client-cache work separate from
this data correction. Do not claim closure from a synthetic fixture or from a
package whose archive provenance is not tied to the audit.

Remaining risks:

- `PLAN/0.3.2.1审计.docx` names the namespaced identities but does not contain a
  package filename/hash; exact mapping to `run/client/.../kill_slimes.dgrs` is
  therefore a strong candidate, not proven archive provenance.
- The recorded 0.3.2.1 real-machine run used the unnamespaced 9D8D/9B55 package;
  its passing evidence proves the transition semantics and stale source text,
  while the namespaced package still needs a direct user-audit replay if it is
  the intended correction target.
- The baseline GUI had open-time request plus 20-tick polling; server-push and
  client-cache changes visible in the working tree belong to another agent and
  were not modified or validated here.

## Source authoring path follow-up (2026-09-11)

The exact namespaced node `node_4feb6ff8c4944e078f9914cd808c9f17` is present
inside the audited archive only. It is not present in any raw JSON project
under `studio`, `dist`, or `run`; `run/client/darkgrey_rpg_project/project.json`
is only the empty-base metadata (`dgr_0320b_empty_base`). The archive copy
`run/client/darkgrey_rpg_story_packages/kill_slimes.dgrs` and the preserved
`.tooling/0.3.2.2/audit-trace/original/kill_slimes.dgrs` are byte-identical,
SHA-256 `82E289D8777C869631B0BA7F96985D2FE1CF0475C5396E1C11AE38C3C0928E03`.

The historical Studio acceptance record names the original editable source as
`E:\\Java\\MinecraftMod\\RPGProject\\project_test`; that path no longer
exists on this machine (`Test-Path` false). Its retained in-repo copy is
`.tooling/0.3.2.0_B3/real-machine-20260904/studio-project-copy`, with the
recorded source hashes and the older unnamespaced objective IDs
`node_2c0b762cdddd433d8277c3cd4f0e88af` and
`node_0d58d6b0697d4ad4a64dd7416ebd669e`; it cannot be treated as the exact
GreyHat authoring source. The B4 migration copy
`.tooling/0.3.2.0_B4/real-machine-20260908/old-project-live` likewise has
`B4Final:*` IDs and the older node IDs. Therefore no authoritative editable
project for the exact GreyHat package is available in this checkout; correct
the isolated archive copy and retain its new hash unless the missing source is
recovered.
