<!-- >>> Codex Agent Switch managed native worker routing >>> -->
## Codex Agent Switch managed native worker routing

For non-trivial development, run one Delegation Capability Preflight after minimal localization, then make the Initial Delegation Check before substantive implementation. Tiny or read-only work and user-forbidden delegation are exempt. Prefer WORKER for a clear, bounded, stable, verifiable, non-overlapping package; MAIN owns unresolved architecture or investigation, cross-module decisions, required review, and final integration.

Main supplies semantic lifecycle changes; Agent Switch owns mechanical state and enforcement. Queue relevant triggers—INITIAL_LOCALIZATION_COMPLETE, ARCHITECTURE_RESOLVED, WORKER_RESULT_RECEIVED, WORKER_REVIEW_COMPLETE, PHASE_CHANGE, BUILD_TEST_BOUNDED_FIXES, MODULE_COMPLETE, WORK_CONVERGED—and resolve MAIN vs WORKER once at the next natural reasoning boundary. A pending decision must be resolved before substantive mutation; the ownership lease remains the mechanical backstop. Hooks and Hard Gate are FrozenDisabled in 0.2.7.0.

Never duplicate DELEGATED or RUNNING Worker work. Review returned work to the package risk, adopt or reject it before relying on it, then reconsider remaining ownership after WORKER_REVIEW_COMPLETE.

For bounded delegation, call the codex_agent_switch delegate_worker tool with a complete plaintext TaskPacket. When invoking the configured Native Custom Worker, you MUST call spawn_agent with both actual tool arguments: agent_type="cas_luna_worker" and fork_turns="none". fork_turns is mandatory for this managed custom role: never omit it, never use fork_turns="all", and never create a full-history fork. While the task is DELEGATED or RUNNING, do not duplicate its work. Report the result through report_worker_result, then perform only bounded review.
<!-- <<< Codex Agent Switch managed native worker routing <<< -->
