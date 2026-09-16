[CmdletBinding()]
param(
    [string]$OutputDirectory = 'PLAN/0.3.3.0/evidence',
    [string[]]$ProjectPaths = @(
        'E:\Java\MinecraftMod\RPGProject\TestProject',
        'E:\Java\MinecraftMod\RPGProject\TestProject_2'
    )
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Force -Path $output | Out-Null
Push-Location $root
try {
    $roots = @('src', 'studio/src', 'schema', 'examples', 'docs', 'scripts', 'legacy', 'README.md')
    $files = @(git -c core.quotepath=false ls-files --cached --others --exclude-standard -- @roots)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inventory source files.' }
    $pattern = 'StoryStartSchema|CanonicalStory(StartConfiguration|TriggerIndex|RegionEntryTracker|RepeatPolicy|WaitKind|Snapshot)|\btriggers\b|\benter_region\b|\binteract_actor\b|\benter_story\b|\btrigger_properties\b|\brepeat_policy\b|\btrigger_port_id\b'
    $rows = foreach ($file in $files | Sort-Object -Unique) {
        if ($file -notmatch '\.(java|cs|xaml|json|md|ps1|txt|gd)$' -or !(Test-Path -LiteralPath $file -PathType Leaf)) { continue }
        if ($file -like 'scripts/*0330*') { continue }
        $matches = @(Select-String -LiteralPath $file -Pattern $pattern)
        if (!$matches.Count) { continue }
        $domain = switch -Regex ($file) {
            '^src/main/' { 'Runtime'; break }
            '^src/test/' { 'Runtime probe'; break }
            '^studio/src/.*Tests/' { 'Studio test'; break }
            '^studio/src/DarkGreyRPG.Studio.Core/' { 'Studio Core'; break }
            '^studio/src/DarkGreyRPG.Studio/' { 'Studio UI'; break }
            '^schema/' { 'Schema'; break }
            '^examples/' { 'Example'; break }
            '^legacy/' { 'Historical archive'; break }
            default { 'Documentation/tool'; break }
        }
        [pscustomobject]@{
            Path = $file
            Domain = $domain
            MatchingLines = $matches.Count
            LineNumbers = ($matches.LineNumber -join ',')
            Symbols = (@($matches | ForEach-Object { [regex]::Matches($_.Line, $pattern).Value }) | Sort-Object -Unique) -join ','
            SHA256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        }
    }
    $rows | Export-Csv (Join-Path $output 'legacy-source-inventory.csv') -NoTypeInformation -Encoding utf8

    # Read current editable projects and active Runtime project fixtures; never rewrite them.
    $projects = @($ProjectPaths) + @((Join-Path $root 'run/client/DarkGreyRPG/Project'), (Join-Path $root 'run/client/darkgrey_rpg_project'), (Join-Path $root 'run/server/DarkGreyRPG/Project'), (Join-Path $root 'run/server/darkgrey_rpg_project'))
    $projectRows = foreach ($project in $projects) {
        if (!(Test-Path -LiteralPath $project -PathType Container)) {
            [pscustomobject]@{ Project = $project; Path = ''; Status = 'MISSING'; SHA256 = ''; LegacyNodes = ''; StartProperties = '' }
            continue
        }
        $canonical = Join-Path $project 'resources/canonical'
        $jsonFiles = if (Test-Path -LiteralPath $canonical) { @(Get-ChildItem -LiteralPath $canonical -Recurse -File -Filter *.json) } else { @() }
        foreach ($file in $jsonFiles) {
            $doc = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
            $oldNodes = @($doc.graph.nodes | Where-Object { $_.type -in @('enter_region', 'enter_story', 'narration') -or ($_.type -eq 'interact_actor' -and $doc.resource_kind -eq 'story') })
            $starts = @($doc.graph.nodes | Where-Object { $_.type -eq 'start' -and $doc.resource_kind -eq 'story' })
            $startProperties = @($starts | ForEach-Object { $_.properties.PSObject.Properties.Name } | Sort-Object -Unique)
            [pscustomobject]@{
                Project = $project; Path = $file.FullName; Status = 'READ_ONLY'
                SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
                LegacyNodes = ($oldNodes.type -join ','); StartProperties = ($startProperties -join ',')
            }
        }
        if (!$jsonFiles.Count) {
            [pscustomobject]@{ Project = $project; Path = $canonical; Status = 'NO_CANONICAL_GRAPHS'; SHA256 = ''; LegacyNodes = ''; StartProperties = '' }
        }
    }
    $projectRows | Export-Csv (Join-Path $output 'legacy-project-inventory.csv') -NoTypeInformation -Encoding utf8

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $packageDirectories = @('run/client/DarkGreyRPG/StoryPackages', 'run/server/DarkGreyRPG/StoryPackages', 'run/client/darkgrey_rpg_story_packages', 'run/server/darkgrey_rpg_story_packages', 'PLAN/0.3.2.4/evidence', '.tooling/0.3.3.0/fixtures/legacy-dgrs', '.tooling/0.3.3.0/fixtures/objective0324')
    $packageRows = foreach ($directory in $packageDirectories) {
        if (!(Test-Path -LiteralPath $directory)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $directory -File -Filter *.dgrs) {
            $zip = [IO.Compression.ZipFile]::OpenRead($file.FullName)
            try {
                foreach ($entry in $zip.Entries | Where-Object { $_.FullName -match '\.json$' }) {
                    $reader = [IO.StreamReader]::new($entry.Open())
                    try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
                    if ($content -notmatch $pattern -and $content -notmatch '"narration"') { continue }
                    [pscustomobject]@{
                        Archive = $file.FullName; Entry = $entry.FullName
                        Symbols = ([regex]::Matches($content, "$pattern|\bnarration\b").Value | Sort-Object -Unique) -join ','
                        ArchiveSHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
                    }
                }
            } finally { $zip.Dispose() }
        }
    }
    $packageRows | Export-Csv (Join-Path $output 'legacy-package-inventory.csv') -NoTypeInformation -Encoding utf8
    "SOURCE_CANDIDATE_FILES=$(@($rows).Count)"
    $rows | Group-Object Domain | ForEach-Object { "$($_.Name)=$($_.Count)" }
    "PROJECT_INVENTORY_ROWS=$(@($projectRows).Count)"
    "PACKAGE_MATCHING_ENTRIES=$(@($packageRows).Count)"
    'INVENTORY=READ_ONLY; keyword candidates require domain review, not blind deletion'
} finally { Pop-Location }
