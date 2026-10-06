[CmdletBinding()]
param([string]$SourceDirectory, [string]$ArchivePath, [string]$DestinationDirectory, [switch]$Offline)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'media-tools.lock.json') -Raw | ConvertFrom-Json
$destination = if ($DestinationDirectory) { [IO.Path]::GetFullPath($DestinationDirectory) } else { Join-Path $repositoryRoot '.tooling/media-tools/ffmpeg' }
function Assert-NoLinks([string]$Path) {
    $existing = [IO.Path]::GetFullPath($Path)
    while (-not (Test-Path -LiteralPath $existing)) { $existing = Split-Path -Parent $existing }
    for ($entry = Get-Item -LiteralPath $existing; $null -ne $entry; $entry = $entry.Parent) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Media path is a link: $($entry.FullName)" }
    }
}
function Test-Verified([string]$Directory) {
    foreach ($entry in $lock.files.PSObject.Properties) {
        $file = Join-Path $Directory $entry.Name
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { return $false }
        Assert-NoLinks $file
        if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Value) { return $false }
    }
    return $true
}
Assert-NoLinks $destination
if (-not $SourceDirectory -and -not $ArchivePath -and (Test-Verified $destination)) {
    Write-Host "Reusing verified FFmpeg $($lock.version): $destination"
    return
}
if ($SourceDirectory -and $ArchivePath) { throw 'Use either -SourceDirectory or -ArchivePath.' }
if ($Offline -and -not $SourceDirectory -and -not $ArchivePath) { throw 'Offline preparation requires verified tools or a local source/archive.' }
$parent = Split-Path -Parent $destination
New-Item -ItemType Directory -Force -Path $parent | Out-Null
$work = Join-Path $parent ('prepare-' + [guid]::NewGuid().ToString('N'))
$stage = Join-Path $work 'Ready'
New-Item -ItemType Directory -Force -Path $stage | Out-Null
try {
    if ($SourceDirectory) {
        $source = (Resolve-Path -LiteralPath $SourceDirectory).Path
        Assert-NoLinks $source
    } else {
        $archive = if ($ArchivePath) { (Resolve-Path -LiteralPath $ArchivePath).Path } else { Join-Path $work 'download.zip' }
        if (-not $ArchivePath) { Invoke-WebRequest -Uri $lock.archive_url -OutFile $archive }
        if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $lock.archive_sha256) { throw 'FFmpeg archive SHA-256 mismatch; existing tools were preserved.' }
        $expanded = Join-Path $work 'Expanded'
        Expand-Archive -LiteralPath $archive -DestinationPath $expanded
        $source = Join-Path $expanded 'ffmpeg-9.0-full_build'
    }
    foreach ($entry in $lock.files.PSObject.Properties) {
        $file = Join-Path $source $entry.Name
        if ($entry.Name -like '*.exe' -and -not (Test-Path -LiteralPath $file -PathType Leaf)) { $file = Join-Path $source ('bin/' + $entry.Name) }
        Assert-NoLinks $file
        Copy-Item -LiteralPath $file -Destination (Join-Path $stage $entry.Name)
    }
    if (-not (Test-Verified $stage)) { throw 'FFmpeg file SHA-256 mismatch; existing tools were preserved.' }
    $version = & (Join-Path $stage 'ffmpeg.exe') -version 2>&1
    if ($LASTEXITCODE -ne 0 -or $version[0] -notlike ('ffmpeg version ' + $lock.version + '*') -or ($version -join "`n") -notmatch '--enable-libvorbis') { throw 'Verified FFmpeg cannot run or lacks libvorbis.' }
    # Staging and destination share a volume. A failed promotion restores the
    # previous directory; partial downloads never touch live tools.
    $backup = Join-Path $parent ('previous-ffmpeg-' + [guid]::NewGuid().ToString('N'))
    if (Test-Path -LiteralPath $destination) { Move-Item -LiteralPath $destination -Destination $backup }
    try { Move-Item -LiteralPath $stage -Destination $destination }
    catch { if (Test-Path -LiteralPath $backup) { Move-Item -LiteralPath $backup -Destination $destination }; throw }
    if (Test-Path -LiteralPath $backup) { Write-Host "Previous tools retained at $backup" }
    Write-Host "Prepared verified FFmpeg $($lock.version): $destination"
} finally {
    $resolvedWork = [IO.Path]::GetFullPath($work)
    if (-not $resolvedWork.StartsWith($parent.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe preparation cleanup path' }
    Assert-NoLinks $resolvedWork
    Remove-Item -LiteralPath $resolvedWork -Recurse -Force
}
