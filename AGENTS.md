

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
