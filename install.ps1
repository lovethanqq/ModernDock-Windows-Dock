[CmdletBinding()]
param(
    [switch]$Launch,
    [string]$InstallRoot = ''
)

$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$installRoot = if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'Programs\ModernDock'
} else {
    [System.IO.Path]::GetFullPath($InstallRoot)
}
$sourceExe = Join-Path $sourceRoot 'ModernDock.exe'
$repoReleaseExe = Join-Path $sourceRoot 'Build\Release\ModernDock.exe'

if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) {
    if (Test-Path -LiteralPath $repoReleaseExe -PathType Leaf) {
        $sourceExe = $repoReleaseExe
    } else {
        throw 'ModernDock.exe was not found beside install.ps1 or under Build\Release.'
    }
}

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
$targetExe = Join-Path $installRoot 'ModernDock.exe'
Copy-Item -LiteralPath $sourceExe -Destination $targetExe -Force

foreach ($name in @('uninstall.ps1', 'restore_windows_taskbar.ps1')) {
    $source = Join-Path $sourceRoot $name
    if (Test-Path -LiteralPath $source -PathType Leaf) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $installRoot $name) -Force
    }
}

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-Item -Path $runKey -Force | Out-Null
Set-ItemProperty -Path $runKey -Name 'ModernDock' -Value ('"' + $targetExe + '"')

Write-Output "Installed ModernDock to $installRoot"
Write-Output 'Existing dock_config.txt, dock_metadata.json and Icons were preserved.'

if ($Launch) {
    Start-Process -FilePath $targetExe -WorkingDirectory $installRoot | Out-Null
    Write-Output 'ModernDock launch requested.'
}
