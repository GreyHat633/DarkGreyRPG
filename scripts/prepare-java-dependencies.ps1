[CmdletBinding()]
param([string]$SourceDirectory, [switch]$Offline)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$entries = @(
    @{ Path='libs/CustomNPC-Plus-1.11.1-fixed-v1.jar'; Hash='48ab697e63e8cc059592597f88a9fa00fad23e447b553996e1ef04510be2f2d3'; Url=$null },
    @{ Path='libs/jlayer-1.0.1.jar'; Hash='850508c837454a1b06017c32a36876fae516de1e89a829f725fee1e6dcc52000'; Url='https://repo.maven.apache.org/maven2/javazoom/jlayer/1.0.1/jlayer-1.0.1.jar' },
    @{ Path='libs/jlayer-license/jlayer-1.0.1-sources.jar'; Hash='ecde410fc8940ab5d8d5a1d5c585870a3a194f4001701e66a27b8dd8cb7b75ce'; Url='https://repo.maven.apache.org/maven2/javazoom/jlayer/1.0.1/jlayer-1.0.1-sources.jar' },
    @{ Path='libs/+unimixins-all-1.7.10-0.3.1.jar'; Hash='ad0ea4f92daf7bf7ec5c10e16258425e47929126954aea5d80df69ce26cd7318'; Url='https://github.com/LegacyModdingMC/UniMixins/releases/download/0.3.1/%2Bunimixins-all-1.7.10-0.3.1.jar' }
)
foreach ($entry in $entries) {
    $target = Join-Path $root $entry.Path
    for ($item=Get-Item -LiteralPath (Split-Path $target) -ErrorAction Stop; $null -ne $item; $item=$item.Parent) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Dependency directory is a link: $($item.FullName)" }
    }
    if (Test-Path -LiteralPath $target) {
        if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Dependency is a link: $target" }
        if ((Get-FileHash -LiteralPath $target).Hash -eq $entry.Hash) { Write-Host "Verified $($entry.Path)"; continue }
        throw "Existing dependency differs from the locked hash; preserved $target"
    }
    $temporary = $target + '.prepare-' + [guid]::NewGuid().ToString('N')
    try {
        if ($SourceDirectory) { Copy-Item -LiteralPath (Join-Path $SourceDirectory (Split-Path $target -Leaf)) -Destination $temporary }
        elseif (-not $entry.Url) { throw 'Exact CNPC 1.11.1-fixed-v1 is missing. Its patched source is unverified; provide a lawful exact copy with -SourceDirectory. No upstream replacement is selected.' }
        elseif ($Offline) { throw "Offline dependency missing: $($entry.Path)" }
        else { Invoke-WebRequest -Uri $entry.Url -OutFile $temporary }
        if ((Get-FileHash -LiteralPath $temporary).Hash -ne $entry.Hash) { throw "Dependency SHA-256 mismatch: $($entry.Path)" }
        Move-Item -LiteralPath $temporary -Destination $target
        Write-Host "Prepared $($entry.Path)"
    } finally {
        # Only the exact file created by this invocation is removed.
        if (Test-Path -LiteralPath $temporary -PathType Leaf) { Remove-Item -LiteralPath $temporary -Force }
    }
}
