

## Studio UI skill

When changing Studio node property editors or Inspector layouts, read
`.agents/skills/studio-node-ui/SKILL.md` for the project's multi-module card design rules.

## Story media lifecycle skill

When changing runtime story-package media preloading, cache admission, eviction,
or media lifecycle, read `.agents/skills/story-media-lifecycle/SKILL.md`.

## DGR finished-product release skill

Before packaging a finished DGR product, or when the user explicitly requests
release, publication, or packaging, read
`.agents/skills/dgr-release-packaging/SKILL.md`.
Also read it when changing Studio's portable deployment layout or deployment
scripts, or updating a runnable Studio copy. It defines the shared directory
classification and data-preserving update rules; consulting it does not
authorize a finished-product release.

- These requests mean a new `artifacts\DGR<current version>` folder containing
  the client Runtime JAR and a complete self-contained Windows x64 Studio ZIP.
- Without an explicit user request, do not create or publish finished-product
  packages. Development, fixes, tests, updating `dist`, and GitHub source uploads
  do not implicitly authorize an `artifacts` release.
- A request to remember or edit this rule does not itself authorize packaging.

## Studio delivery invariant

Studio is portable: editor-owned settings, logs, cache and temporary files
belong under the executable directory's `Data` folder. New projects default to
`Data/Projects`; users may explicitly choose an external project location.
Keep those projects, their media and recovery files in the chosen location and
record that choice in local settings so reopening/restoring does not import them.
New copies must not load shared Windows-user settings. Other external projects
are copied into `Data/Projects` before opening/migrating. Store internal paths
relatively and explicitly selected external paths absolutely. Default to Dark
theme on first launch; preserve existing user theme preferences.
Use self-contained directory deployment, and preserve `Data` when updating
program files. Only explicit project/export/deployment destinations may write outside.
Keep both dist and the Studio ZIP organized: only the root apphost EXE at the
root; `Program` for assemblies/runtime/resources, `Tools` for media utilities,
`Docs` for documentation/manifests, and `Data` for personal state. The root
apphost loads `Program/DarkGreyRPGStudio.dll` directly. Packaging uses the
program whitelist in `Docs/StudioProgramFiles.json` and excludes `Data`.
Minecraft-owned runtime directories below are separate from Studio data.

Runtime-owned data belongs under `<Minecraft>/DarkGreyRPG`: `Project`,
`StoryPackages`, `Cache`, and `Config`. Use PascalCase for new user-facing
directory names. Migrate legacy default paths without overwriting data;
preserve explicit external paths and persisted resource/mod identifiers.
The development repository is `E:\Java\MinecraftMod\DarkGreyRPG`.

- The single authoritative, user-runnable Studio delivery artifact is
  `E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`.
- Every Studio update must publish or promote the newest self-contained Windows
  x64 Release client to that exact path before the update may be reported as
  delivered, published, or complete.
- Acceptance candidates under `.tooling`, ordinary `bin\Release` outputs, and
  versioned archive directories do not satisfy delivery by themselves.
- After promotion, verify that the EXE exists at the authoritative path and
  report its ProductVersion, file size, and SHA-256. If it is missing or stale,
  the Studio update is not delivered.


<!-- >>> Codex Agent Switch managed native worker routing >>> -->
## Codex Agent Switch managed native worker routing

For non-trivial development, run one Delegation Capability Preflight after minimal localization, then make the Initial Delegation Check before substantive implementation. Tiny or read-only work and user-forbidden delegation are exempt. Prefer WORKER for a clear, bounded, stable, verifiable, non-overlapping package; MAIN owns unresolved architecture or investigation, cross-module decisions, required review, and final integration.

Main supplies semantic lifecycle changes; Agent Switch owns mechanical state and enforcement. Queue relevant triggers—INITIAL_LOCALIZATION_COMPLETE, ARCHITECTURE_RESOLVED, WORKER_RESULT_RECEIVED, WORKER_REVIEW_COMPLETE, PHASE_CHANGE, BUILD_TEST_BOUNDED_FIXES, MODULE_COMPLETE, WORK_CONVERGED—and resolve MAIN vs WORKER once at the next natural reasoning boundary. A pending decision must be resolved before substantive mutation; the ownership lease remains the mechanical backstop. Hooks and Hard Gate are FrozenDisabled in 0.2.7.0.

Never duplicate DELEGATED or RUNNING Worker work. Review returned work to the package risk, adopt or reject it before relying on it, then reconsider remaining ownership after WORKER_REVIEW_COMPLETE.

For bounded delegation, call the codex_agent_switch delegate_worker tool with a complete plaintext TaskPacket. When invoking the configured Native Custom Worker, you MUST call spawn_agent with both actual tool arguments: agent_type="cas_luna_worker" and fork_turns="none". fork_turns is mandatory for this managed custom role: never omit it, never use fork_turns="all", and never create a full-history fork. While the task is DELEGATED or RUNNING, do not duplicate its work. Report the result through report_worker_result, then perform only bounded review.
<!-- <<< Codex Agent Switch managed native worker routing <<< -->
