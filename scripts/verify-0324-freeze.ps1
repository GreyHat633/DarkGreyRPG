param([string]$Baseline = '879fcfc60b6b4876ea1b673541186f63dc8f2792')
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $frozen = @('schema','src/main/java/darkgrey/rpg/graph','src/main/java/darkgrey/rpg/project','src/main/java/darkgrey/rpg/session','src/main/java/darkgrey/rpg/task','src/main/java/darkgrey/rpg/story','src/main/java/darkgrey/rpg/creator/CanonicalTaskPresentationServer.java','src/main/java/darkgrey/rpg/client/CanonicalTaskClientStore.java','src/main/java/darkgrey/rpg/client/session','src/main/java/darkgrey/rpg/network','src/main/java/darkgrey/rpg/identity','src/main/java/darkgrey/rpg/item/identity','src/main/java/darkgrey/rpg/nominator/NominatorActions.java','src/main/java/darkgrey/rpg/nominator/NominatorService.java')
    $changed = @(git diff --name-only $Baseline -- @frozen)
    if ($LASTEXITCODE -ne 0) { throw 'Frozen comparison failed' }
    $changed += @(git ls-files --others --exclude-standard -- @frozen)
    if ($changed.Count) { throw "Frozen changes: $($changed -join ', ')" }
    $allowedStudio = @(
        'studio/src/DarkGreyRPG.Studio/Settings/StudioSettings.cs',
        'studio/src/DarkGreyRPG.Studio/Services/DgrsExportPathPicker.cs',
        'studio/src/DarkGreyRPG.Studio/Services/OfflinePackageDialogs.cs',
        'studio/src/DarkGreyRPG.Studio/MainWindow.xaml.cs',
        'studio/src/DarkGreyRPG.Studio/ViewModels/ShellViewModel.OfflinePackages.cs',
        'studio/src/DarkGreyRPG.Studio.Wpf.Tests/DgrsExportPathPickerTests.cs',
        'studio/src/DarkGreyRPG.Studio.Wpf.Tests/SettingsServiceTests.cs'
    )
    $studio = @(git diff --name-only $Baseline -- studio)
    $studio += @(git ls-files --others --exclude-standard -- studio)
    $outside = @($studio | Where-Object { $_ -notin $allowedStudio })
    if ($outside.Count) { throw "Studio outside file-dialog exception: $($outside -join ', ')" }
    'FROZEN_CANONICAL_RUNTIME_DIFF=0'
    'FROZEN_DGRS_SCHEMA_DIFF=0'
    'FROZEN_IDENTITY_AND_NETWORK_DIFF=0'
    'STUDIO_DIFF_OUTSIDE_FILE_DIALOG_EXCEPTION=0 (path whitelist; semantic diff separately reviewed)'
    'FREEZE_GUARD=PASS'
} finally { Pop-Location }
