> 2026-09-14 最终集成更新：本文件保留工作包阶段记录；后续编译修复、真实交互及最终证据以 [DELIVERY_REPORT.md](DELIVERY_REPORT.md) 为准。阶段性待办不能覆盖最终记录，未实测部分也不自动计为通过。

# WP-D Java runtime construction

This file records the Java-side WP-D boundary delivered in the current construction tree.

## Canonical wire

`resources/story_logic_graph.json` is the only project Story boundary graph.  Its schema version is `2`; every edge carries the exact case-sensitive `interface_kind` value `Flow` or `Logic` alongside `source_story_id`, `source_port_id`, `target_story_id`, and `target_port_id`.

The loader validates the complete installed package set.  A package-owned fragment may be parsed with unresolved target IDs, but activation validates the merged set.  Flow edges require a unique `terminate.port_id` source and an exact `flow_driven` Start trigger target.  Logic edges require a unique `logic_output` source and `logic_input` target.  Flow outputs have at most one destination; Logic inputs have at most one source.

## Runtime behavior

`CanonicalStoryRuntime` remains the single Story cursor.  Start transitions use the stable trigger `port_id`; `flow_driven` does not add an implicit input node.  Termination exposes the exact `terminate.port_id`.  `CanonicalStoryServerService.startFlowFromTerminal` resolves one matching Flow edge and starts only that target trigger.  `CanonicalStoryForgeManager` records a durable pending `(player, story, activation_time, terminal_port_id)` claim before routing and promotes it to applied only after the target route is committed.  Schema 6 also records the exact destination run identity for each source claim, so repeatable targets are resumed after reload instead of started again; if that target has since advanced to a newer run, the recorded handoff is resolved without starting another run.  `recoverPendingTerminalRoutes` replays source terminal snapshots after reload.  A fault before or after destination start cannot lose or duplicate the route; the source terminal snapshot is preserved while its claim is pending.  A cyclic terminal chain shares one safety budget and resolves encountered pending claims on bounded abort, preventing tick recovery storms.

Logic propagation filters to `Logic` edges and compares each Start trigger's own `logic_port_id` against its previous observed value.  Unrelated input changes therefore cannot restart a repeatable Story whose trigger remains true; an ACTIVE Story still receives input updates but cannot re-enter.  Automatic cursor traversal is bounded by the existing deterministic transition guard.

## Verification boundary

The following probes were compiled and run sequentially with `JAVA_HOME=E:/Java/jdk-25.0.1` and the repository-local `.gradle-user-home` cache, all with `--offline`: `storyBoundary0331Probe` (v2 Flow wire, schema 6 destination identity, pending/applied codec recovery), `canonicalProjectContentProbe`, `canonicalStoryInstanceProbe`, `canonicalStoryRuntimeProbe`, `canonicalStoryForgeCoordinatorProbe`, `canonicalStoryServerServiceProbe`, and `storyPackageLoaderProbe`.  The probes report `PASS`; the Forge coordinator probe includes injected faults before destination start, after destination start with a repeatable target, and a direct cyclic graph, then reloads world state and verifies recovery/deduplication/safety bounding.

These are server-neutral Java probes.  A real Minecraft/Forge process run, an event hook that invokes `recoverPendingTerminalRoutes` on player/world load, and package reload against live media remain open integration checks.
