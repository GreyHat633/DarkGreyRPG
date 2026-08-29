[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dotnetPath = 'E:\Java\dotnet-sdk-10\dotnet.exe'
$projectPath = Join-Path $repositoryRoot 'studio\src\DarkGreyRPG.Studio\DarkGreyRPG.Studio.csproj'
$outputPath = Join-Path $repositoryRoot 'dist\DarkGreyRPGStudio'
$expectedOutputPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'dist\DarkGreyRPGStudio'))
$toolingRoot = Join-Path $repositoryRoot '.tooling\wpf-build'
$dotnetCliHome = $toolingRoot
$nugetPackages = Join-Path $toolingRoot 'nuget-packages'
$temporaryPath = Join-Path $toolingRoot 'temp'

if (-not (Test-Path -LiteralPath $dotnetPath -PathType Leaf)) {
    throw "Required isolated .NET SDK was not found: $dotnetPath"
}
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Studio project was not found: $projectPath"
}

foreach ($directoryPath in @($dotnetCliHome, $nugetPackages, $temporaryPath)) {
    New-Item -ItemType Directory -Force -Path $directoryPath | Out-Null
}

$resolvedOutputPath = [IO.Path]::GetFullPath($outputPath)
if ($resolvedOutputPath -ne $expectedOutputPath) {
    throw "Refusing to clean an unexpected package directory: $resolvedOutputPath"
}
if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
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
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

Write-Host "Publishing DarkGrey RPG Studio to $outputPath"
& $dotnetPath @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$executablePath = Join-Path $outputPath 'DarkGreyRPGStudio.exe'
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Publish completed without the expected executable: $executablePath"
}

$unexpectedFiles = @(Get-ChildItem -LiteralPath $outputPath -File | Where-Object { $_.Name -ne 'DarkGreyRPGStudio.exe' })
if ($unexpectedFiles.Count -gt 0) {
    $names = ($unexpectedFiles | ForEach-Object Name) -join ', '
    throw "Single-file publish produced unexpected files: $names"
}

$fileHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $executablePath).Hash.ToLowerInvariant()
Write-Host "Published: $executablePath"
Write-Host "SHA-256: $fileHash"
