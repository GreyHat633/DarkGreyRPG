param(
    [switch]$SkipProbe
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$godotDirectory = Join-Path $projectRoot '.tooling\godot-4.7.1'
$godot = Join-Path $godotDirectory 'Godot_v4.7.1-stable_win64_console.exe'
$template = Join-Path $projectRoot '.tooling\godot-export-templates\windows_release_x86_64.exe'
$studio = Join-Path $projectRoot 'studio'
$dist = Join-Path $projectRoot 'dist'
$isolatedRoot = Join-Path $projectRoot '.tooling\studio-package-env'
$smokeRoot = Join-Path $isolatedRoot 'smoke'

if (-not (Test-Path -LiteralPath $godot -PathType Leaf)) {
    throw "Portable Godot executable not found: $godot"
}
if (-not (Test-Path -LiteralPath $template -PathType Leaf)) {
    throw "Isolated Windows export template not found: $template"
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $isolatedRoot 'appdata') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $isolatedRoot 'localappdata') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $isolatedRoot 'temp') | Out-Null

$previousEnvironment = @{
    APPDATA = $env:APPDATA
    LOCALAPPDATA = $env:LOCALAPPDATA
    TEMP = $env:TEMP
    TMP = $env:TMP
}

try {
    $env:APPDATA = Join-Path $isolatedRoot 'appdata'
    $env:LOCALAPPDATA = Join-Path $isolatedRoot 'localappdata'
    $env:TEMP = Join-Path $isolatedRoot 'temp'
    $env:TMP = $env:TEMP

    & $godot --headless --path $studio --export-release 'Windows Portable' (Join-Path $dist 'DarkGreyRPGStudio.exe')
    if ($LASTEXITCODE -ne 0) {
        throw "Godot Studio export failed with exit code $LASTEXITCODE."
    }

    if (-not $SkipProbe) {
        if (Test-Path -LiteralPath $smokeRoot) {
            Remove-Item -LiteralPath $smokeRoot -Recurse -Force
        }
        New-Item -ItemType Directory -Force -Path $smokeRoot | Out-Null
        $smokeExecutable = Join-Path $smokeRoot 'DarkGreyRPGStudio.exe'
        Copy-Item -LiteralPath (Join-Path $dist 'DarkGreyRPGStudio.exe') -Destination $smokeExecutable
        try {
            $probe = Start-Process -FilePath $smokeExecutable `
                -ArgumentList @('--headless', '--', '--portable-smoke-test') `
                -WindowStyle Hidden -Wait -PassThru
            if ($probe.ExitCode -ne 0) {
                throw "Exported Studio smoke probe failed with exit code $($probe.ExitCode)."
            }
        }
        finally {
            Remove-Item -LiteralPath $smokeRoot -Recurse -Force
        }
    }

    $artifact = Get-Item -LiteralPath (Join-Path $dist 'DarkGreyRPGStudio.exe')
    $hash = Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256
    [pscustomobject]@{
        Artifact = $artifact.FullName
        Bytes = $artifact.Length
        SHA256 = $hash.Hash
    } | Format-List
}
finally {
    $env:APPDATA = $previousEnvironment.APPDATA
    $env:LOCALAPPDATA = $previousEnvironment.LOCALAPPDATA
    $env:TEMP = $previousEnvironment.TEMP
    $env:TMP = $previousEnvironment.TMP
}
