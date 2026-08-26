# DarkGrey RPG Studio 2.1 (WPF)

The current Studio is a Windows WPF application targeting .NET 10. It uses the repository's Studio Core project and has no third-party UI, MVVM, serialization, logging, or packaging dependencies.

Studio 2.1 uses Story-first navigation, dedicated Actor/Dialogue/Quest editors,
a free-form Story Flow node canvas, and a logic-read-only Project Story Graph.
See `docs/2.1_ARCHITECTURE.md` and `docs/TESTING.md` for product boundaries and
the accepted verification matrix.

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
