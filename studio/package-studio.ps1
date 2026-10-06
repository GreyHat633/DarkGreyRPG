[CmdletBinding()]
param([string]$OutputDirectory, [string]$DotnetPath, [string]$MediaToolsDirectory)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dotnetPath = if ($DotnetPath) { $DotnetPath }
    elseif ($env:DGR_DOTNET_PATH) { $env:DGR_DOTNET_PATH }
    else { (Get-Command dotnet -ErrorAction SilentlyContinue).Source }
if ([string]::IsNullOrWhiteSpace($dotnetPath)) {
    throw 'A .NET 10 x64 SDK is required. Use -DotnetPath, DGR_DOTNET_PATH, or dotnet on PATH.'
}
if (Test-Path -LiteralPath $dotnetPath -PathType Container) { $dotnetPath = Join-Path $dotnetPath 'dotnet.exe' }
$dotnetPath = [IO.Path]::GetFullPath($dotnetPath)
$projectPath = Join-Path $repositoryRoot 'studio\src\DarkGreyRPG.Studio\DarkGreyRPG.Studio.csproj'
$destinationPath = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repositoryRoot 'dist\DarkGreyRPGStudio' }
$toolingRoot = Join-Path $repositoryRoot '.tooling\wpf-build'
$dotnetCliHome = $toolingRoot
$nugetPackages = Join-Path $toolingRoot 'nuget-packages'
$temporaryPath = Join-Path $toolingRoot 'temp'
$outputPath = Join-Path $toolingRoot ('portable-publish-' + [guid]::NewGuid().ToString('N'))

if (-not (Test-Path -LiteralPath $dotnetPath -PathType Leaf)) {
    throw "The selected .NET SDK executable was not found: $dotnetPath"
}
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Studio project was not found: $projectPath"
}

# Resolve apphost, HostModel and notices from the SDK actually selected.
$sdkVersion = (& $dotnetPath --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^10\.\d+\.\d+$') { throw "A stable .NET 10 SDK is required; selected SDK: $sdkVersion" }
$sdkLocations = @(& $dotnetPath --list-sdks)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate SDKs from the selected dotnet executable' }
$sdkLocation = $sdkLocations | Where-Object { $_ -match ('^' + [regex]::Escape($sdkVersion) + ' \[(.+)\]$') } | Select-Object -First 1
if (-not $sdkLocation) { throw "Cannot locate the selected SDK $sdkVersion" }
[void]($sdkLocation -match '\[(.+)\]$')
$sdkPath = Join-Path $Matches[1] $sdkVersion
$dotnetRootPath = Split-Path -Parent $Matches[1]
foreach ($requiredSdkFile in @('Microsoft.NET.HostModel.dll', 'AppHostTemplate\apphost.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sdkPath $requiredSdkFile) -PathType Leaf)) { throw "Selected SDK is incomplete: $requiredSdkFile" }
}
$apphostBytes = [IO.File]::ReadAllBytes((Join-Path $sdkPath 'AppHostTemplate\apphost.exe'))
if ($apphostBytes.Length -lt 64 -or $apphostBytes[0] -ne 0x4d -or $apphostBytes[1] -ne 0x5a) { throw 'Selected SDK apphost is not a Windows executable' }
$peOffset = [BitConverter]::ToInt32($apphostBytes, 0x3c)
if ($peOffset -lt 0 -or $peOffset + 6 -gt $apphostBytes.Length -or [BitConverter]::ToUInt32($apphostBytes, $peOffset) -ne 0x4550 -or [BitConverter]::ToUInt16($apphostBytes, $peOffset + 4) -ne 0x8664) {
    throw 'The selected .NET SDK must provide a Windows x64 apphost'
}
foreach ($noticeName in @('LICENSE.txt', 'ThirdPartyNotices.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $dotnetRootPath $noticeName) -PathType Leaf)) { throw "Selected SDK is missing distribution notices: $noticeName" }
}
Write-Host "Selected SDK: $sdkVersion ($sdkPath)"
$selectedMediaTools = if ($MediaToolsDirectory) { [IO.Path]::GetFullPath($MediaToolsDirectory) } else { Join-Path $repositoryRoot '.tooling\media-tools\ffmpeg' }
& (Join-Path $PSScriptRoot 'prepare-media-tools.ps1') -DestinationDirectory $selectedMediaTools -Offline

foreach ($directoryPath in @($dotnetCliHome, $nugetPackages, $temporaryPath)) {
    New-Item -ItemType Directory -Force -Path $directoryPath | Out-Null
}

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

$env:DOTNET_CLI_HOME = $dotnetCliHome
$env:NUGET_PACKAGES = $nugetPackages
$env:TEMP = $temporaryPath
$env:TMP = $temporaryPath

$publishArguments = @(
    'publish',
    $projectPath,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--output', $outputPath,
    '--nologo',
    '-p:PublishSingleFile=false',
    '-p:IncludeNativeLibrariesForSelfExtract=false',
    '-p:NuGetAudit=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

if ($MediaToolsDirectory) { $publishArguments += '-p:MediaToolsDirectory=' + [IO.Path]::GetFullPath($MediaToolsDirectory) }
Write-Host "Publishing DarkGrey RPG Studio to $outputPath"
& $dotnetPath @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$executablePath = Join-Path $outputPath 'DarkGreyRPGStudio.exe'
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Publish completed without the expected executable: $executablePath"
}

foreach ($requiredFile in @('DarkGreyRPGStudio.dll', 'coreclr.dll', 'wpfgfx_cor3.dll', 'Tools\FFmpeg\ffmpeg.exe', 'Tools\FFmpeg\ffprobe.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $outputPath $requiredFile) -PathType Leaf)) { throw "Missing bundled program file: $requiredFile" }
}

# Keep the native apphost at the root while its entry assembly and runtime live in Program.
# HostWriter is the same SDK API used to build the original apphost; no wrapper process,
# global runtime installation, or self-extracting single-file bundle is introduced.
$layoutPath = Join-Path $toolingRoot ('organized-publish-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $layoutPath | Out-Null
foreach ($file in Get-ChildItem -LiteralPath $outputPath -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($outputPath, $file.FullName)
    if ($relative -eq 'DarkGreyRPGStudio.exe') { continue }
    $organized = if ($relative -match '^Tools[\\/]') { $relative }
        elseif ($relative -in @('LICENSE.txt', 'THIRD-PARTY-NOTICES.txt')) { Join-Path 'Docs' $relative }
        else { Join-Path 'Program' $relative }
    $targetPath = Join-Path $layoutPath $organized
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetPath) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $targetPath
}
[void][Reflection.Assembly]::LoadFrom((Join-Path $sdkPath 'Microsoft.NET.HostModel.dll'))
[Microsoft.NET.HostModel.AppHost.HostWriter]::CreateAppHost(
    (Join-Path $sdkPath 'AppHostTemplate\apphost.exe'),
    (Join-Path $layoutPath 'DarkGreyRPGStudio.exe'),
    'Program\DarkGreyRPGStudio.dll', $true,
    (Join-Path $layoutPath 'Program\DarkGreyRPGStudio.dll'), $false, $true, $null)
$docsPath = Join-Path $layoutPath 'Docs'
New-Item -ItemType Directory -Force -Path $docsPath | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PORTABLE_STORAGE.md') -Destination (Join-Path $docsPath 'PortableStorage.md')
foreach ($document in @('THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $document) -Destination (Join-Path $docsPath (Split-Path $document -Leaf))
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'media-tools.lock.json') -Destination (Join-Path $docsPath 'MediaTools.lock.json')
# Runtime distribution notices are not emitted by dotnet publish. Preserve the
# SDK's bundled .NET notices alongside the self-contained runtime files.
foreach ($noticeName in @('LICENSE.txt', 'ThirdPartyNotices.txt')) {
    $noticePath = Join-Path $dotnetRootPath $noticeName
    if (-not (Test-Path -LiteralPath $noticePath -PathType Leaf)) { throw "Missing bundled .NET notice: $noticeName" }
    Copy-Item -LiteralPath $noticePath -Destination (Join-Path $docsPath $noticeName)
}

# Never mirror or clear a deployed copy: it may contain personal Data.
function Assert-NoLinks([string]$Path) {
    $existingPath = [IO.Path]::GetFullPath($Path)
    while (-not (Test-Path -LiteralPath $existingPath)) { $existingPath = Split-Path -Parent $existingPath }
    for ($entry = Get-Item -LiteralPath $existingPath; $null -ne $entry; $entry = $entry.Parent) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Program destination is a link: $($entry.FullName)" }
    }
}
Assert-NoLinks $destinationPath
New-Item -ItemType Directory -Force -Path $destinationPath | Out-Null
if ((Get-Item -LiteralPath $destinationPath).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Output directory is a link: $destinationPath" }
$manifestPath = Join-Path $destinationPath 'Docs\StudioProgramFiles.json'
$legacyManifestPath = Join-Path $destinationPath 'StudioProgramFiles.json'
$previousManifest = if (Test-Path -LiteralPath $manifestPath) { $manifestPath } else { $legacyManifestPath }
$previousFiles = if (Test-Path -LiteralPath $previousManifest) { @(Get-Content -LiteralPath $previousManifest -Raw | ConvertFrom-Json) } else { @('DarkGreyRPGStudio.pdb', 'DarkGreyRPG.Studio.Core.pdb') }
$programFiles = @(Get-ChildItem -LiteralPath $layoutPath -Recurse -File | ForEach-Object { [IO.Path]::GetRelativePath($layoutPath, $_.FullName) })
if (@(Get-Process -Name DarkGreyRPGStudio -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited -and $_.Path -eq (Join-Path $destinationPath 'DarkGreyRPGStudio.exe') }).Count) { throw 'Close the destination Studio before updating program files' }
$obsoleteDirectories = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$replacementBackup = [IO.Path]::GetFullPath((Join-Path $toolingRoot ('replaced-program-' + [guid]::NewGuid().ToString('N'))))
foreach ($relative in $previousFiles) {
    $targetPath = [IO.Path]::GetFullPath((Join-Path $destinationPath $relative))
    if ([IO.Path]::IsPathRooted($relative) -or -not $targetPath.StartsWith($destinationPath.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or $relative -match '^Data([\\/]|$)') { throw "Unsafe previous program manifest path: $relative" }
    Assert-NoLinks $targetPath
    if ($relative -notin $programFiles -and (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
        Remove-Item -LiteralPath $targetPath -Force
        for ($parent = Split-Path -Parent $targetPath; $parent -ne $destinationPath; $parent = Split-Path -Parent $parent) { [void]$obsoleteDirectories.Add($parent) }
    }
}
foreach ($relative in $programFiles) {
    $targetPath = Join-Path $destinationPath $relative
    Assert-NoLinks $targetPath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetPath) | Out-Null
    try {
        Copy-Item -LiteralPath (Join-Path $layoutPath $relative) -Destination $targetPath -Force
    } catch [IO.IOException] {
        # An exited Windows process can retain its image mapping. Rename the old
        # program file into tooling, then install a fresh file; personal Data is excluded.
        $targetPath = [IO.Path]::GetFullPath($targetPath)
        $backupPath = [IO.Path]::GetFullPath((Join-Path $replacementBackup $relative))
        if (-not $targetPath.StartsWith($destinationPath.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or
            -not $backupPath.StartsWith($replacementBackup.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or
            $relative -match '^Data([\\/]|$)' -or -not (Test-Path -LiteralPath $targetPath -PathType Leaf)) { throw }
        Assert-NoLinks $backupPath
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backupPath) | Out-Null
        Move-Item -LiteralPath $targetPath -Destination $backupPath
        try { Copy-Item -LiteralPath (Join-Path $layoutPath $relative) -Destination $targetPath }
        catch { Move-Item -LiteralPath $backupPath -Destination $targetPath; throw }
        Write-Host "Retained previous program file: $backupPath"
    }
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $manifestPath) | Out-Null
ConvertTo-Json -InputObject $programFiles | Set-Content -LiteralPath $manifestPath -Encoding utf8
if (Test-Path -LiteralPath $legacyManifestPath -PathType Leaf) { Remove-Item -LiteralPath $legacyManifestPath -Force }
# Only remove empty directories that held obsolete program files. Data is never traversed.
foreach ($directory in ($obsoleteDirectories | Sort-Object Length -Descending)) {
    if (-not $directory.StartsWith($destinationPath.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe obsolete directory' }
    Assert-NoLinks $directory
    if ((Test-Path -LiteralPath $directory -PathType Container) -and @(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0) { Remove-Item -LiteralPath $directory }
}
$executablePath = Join-Path $destinationPath 'DarkGreyRPGStudio.exe'
Get-Item -LiteralPath $executablePath | Select-Object FullName,Length,@{Name='ProductVersion';Expression={$_.VersionInfo.ProductVersion}}

$fileHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $executablePath).Hash.ToLowerInvariant()
Write-Host "Published: $executablePath"
Write-Host "SHA-256: $fileHash"
