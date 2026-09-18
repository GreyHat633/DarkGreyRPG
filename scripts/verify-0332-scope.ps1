[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$baseline = '66732365aacb426cbec96d40bb55a990aa758d55'
Push-Location $root
try {
    git merge-base --is-ancestor $baseline HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Required 0.3.3.1 baseline is not an ancestor.' }
    $tracked = @(git diff --name-only $baseline -- src studio scripts gradle.properties build.gradle.kts)
    $untracked = @(git ls-files --others --exclude-standard -- src/main src/test studio/src scripts)
    $paths = @($tracked + $untracked | Sort-Object -Unique | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    $implementation = @($paths | Where-Object { $_ -match '\.(java|cs|xaml|kts|properties)$' })
    $errorsFound = [Collections.Generic.List[string]]::new()
    $protected = @('src/main/java/darkgrey/rpg/media/StoryMediaCacheIndex.java')
    foreach ($file in $protected) {
        $before = git rev-parse "${baseline}:$file"
        $after = git hash-object -- $file
        if ($before -ne $after) { $errorsFound.Add("Story cache policy changed and requires explicit boundary review: $file") }
    }
    $network = 'src/main/java/darkgrey/rpg/network/DialogueNetwork.java'
    $oldNetwork = (git show "${baseline}:$network") -join "`n"
    $newNetwork = Get-Content -LiteralPath $network -Raw
    foreach ($match in [regex]::Matches($oldNetwork, 'public static final int (\w+DISCRIMINATOR)\s*=\s*(\d+)')) {
        $expected = 'public static final int ' + $match.Groups[1].Value + '\s*=\s*' + $match.Groups[2].Value + '\s*;'
        if ($newNetwork -notmatch $expected) { $errorsFound.Add("Existing message number changed: $($match.Groups[1].Value)") }
    }
    foreach ($file in $implementation) {
        $text = Get-Content -LiteralPath $file -Raw
        if ($file -match 'src/main/' -and $text -match 'register\w*\([^\r\n]*["''](?:wait|enter_story|use_item|block_interact)["'']') {
            $errorsFound.Add("Review forbidden runtime registration: $file")
        }
        if ($file -match 'DialogueBacklog\.java$' -and $text -match 'sendToServer|SavedData|completeTask|resetStory') {
            $errorsFound.Add("Backlog crosses authority boundary: $file")
        }
    }
    Write-Output "BASELINE=$baseline"
    Write-Output "TRACKED_AND_UNTRACKED_SOURCE_FILES=$($implementation.Count)"
    if ($errorsFound.Count) { throw ($errorsFound -join "`n") }
    Write-Output 'SCOPE_SCAN=PASS'
    Write-Output 'This scan is advisory; behavior probes and review remain required.'
} finally { Pop-Location }
