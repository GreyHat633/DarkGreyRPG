param([string]$Baseline = 'c5ee16069ee938fd684af621de56597993c48c4e')
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $frozen = @('studio','schema','src/main/java/darkgrey/rpg/graph','src/main/java/darkgrey/rpg/project','src/main/java/darkgrey/rpg/session','src/main/java/darkgrey/rpg/task','src/main/java/darkgrey/rpg/story','src/main/java/darkgrey/rpg/creator/CanonicalTaskPresentationServer.java','src/main/java/darkgrey/rpg/client/CanonicalTaskClientStore.java','src/main/java/darkgrey/rpg/client/session')
    $paths = @(git diff --name-only $Baseline -- @frozen)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot compare frozen baseline.' }
    $paths += @(git ls-files --others --exclude-standard -- @frozen)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect frozen additions.' }
    if ($paths.Count) { throw "Frozen paths changed: $($paths -join ', ')" }
    'STUDIO_DIFF=0'
    'FROZEN_SCHEMA_DIFF=0'
    'FROZEN_CANONICAL_RUNTIME_DIFF=0'
    'FREEZE_GUARD=PASS'
} finally { Pop-Location }
