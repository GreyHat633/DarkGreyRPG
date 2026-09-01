param(
    [Parameter(Mandatory = $true)]
    [int] $ProcessId,

    [int] $Count = 25,

    [int] $Offset = 0,

    [string] $AutomationId = 'StoryStartTriggerType',

    [switch] $Keyboard,

    [string[]] $Sequence
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

if (-not ('Stage1UiWin32' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Stage1UiWin32 {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll")] public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
'@
}

$processCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
    $ProcessId)
$listItemCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::ListItem)

function Get-StudioRoot {
    $roots = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        $processCondition)
    foreach ($candidate in $roots) {
        if ($candidate.Current.AutomationId -eq 'RootWindow') {
            return $candidate
        }
    }
    return $null
}

function Find-ElementById {
    param([string] $AutomationId, [int] $TimeoutMs = 3000)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.ElapsedMilliseconds -lt $TimeoutMs) {
        try {
            $root = Get-StudioRoot
            if ($null -ne $root) {
                $element = $root.FindFirst(
                    [System.Windows.Automation.TreeScope]::Descendants,
                    (New-Object System.Windows.Automation.PropertyCondition(
                        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
                        $AutomationId)))
                if ($null -ne $element) {
                    return $element
                }
            }
        } catch {
            # The editor intentionally replaces the trigger item during projection.
        }
        Start-Sleep -Milliseconds 4
    }
    throw "Timed out locating $AutomationId."
}

function Get-SelectedItemName {
    param([string] $AutomationId)
    $combo = Find-ElementById $AutomationId
    $selection = $combo.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)
    $selected = $selection.Current.GetSelection()
    if ($selected.Count -eq 0) {
        return ''
    }
    return $selected[0].Current.Name
}

function Try-GetSelectedItemName {
    param([string] $AutomationId)
    try { return Get-SelectedItemName $AutomationId } catch { return $null }
}

function Select-StableValue {
    param([string] $AutomationId, [string] $StableValue)

    $combo = Find-ElementById $AutomationId

    if ($Keyboard) {
        $bounds = $combo.Current.BoundingRectangle
        [Stage1UiWin32]::SetCursorPos(
            [int]($bounds.Left + ($bounds.Width / 2)),
            [int]($bounds.Top + ($bounds.Height / 2))) | Out-Null
        [Stage1UiWin32]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
        [Stage1UiWin32]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 20

        $downCount = switch ($StableValue) {
            'interact_actor' { 0 }
            'enter_region' { 1 }
            'logic' { 2 }
            default { throw "Unknown stable value $StableValue." }
        }
        foreach ($virtualKey in @(36) + (@(40) * $downCount) + @(13)) {
            [Stage1UiWin32]::keybd_event($virtualKey, 0, 0, [UIntPtr]::Zero)
            [Stage1UiWin32]::keybd_event($virtualKey, 0, 2, [UIntPtr]::Zero)
        }

        $timer = [Diagnostics.Stopwatch]::StartNew()
        while ($timer.ElapsedMilliseconds -lt 3000) {
            try {
                if ((Get-SelectedItemName $AutomationId) -like "*Value = $StableValue,*") {
                    return
                }
            } catch {
                # Retry until the replacement ComboBox is available.
            }
            Start-Sleep -Milliseconds 4
        }
        throw "Timed out waiting for $StableValue keyboard selection."
    }

    $expand = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()

    $timer = [Diagnostics.Stopwatch]::StartNew()
    $item = $null
    while ($timer.ElapsedMilliseconds -lt 3000 -and $null -eq $item) {
        try {
            $root = Get-StudioRoot
            $items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $listItemCondition)
            foreach ($candidate in $items) {
                if ($candidate.Current.Name -like "*Value = $StableValue,*" -and
                    $candidate.Current.BoundingRectangle.Width -gt 100) {
                    $item = $candidate
                    break
                }
            }
        } catch {
            # Retry while the popup is materializing.
        }
        if ($null -eq $item) {
            Start-Sleep -Milliseconds 4
        }
    }
    if ($null -eq $item) {
        throw "Timed out locating popup value $StableValue."
    }

    $bounds = $item.Current.BoundingRectangle
    [Stage1UiWin32]::SetCursorPos(
        [int]($bounds.Left + ($bounds.Width / 2)),
        [int]($bounds.Top + ($bounds.Height / 2))) | Out-Null
    [Stage1UiWin32]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [Stage1UiWin32]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)

    $timer.Restart()
    while ($timer.ElapsedMilliseconds -lt 3000) {
        try {
            if ((Get-SelectedItemName $AutomationId) -like "*Value = $StableValue,*") {
                return
            }
        } catch {
            # Retry until the replacement ComboBox is available.
        }
        Start-Sleep -Milliseconds 4
    }
    throw "Timed out waiting for $StableValue selection."
}

$studio = Get-Process -Id $ProcessId
[Stage1UiWin32]::ShowWindowAsync($studio.MainWindowHandle, 9) | Out-Null
[Stage1UiWin32]::SetWindowPos($studio.MainWindowHandle, [IntPtr](-1), 0, 0, 0, 0, 0x13) | Out-Null
[Stage1UiWin32]::SetWindowPos($studio.MainWindowHandle, [IntPtr](-2), 0, 0, 0, 0, 0x13) | Out-Null
[Stage1UiWin32]::SetForegroundWindow($studio.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 100

$values = @(if ($Sequence.Count -gt 0) { $Sequence } else { 'logic'; 'enter_region'; 'interact_actor' })
$times = [Collections.Generic.List[double]]::new()
for ($index = 0; $index -lt $Count; $index++) {
    $value = $values[($index + $Offset) % $values.Count]
    $timer = [Diagnostics.Stopwatch]::StartNew()
    Select-StableValue $AutomationId $value
    $timer.Stop()
    $times.Add($timer.Elapsed.TotalMilliseconds)
    Write-Host "switch=$($index + 1)/$Count value=$value elapsed_ms=$([math]::Round($timer.Elapsed.TotalMilliseconds, 2))"
}

$studio.Refresh()
[pscustomobject]@{
    ProcessId = $ProcessId
    Count = $times.Count
    Offset = $Offset
    TimesMs = @($times | ForEach-Object { [math]::Round($_, 3) })
    AutomationId = $AutomationId
    Selected = Get-SelectedItemName $AutomationId
    Right = if ($AutomationId -in @('StoryStartTriggerType', 'InlineStartTriggerType')) {
        Try-GetSelectedItemName 'StoryStartTriggerType'
    } else { $null }
    Inline = if ($AutomationId -in @('StoryStartTriggerType', 'InlineStartTriggerType')) {
        Try-GetSelectedItemName 'InlineStartTriggerType'
    } else { $null }
    Responding = $studio.Responding
} | ConvertTo-Json -Depth 4 -Compress
