param([string]$Baseline = '0f1f002aba10a7e1ae12564d6fdda093d5389ef4')
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $frozen = @('studio','schema','src/main/java/darkgrey/rpg/graph','src/main/java/darkgrey/rpg/project','src/main/java/darkgrey/rpg/session/runtime','src/main/java/darkgrey/rpg/task/runtime','src/main/java/darkgrey/rpg/story/canonical/runtime')
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
