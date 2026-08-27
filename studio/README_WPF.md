# DarkGrey RPG Studio 2.1.3 (WPF)

The current Studio is a Windows WPF application targeting .NET 10. It uses the repository's Studio Core project and has no third-party UI, MVVM, serialization, logging, or packaging dependencies.

Studio 2.1.3 uses Story-first navigation, dedicated Actor/Dialogue/Quest editors,
a free-form Story Flow node canvas, and a logic-read-only Project Story Graph.
See `docs/2.1_ARCHITECTURE.md` and `docs/TESTING.md` for product boundaries and
the accepted verification matrix.

Actor, Dialogue, and Quest use one compact Story resource library for Owned and
Referenced memberships. Create and Duplicate open an in-memory Draft and do
not write JSON or membership until the first valid Save; Reference keeps the
same project resource and its Home Story. Dialogue starts with End/`complete`,
while Quest starts with no objectives or fake actor. See
`docs/2.1.3_RESOURCE_CREATION_MODEL.md` and
`docs/2.1.3_STORY_RESOURCE_LIBRARY.md` for the lifecycle and visual contracts.

Canvas controls follow the 2.1.2 convention retained in 2.1.3: left drag moves nodes, middle drag
pans, the wheel zooms around the cursor, and right click opens context actions.
Project Story Graph remains logic-read-only; only Story Flow edits Runtime logic.

Flow connections can begin from either endpoint. Existing edges can reconnect
from either side, blank release disconnects, and `Esc` restores the original
edge. Boolean, Dialogue Exit, Sequence, terminal, and legacy-preserved outputs
come from the central node definition registry. Inline core/advanced property
editors preserve compatibility fields, Problems focus their exact node field,
and invalid drafts are isolated in the editor Recovery store.

Project Graph aggregates parallel transitions, renders explicit self-loops and
direction arrows, explains branch sources in tooltips, and lays out cycles via
finite SCC condensation. See
`docs/2.1.2_FLOW_CONNECTION_MODEL.md`,
`docs/2.1.2_NODE_PORT_CONTRACT.md`, and
`docs/2.1.2_STORY_GRAPH.md` for the contracts.

## Build and run from source

Use the isolated SDK at `E:\Java\dotnet-sdk-10\dotnet.exe`. Keep CLI state and packages in the repository-local `.tooling\wpf-build` directory:

```powershell
$env:DOTNET_CLI_HOME = "$pwd\.tooling\wpf-build"
$env:NUGET_PACKAGES = "$pwd\.tooling\wpf-build\nuget-packages"
& 'E:\Java\dotnet-sdk-10\dotnet.exe' build .\studio\src\DarkGreyRPG.Studio\DarkGreyRPG.Studio.csproj -c Release
& 'E:\Java\dotnet-sdk-10\dotnet.exe' run --project .\studio\src\DarkGreyRPG.Studio\DarkGreyRPG.Studio.csproj -c Release
```

## Package a portable Windows x64 build

Run the repeatable packaging script from the repository root:

```powershell
& .\studio\package-studio.ps1
```

The script uses the required repository-local `.tooling\wpf-build` directories for `DOTNET_CLI_HOME`, `NUGET_PACKAGES`, `TEMP`, and `TMP`. It publishes Release, Windows x64, self-contained, single-file output to:

```text
dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe
```

The packaged executable embeds the .NET runtime and native libraries. End users do not need to install .NET or Godot, and this WPF Studio does not depend on Godot at runtime.

The package script replaces only the exact `dist\DarkGreyRPGStudio` directory before publishing. It prints the resulting SHA-256 hash and fails if the expected single executable is absent or if additional files are emitted.
