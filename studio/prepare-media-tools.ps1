[CmdletBinding()]
param([string]$SourceDirectory)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$destination = Join-Path $repositoryRoot '.tooling/media-tools/ffmpeg'
if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
    $command = Get-Command ffmpeg -ErrorAction Stop
    $item = Get-Item -LiteralPath $command.Source
    $executable = if ($item.LinkType -eq 'SymbolicLink') { [string]$item.Target } else { $item.FullName }
    $SourceDirectory = Split-Path (Split-Path $executable)
}
$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
$files = @{
    'ffmpeg.exe' = Join-Path $source 'bin/ffmpeg.exe'
    'ffprobe.exe' = Join-Path $source 'bin/ffprobe.exe'
    'LICENSE' = Join-Path $source 'LICENSE'
    'README.txt' = Join-Path $source 'README.txt'
}
foreach ($file in $files.Values) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing media dependency: $file" }
}
New-Item -ItemType Directory -Force -Path $destination | Out-Null
foreach ($entry in $files.GetEnumerator()) { Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $destination $entry.Key) -Force }
$env:TEMP = Join-Path $repositoryRoot '.tmp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Force -Path $env:TEMP | Out-Null
$version = & (Join-Path $destination 'ffmpeg.exe') -version 2>&1
if ($LASTEXITCODE -ne 0 -or ($version -join "`n") -notmatch '--enable-libvorbis') { throw 'FFmpeg must provide libvorbis encoding.' }
if ($version[0] -notmatch '9\.0-full_build-www\.gyan\.dev') { throw "Unverified FFmpeg build: $($version[0])" }
$version[0]
Get-ChildItem -LiteralPath $destination -File | ForEach-Object {
    [pscustomobject]@{ Name=$_.Name; Bytes=$_.Length; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
