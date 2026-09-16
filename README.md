# DarkGreyRPG 0.3.1.0

DarkGreyRPG is a server-authoritative RPG authoring and runtime framework for
Minecraft 1.7.10. The Windows WPF Studio authors Story, Session, Task, Actor,
and Item resources; a Story can be exported as a self-contained Story Package
and installed by a single-player host or dedicated-server owner.

Version `0.3.1.0` adds the Minecraft deployment layer for the canonical graph
model introduced in 0.3.0.0:

- Story Packages with strict manifests, server-owned installation, startup
  loading, and transactional `/dgr reload`;
- individual and collective Actor identities backed by external SavedData, so
  unique NPC IDs are never copied through ordinary entity NBT;
- exact Item IDs and exact/fuzzy Item Groups captured from real ItemStacks;
- the server-authoritative Nominator, multi-template Copier, survival/creative
  Storage Box, and placeholder Body/Editor tools;
- editable cross-Story logic wiring with stable `port_id` boundaries;
- Story and Session logic inputs/outputs, Narration, and unified Condition
  behavior;
- pure-logic Tasks without Activate/flow nodes, dynamically gated Objectives,
  and one prioritized Settlement node;
- UUID-isolated player Story/Session/Task state and idempotent reward receipts;
- `give_item` actions that resolve only a DGR individual Item ID;
- optional, reflection-safe CustomNPC+ identity compatibility. DarkGreyRPG
  starts and runs without CustomNPC+.

The Project Story Graph remains a cross-Story logic layer, not a second Story
Flow. Native NPC editing, arbitrary NBT scripting, Task flow control, implicit
concurrency, and CNPC Quest/Dialogue migration are outside this release.

## Requirements

- Minecraft 1.7.10
- Forge 10.13.4.1614
- Java 8 for Runtime builds and Minecraft
- .NET 10 SDK for Studio source builds; the packaged EXE is self-contained
- CustomNPC+ and UniMixins only when testing the optional CNPC adapter

## Runtime build

PowerShell:

```powershell
$env:JAVA_HOME='E:\Java\jdk1.8.0_471'
$env:GRADLE_USER_HOME='E:\Java\gradle'
.\gradlew.bat build --offline --no-daemon --no-configuration-cache --max-workers=1
```

The release JAR is written to
`build/libs/darkgrey_rpg-0.3.1.0.jar`.

## Studio build

```powershell
$env:windir=$env:SystemRoot
& 'E:\Java\dotnet-sdk-10\dotnet.exe' test `
  'studio/src/DarkGreyRPG.Studio.Tests/DarkGreyRPG.Studio.Tests.csproj' `
  --configuration Release
& 'E:\Java\dotnet-sdk-10\dotnet.exe' test `
  'studio/src/DarkGreyRPG.Studio.Wpf.Tests/DarkGreyRPG.Studio.Wpf.Tests.csproj' `
  --configuration Release
& '.\studio\package-studio.ps1'
```

The self-contained Windows x64 application is published to
`dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`.

## Story Package workflow

1. Open or create a project in Studio.
2. Create individual/collective Actors and Items, then author the Story,
   Session, and pure-logic Task graphs in the shared editor.
3. Export the completed Story. The package includes its manifest and declared
   Actor, Item, Session, Task, and graph resources.
4. Copy the package directory into the server-owned
   `DarkGreyRPG/StoryPackages` directory. Its path is configurable in
   `DarkGreyRPG/Config/darkgrey_rpg.cfg`.
5. Start the server or run `/dgr reload` as an operator. Invalid replacement
   packages are rejected without first discarding the active definitions.

Ordinary clients do not install or authorize Story Packages. Integrated Server
single-player uses the same server path and validation rules.

## In-game authoring tools

- Nominator: right-click a living entity to assign one individual NPC ID and
  zero or more Actor Group IDs; right-click air to bind an inventory stack to
  one exact Item ID and Item Groups using exact or fuzzy matching.
- Copier: right-click a living entity to capture a reusable template, open its
  management GUI from air, and right-click a block to spawn the selected copy.
  Groups copy; unique NPC IDs do not.
- Storage Box: in Survival it moves the same living entity and keeps its UUID
  identity reserved; in Creative it stores a reusable single-slot template and
  never copies a unique NPC ID.
- Body and Editor: visible placeholder items for future native NPC work; that
  larger system is intentionally not part of 0.3.1.0.

All identity changes, package loading, runtime advancement, Task progress, and
rewards are validated on the server. Nominator use requires operator or DGR
editing permission.

## Compatibility and migration

Studio retains the explicit preview/confirm/backup transaction for legacy
2.1.3-to-canonical migration. 0.3.1.0 adds strict compatibility diagnostics for
the removed EnterStory model, old Task Activate/flow shapes, old judgment
nodes, and former aggregate interface shapes. It never silently resets player
progress during a normal package reload.

See:

- `PLAN/DarkGreyRPG_0.3.1.0_Construction_Plan.md`
- `docs/0.3.1.0_ARCHITECTURE.md`
- `docs/0.3.1.0_MIGRATION.md`
- `docs/0.3.1.0_ACCEPTANCE.md`

The earlier `docs/0.3.0.0_*` and `docs/2.1.3_*` files remain historical
specifications and migration evidence.

## Runtime directory layout (0.3.3.1)

Player-facing files are grouped under `<Minecraft>/DarkGreyRPG/`:

- `Project`: default runtime project.
- `StoryPackages`: installed story packages.
- `Cache`: downloaded image/audio resources and whole-package cache metadata.
- `Config`: mod configuration and client window preferences.
- `Exports`: generated BUFF catalog exports.

Legacy default directories migrate on startup without overwriting conflicting
content. Explicitly configured external project/package paths remain supported.
Minecraft's `mods`, `saves`, and `logs`, persisted mod IDs, and world SavedData
remain managed by Minecraft. The development checkout is now
`E:\Java\MinecraftMod\DarkGreyRPG`.