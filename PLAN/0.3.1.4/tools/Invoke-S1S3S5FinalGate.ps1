[CmdletBinding()]
param(
    [string] $RepositoryRoot = 'E:\Java\MinecraftMod\DarkGrey_RPG',
    [string] $ExePath = 'E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe',
    [string] $ProjectDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-s20-authoring-project',
    [string] $SettingsPath = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-s1-s3-s5-settings.json',
    [string] $EvidenceDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\PLAN\0.3.1.4\evidence\final-live-post-choice-layout\s1-s3-s5'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class S1S3S5Native {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
'@

$repo = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
$tooling = [IO.Path]::GetFullPath((Join-Path $repo '.tooling')).TrimEnd('\')
$planEvidence = [IO.Path]::GetFullPath((Join-Path $repo 'PLAN\0.3.1.4\evidence')).TrimEnd('\')
$project = [IO.Path]::GetFullPath($ProjectDirectory).TrimEnd('\')
$exe = [IO.Path]::GetFullPath($ExePath)
$settings = [IO.Path]::GetFullPath($SettingsPath)
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory).TrimEnd('\')
$resultPath = Join-Path $evidence 's1-s3-s5-final-gate.json'
$storyPath = Join-Path $project 'resources\canonical\stories\slime_cleanup_request.json'
$taskPath = Join-Path $project 'resources\canonical\tasks\slime_cleanup_task.json'
if (-not $project.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Gate project must remain below .tooling.' }
if (-not $settings.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Gate settings must remain below .tooling.' }
if (-not ($evidence.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase) -or $evidence.StartsWith($planEvidence + '\', [StringComparison]::OrdinalIgnoreCase))) { throw 'Gate evidence must remain below .tooling or PLAN/0.3.1.4/evidence.' }
foreach ($required in @($exe,$storyPath,$taskPath)) { if (-not (Test-Path -LiteralPath $required)) { throw "Required path missing: $required" } }
New-Item -ItemType Directory -Path $evidence -Force | Out-Null

$desktop = [Windows.Automation.AutomationElement]::RootElement
$walker = [Windows.Automation.TreeWalker]::RawViewWalker
$process = $null
$state = [ordered]@{
    schema_version = 1
    status = 'FAIL'
    started_utc = [DateTime]::UtcNow.ToString('o')
    exe = $exe
    exe_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $exe).Hash
    exe_product_version = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
    exe_file_version = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
    exe_bytes = (Get-Item -LiteralPath $exe).Length
    project = $project
    evidence_directory = $evidence
    screenshots = [Collections.Generic.List[string]]::new()
    gates = [Collections.Generic.List[object]]::new()
}

function Visible($element) {
    if ($null -eq $element) { return $false }
    try { $r=$element.Current.BoundingRectangle; return (-not $element.Current.IsOffscreen -and $r.Width -gt 0 -and $r.Height -gt 0) } catch { return $false }
}
function Find-Id($root, [string] $id, [int] $timeout = 12) {
    $condition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id)
    $until=[DateTime]::UtcNow.AddSeconds($timeout)
    do { try { foreach($x in $root.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)){ if(Visible $x){ return $x } } } catch {}; Start-Sleep -Milliseconds 80 } while([DateTime]::UtcNow -lt $until)
    throw "Visible AutomationId '$id' was not found."
}
function Try-FindId($root, [string] $id) {
    $condition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id)
    try { foreach($x in $root.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)){ if(Visible $x){ return $x } } } catch {}
    return $null
}
function Find-Name($root, [string] $name, [int] $timeout = 12) {
    $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$name)
    $until=[DateTime]::UtcNow.AddSeconds($timeout)
    do { try { foreach($x in $root.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)){ if(Visible $x){ return $x } } } catch {}; Start-Sleep -Milliseconds 80 } while([DateTime]::UtcNow -lt $until)
    throw "Visible element named '$name' was not found."
}
function Ancestor($element, [Windows.Automation.ControlType] $type) {
    for($i=0;$i -lt 20 -and $null -ne $element;$i++){ if($element.Current.ControlType -eq $type){ return $element }; $element=$walker.GetParent($element) }
    return $null
}
function Get-ProcessWindows([int] $processId) {
    $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$processId)
    @($desktop.FindAll([Windows.Automation.TreeScope]::Descendants,$condition) | Where-Object { Visible $_ })
}
function Find-Main([int] $processId, [int] $timeout=25) {
    $until=[DateTime]::UtcNow.AddSeconds($timeout)
    do { foreach($w in (Get-ProcessWindows $processId)){ if($w.Current.AutomationId -eq 'RootWindow'){ return $w } }; Start-Sleep -Milliseconds 100 } while([DateTime]::UtcNow -lt $until)
    throw "Studio RootWindow for process $processId was not found."
}
function Focus-Main($main,[int]$settle=180) {
    if($null -eq $main){throw 'Studio main window is unavailable.'}
    [S1S3S5Native]::SetForegroundWindow($main.Current.NativeWindowHandle)|Out-Null
    Start-Sleep -Milliseconds $settle
}
function Get-ModalWindows([int] $processId) {
    @(Get-ProcessWindows $processId | Where-Object { $_.Current.AutomationId -ne 'RootWindow' -and $_.Current.ControlType -eq [Windows.Automation.ControlType]::Window })
}
function Drag-Point([double]$x1,[double]$y1,[double]$x2,[double]$y2,[switch]$right) {
    [S1S3S5Native]::SetCursorPos([int]$x1,[int]$y1)|Out-Null
    [S1S3S5Native]::mouse_event($(if($right){8}else{2}),0,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 100
    if([math]::Abs($x2-$x1)-gt 1 -or [math]::Abs($y2-$y1)-gt 1){ 1..8 | ForEach-Object { $t=$_/8.0; [S1S3S5Native]::SetCursorPos([int]($x1+(($x2-$x1)*$t)),[int]($y1+(($y2-$y1)*$t)))|Out-Null; Start-Sleep -Milliseconds 30 } }
    [S1S3S5Native]::mouse_event($(if($right){16}else{4}),0,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 250
}
function Click-Point([double]$x,[double]$y,[switch]$right){ Drag-Point $x $y $x $y -right:$right }
function Click-Element($element){ $r=$element.Current.BoundingRectangle; if($r.Width -le 0 -or $r.Height -le 0){ throw 'Element has no click target.' }; Click-Point ($r.Left+$r.Width/2) ($r.Top+$r.Height/2) }
function Invoke-Ui($element) {
    $pattern=$null
    if($element.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern,[ref]$pattern)){ $pattern.Invoke(); Start-Sleep -Milliseconds 250 } else { Click-Element $element }
}
function Send-Key([byte]$key,[int]$settle=250){ [S1S3S5Native]::keybd_event($key,0,0,[UIntPtr]::Zero); [S1S3S5Native]::keybd_event($key,0,2,[UIntPtr]::Zero); Start-Sleep -Milliseconds $settle }
function Send-Chord([byte[]]$keys,[int]$settle=500){ foreach($key in $keys){ [S1S3S5Native]::keybd_event($key,0,0,[UIntPtr]::Zero) }; for($i=$keys.Count-1;$i -ge 0;$i--){ [S1S3S5Native]::keybd_event($keys[$i],0,2,[UIntPtr]::Zero) }; Start-Sleep -Milliseconds $settle }
function Send-LowerAscii([string]$text){
    foreach($character in $text.ToCharArray()){
        $code=[int][char]$character
        if($code -ge 97 -and $code -le 122){Send-Key ([byte]($code-32)) 35}
        elseif($code -ge 48 -and $code -le 57){Send-Key ([byte]$code) 35}
        else{throw "Unsupported physical ASCII character '$character'."}
    }
    Start-Sleep -Milliseconds 180
}
function Screenshot($window,[string]$name){
    Focus-Main $window 250
    $r=$window.Current.BoundingRectangle; $bmp=[Drawing.Bitmap]::new([int]$r.Width,[int]$r.Height)
    try { $g=[Drawing.Graphics]::FromImage($bmp); try{$g.CopyFromScreen([int]$r.Left,[int]$r.Top,0,0,$bmp.Size)}finally{$g.Dispose()}; $path=Join-Path $evidence $name; $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png); $full=[IO.Path]::GetFullPath($path); $state.screenshots.Add($full); return $full } finally { $bmp.Dispose() }
}
function Configure-Settings {
    $cfg=[ordered]@{schema_version=1;theme='Dark';window_width=1540;window_height=900;window_maximized=$false;last_project=$project;recent_projects=@($project)}
    [IO.File]::WriteAllText($settings,($cfg|ConvertTo-Json -Depth 5)+"`n",[Text.UTF8Encoding]::new($false))
}
function Start-Studio {
    Configure-Settings
    $si=[Diagnostics.ProcessStartInfo]::new(); $si.FileName=$exe; $si.WorkingDirectory=Split-Path -Parent $exe; $si.UseShellExecute=$false; $si.Environment['DARKGREYRPG_STUDIO_SETTINGS_PATH']=$settings
    $script:process=[Diagnostics.Process]::Start($si); $main=Find-Main $process.Id; [S1S3S5Native]::MoveWindow($main.Current.NativeWindowHandle,20,20,1540,900,$true)|Out-Null; [S1S3S5Native]::SetForegroundWindow($main.Current.NativeWindowHandle)|Out-Null; Start-Sleep -Milliseconds 900; return $main
}
function Stop-Studio {
    if($null -eq $process -or $process.HasExited){ return }
    $process.CloseMainWindow()|Out-Null; if(-not $process.WaitForExit(10000)){ $process.Kill(); $process.WaitForExit(5000) }; $script:process=$null
}
function Enter-Story($main){
    Focus-Main $main
    $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'史莱姆清理委托'); $mainLeft=$main.Current.BoundingRectangle.Left; $item=$null
    foreach($label in $main.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)){ if(-not(Visible $label) -or $label.Current.BoundingRectangle.Left -ge ($mainLeft+350)){continue}; $candidate=Ancestor $label ([Windows.Automation.ControlType]::ListItem); if($null -ne $candidate){$item=$candidate;break} }
    if($null -eq $item){throw 'Left Story ListItem was not found.'}; $selectionItem=$null; if(-not $item.TryGetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern,[ref]$selectionItem)){throw 'Left Story ListItem has no SelectionItemPattern.'}; $selectionItem.Select(); Start-Sleep -Milliseconds 350
    $until=[DateTime]::UtcNow.AddSeconds(8); $enter=$null
    do { try{$enter=Find-Name $main '进入选中故事' 1;if(-not $enter.Current.IsEnabled){$enter=$null}}catch{$enter=$null};if($null -eq $enter){Start-Sleep -Milliseconds 80} }while($null -eq $enter -and [DateTime]::UtcNow -lt $until)
    if($null -eq $enter){throw 'Enter selected story button did not become enabled.'}; Click-Element $enter; Find-Id $main 'CanonicalStoryWorkspace' 15|Out-Null
}
function Expand-Folder($main,[string]$folder){
    $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$folder)
    foreach($candidate in $main.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)){ $pattern=$null; if($candidate.TryGetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern,[ref]$pattern)){ if($pattern.Current.ExpandCollapseState -eq [Windows.Automation.ExpandCollapseState]::Collapsed){$pattern.Expand();Start-Sleep -Milliseconds 180}; return } }
    throw "Folder expander '$folder' was not found."
}
function Open-Resource($main,[string]$folder,[string]$displayName){
    Focus-Main $main
    Expand-Folder $main $folder
    $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$displayName); $mainLeft=$main.Current.BoundingRectangle.Left; $button=$null
    foreach($label in $main.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)){ if(-not (Visible $label)){continue}; $candidate=Ancestor $label ([Windows.Automation.ControlType]::Button); if($null -ne $candidate -and $candidate.Current.BoundingRectangle.Left -lt ($mainLeft+400)){ $button=$candidate;break } }
    if($null -eq $button){throw "Left resource row '$displayName' was not found."}; $r=$button.Current.BoundingRectangle; Click-Point ($r.Left+$r.Width/2) ($r.Top+$r.Height/2) -right; Click-Element (Find-Name $desktop '编辑' 5); Find-Id $main 'CanonicalStoryWorkspaceGraph' 12|Out-Null; Start-Sleep -Milliseconds 400
}
function Select-NodePhysical($main,[string]$nodeId){ Focus-Main $main; $node=Find-Id $main ("CanonicalGraphNode_{0}" -f $nodeId); $r=$node.Current.BoundingRectangle; Click-Point ($r.Left+[math]::Min(80,$r.Width/3)) ($r.Top+18); Start-Sleep -Milliseconds 350 }
function Get-Value($element){ $pattern=$null; if(-not $element.TryGetCurrentPattern([Windows.Automation.ValuePattern]::Pattern,[ref]$pattern)){throw "Element '$($element.Current.AutomationId)' has no ValuePattern."}; return [string]$pattern.Current.Value }
function Get-ComboSelection($combo){ $pattern=$null; if(-not $combo.TryGetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern,[ref]$pattern)){throw "Combo '$($combo.Current.AutomationId)' has no SelectionPattern."}; $selection=@($pattern.Current.GetSelection()); if($selection.Count -ne 1){return ''}; return [string]$selection[0].Current.Name }
function Select-ComboValuePhysical($main,[string]$automationId,[string]$exactName){
    Focus-Main $main 80
    $combo=Find-Id $main $automationId; $bounds=$combo.Current.BoundingRectangle; Click-Element $combo
    $until=[DateTime]::UtcNow.AddSeconds(5); $item=$null
    do { try { $items=@($desktop.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$exactName)) | Where-Object { (Visible $_) -and $_.Current.BoundingRectangle.Top -ge ($bounds.Bottom-2) -and [math]::Abs($_.Current.BoundingRectangle.Left-$bounds.Left) -lt 160 }); if($items.Count -gt 0){$item=$items|Sort-Object {[math]::Abs($_.Current.BoundingRectangle.Left-$bounds.Left)}|Select-Object -First 1} }catch{}; if($null -eq $item){Start-Sleep -Milliseconds 40} }while($null -eq $item -and [DateTime]::UtcNow -lt $until)
    if($null -eq $item){throw "Popup item '$exactName' was not found for $automationId."}; Click-Element $item
    $until=[DateTime]::UtcNow.AddSeconds(5); do { try{ $current=Find-Id $main $automationId 1; if((Get-ComboSelection $current) -like "*$exactName*"){return} }catch{}; Start-Sleep -Milliseconds 40 }while([DateTime]::UtcNow -lt $until)
    throw "$automationId did not retain physical popup selection '$exactName'."
}
function Normalize-Task($task){
    $nodes=@($task.graph.nodes|ForEach-Object{[ordered]@{id=[string]$_.id;type=[string]$_.type;ports=@($_.ports|ForEach-Object{"$($_.port_id)|$($_.kind)|$($_.direction)"}|Sort-Object)}}|Sort-Object id)
    $connections=@($task.graph.connections|ForEach-Object{"$($_.from_node_id)|$($_.from_port_id)|$($_.to_node_id)|$($_.to_port_id)|$($_.interface_kind)"}|Sort-Object)
    return [ordered]@{nodes=$nodes;connections=$connections}|ConvertTo-Json -Depth 10 -Compress
}
function Gate([string]$id,[string]$description,[scriptblock]$action){ $entry=[ordered]@{id=$id;description=$description;status='FAIL';started_utc=[DateTime]::UtcNow.ToString('o')}; try{$details=&$action;$entry.status='PASS';$entry.details=$details}catch{$entry.error=$_.Exception.Message;$state.gates.Add([pscustomobject]$entry);throw};$entry.completed_utc=[DateTime]::UtcNow.ToString('o');$state.gates.Add([pscustomobject]$entry) }

try {
    $main=Start-Studio
    Enter-Story $main
    Gate 'S1' 'Text Draft remains empty for two seconds, accepts a new name, and commits on Enter' {
        Select-NodePhysical $main 'start'
        $edit=Find-Id $main 'StoryStartTriggerName'; Click-Element $edit; Send-Chord @(0x11,0x41) 100; Send-Key 0x2E 100
        $emptyImmediate=Get-Value (Find-Id $main 'StoryStartTriggerName')
        if($emptyImmediate -ne ''){throw "Text Draft was not empty after Ctrl+A/Delete: '$emptyImmediate'."}
        Start-Sleep -Milliseconds 2100
        $emptyAfterTwoSeconds=Get-Value (Find-Id $main 'StoryStartTriggerName')
        if($emptyAfterTwoSeconds -ne ''){throw "Old value was forced back during the two-second draft window: '$emptyAfterTwoSeconds'."}
        Screenshot $main 's1-empty-after-two-seconds.png'|Out-Null
        $edit=Find-Id $main 'StoryStartTriggerName'; Click-Element $edit; Send-Key 0x10 120; Send-LowerAscii 's1finaldraft'
        $typed=Get-Value (Find-Id $main 'StoryStartTriggerName'); if($typed -ne 's1finaldraft'){throw "Typed draft mismatch: '$typed'."}
        Send-Key 0x0D 700
        $committed=Get-Value (Find-Id $main 'StoryStartTriggerName'); if($committed -ne 's1finaldraft'){throw "Enter commit mismatch: '$committed'."}
        Send-Chord @(0x11,0x10,0x53) 900
        $story=Get-Content -LiteralPath $storyPath -Raw|ConvertFrom-Json; $persisted=[string]$story.graph.nodes[0].properties.triggers[0].display_name
        if($persisted -ne 's1finaldraft'){throw "Persisted trigger name mismatch: '$persisted'."}
        Screenshot $main 's1-enter-committed.png'|Out-Null
        [ordered]@{empty_immediate=$emptyImmediate;empty_after_2100_ms=$emptyAfterTwoSeconds;typed_value=$typed;committed_value=$committed;persisted_value=$persisted;physical_input='Ctrl+A, Delete, SendKeys, Enter'}
    }
    Gate 'S3' 'Objective target switches between actor and actor group 100 times without crash' {
        Open-Resource $main '任务' '清理史莱姆'; $task=Get-Content -LiteralPath $taskPath -Raw|ConvertFrom-Json; $objective=@($task.graph.nodes|Where-Object type -eq 'objective'|Select-Object -First 1); if($null -eq $objective){throw 'Persisted Objective node was not found.'}; $objectiveId=[string]$objective.id; Select-NodePhysical $main $objectiveId
        $durations=[Collections.Generic.List[double]]::new(); $completed=0
        for($i=0;$i -lt 100;$i++){ if($process.HasExited){throw "Studio exited after $completed target switches."}; $target=if(($i%2)-eq 0){'酒馆老板'}else{'史莱姆'}; $sw=[Diagnostics.Stopwatch]::StartNew(); Select-ComboValuePhysical $main 'TaskObjectiveActorSelector' $target; $sw.Stop(); $durations.Add($sw.Elapsed.TotalMilliseconds); $completed++ }
        if($completed -ne 100 -or $process.HasExited -or -not $process.Responding){throw "Objective target loop ended unhealthy: completed=$completed exited=$($process.HasExited) responding=$($process.Responding)."}
        $final=Get-ComboSelection (Find-Id $main 'TaskObjectiveActorSelector'); if($final -notlike '*史莱姆*'){throw "Unexpected final Objective selection '$final'."}
        Send-Chord @(0x11,0x10,0x53) 900; $persisted=Get-Content -LiteralPath $taskPath -Raw|ConvertFrom-Json; $savedObjective=@($persisted.graph.nodes|Where-Object type -eq 'objective'|Select-Object -First 1); if([string]$savedObjective.properties.entity -ne 'slimes'){throw "Persisted Objective entity is '$($savedObjective.properties.entity)', expected 'slimes'."}
        Screenshot $main 's3-after-100-switches.png'|Out-Null
        $sorted=@($durations|Sort-Object); $p95Index=[math]::Min($sorted.Count-1,[math]::Ceiling($sorted.Count*0.95)-1)
        [ordered]@{completed_switches=$completed;actor='酒馆老板';actor_id='tavern_boss';actor_group='史莱姆';actor_group_id='slimes';final_selection=$final;persisted_entity=[string]$savedObjective.properties.entity;crash_count=0;median_ms=[math]::Round(($sorted[49]+$sorted[50])/2,1);p95_ms=[math]::Round($sorted[$p95Index],1);max_ms=[math]::Round(($sorted|Measure-Object -Maximum).Maximum,1)}
    }
    Gate 'S5' 'Ordinary node deletes without confirmation, Ctrl+Z restores it and its edge, fixed Settlement rejects delete' {
        $before=Get-Content -LiteralPath $taskPath -Raw|ConvertFrom-Json; $beforeSemantic=Normalize-Task $before; $objective=@($before.graph.nodes|Where-Object type -eq 'objective'|Select-Object -First 1); $objectiveId=[string]$objective.id; $beforeNodeCount=@($before.graph.nodes).Count; $beforeConnectionCount=@($before.graph.connections).Count
        Select-NodePhysical $main $objectiveId; $modalBefore=@(Get-ModalWindows $process.Id).Count; Send-Key 0x2E 650
        if($null -ne (Try-FindId $main ("CanonicalGraphNode_{0}"-f $objectiveId))){throw 'Ordinary Objective node remained visible after Delete.'}; $modalAfter=@(Get-ModalWindows $process.Id).Count; if($modalAfter -gt $modalBefore){throw 'Delete opened an unexpected confirmation dialog.'}
        Screenshot $main 's5-after-delete-no-confirmation.png'|Out-Null
        Send-Chord @(0x11,0x5A) 900; Find-Id $main ("CanonicalGraphNode_{0}"-f $objectiveId) 5|Out-Null
        $objectivePort=Find-Id $main ("CanonicalGraphPort_{0}_logic_status"-f $objectiveId) 5; if($null -eq $objectivePort){throw 'Objective logic output did not return after Ctrl+Z.'}
        Screenshot $main 's5-after-undo-restored.png'|Out-Null
        Select-NodePhysical $main 'settle'; Send-Key 0x2E 650; Find-Id $main 'CanonicalGraphNode_settle' 3|Out-Null; if(@(Get-ModalWindows $process.Id).Count -gt $modalBefore){throw 'Fixed Settlement delete opened an unexpected modal.'}
        Screenshot $main 's5-fixed-settlement-rejected-delete.png'|Out-Null
        Send-Chord @(0x11,0x10,0x53) 900; $after=Get-Content -LiteralPath $taskPath -Raw|ConvertFrom-Json; $afterSemantic=Normalize-Task $after
        if($beforeSemantic -ne $afterSemantic){throw 'Task node/port/connection semantics did not return to the pre-delete state after Ctrl+Z.'}
        [ordered]@{ordinary_node_id=$objectiveId;delete_confirmation_dialog=$false;node_absent_after_delete=$true;undo_restored_node=$true;undo_restored_output_port=$true;fixed_node_id='settle';fixed_node_still_present=$true;before_node_count=$beforeNodeCount;after_node_count=@($after.graph.nodes).Count;before_connection_count=$beforeConnectionCount;after_connection_count=@($after.graph.connections).Count;disk_semantics_restored=$true}
    }
    Stop-Studio
    $state.status='PASS'
} catch {
    $state.error=$_.Exception.Message
    try{if($null -ne $process -and -not $process.HasExited){$failureMain=Find-Main $process.Id 2;Screenshot $failureMain 'failure.png'|Out-Null}}catch{}
    try{Stop-Studio}catch{}
} finally {
    $state.completed_utc=[DateTime]::UtcNow.ToString('o')
    [IO.File]::WriteAllText($resultPath,($state|ConvertTo-Json -Depth 20)+"`n",[Text.UTF8Encoding]::new($false))
}

$state|ConvertTo-Json -Depth 20
if($state.status -ne 'PASS'){exit 1}
