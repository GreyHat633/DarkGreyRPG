param([string]$Baseline = 'effce10e48beb694904627a9284a930cd0e3db9c')
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    # Fail closed: new client transport/tooling paths must be reviewed and added explicitly.
    $allowed = @(
        '^src/main/java/darkgrey/rpg/client/',
        '^src/test/java/darkgrey/rpg/client/',
        '^src/main/java/darkgrey/rpg/creator/',
        '^src/test/java/darkgrey/rpg/creator/',
        '^src/test/java/darkgrey/rpg/command/CanonicalTaskStage4SurfaceProbe.java$',
        '^src/main/java/darkgrey/rpg/DarkGreyRpg.java$',
        '^src/main/java/darkgrey/rpg/content/ModItems.java$',
        '^src/main/java/darkgrey/rpg/proxy/CommonProxy.java$',
        '^src/main/java/darkgrey/rpg/command/CommandDarkGreyRpg.java$',
        '^src/main/resources/assets/darkgrey_rpg/textures/items/inspector_goggles.png$',
        '^src/main/resources/assets/darkgrey_rpg/textures/models/armor/inspector_goggles.png$',
        '^src/main/resources/assets/darkgrey_rpg/lang/',
        '^gradle.properties$',
        '^build.gradle.kts$',
        '^scripts/verify-0321-freeze.ps1$',
        '^scripts/0321-client-format.gradle$',
        '^PLAN/0.3.2.1/'
    )
    $paths = @(git diff --name-only $Baseline -- src studio schema gradle.properties build.gradle.kts scripts)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot compare B4 baseline.' }
    $paths += @(git ls-files --others --exclude-standard -- src studio schema scripts)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect new files.' }
    $rejected = @($paths | Sort-Object -Unique | Where-Object {
        $path = $_
        -not ($allowed | Where-Object { $path -match $_ })
    })
    if ($rejected.Count) { throw "Frozen or unreviewed paths changed: $($rejected -join ', ')" }
    Write-Output 'STUDIO_DIFF=0'
    Write-Output 'FROZEN_RUNTIME_SCHEMA_DIFF=0'
    Write-Output 'FREEZE_GUARD=PASS'
} finally {
    Pop-Location
}
