[CmdletBinding()]
param(
    [switch]$RemoveUserData,
    [string]$InstallRoot = ''
)

$ErrorActionPreference = 'Stop'
$installRoot = if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'Programs\ModernDock'
} else {
    [System.IO.Path]::GetFullPath($InstallRoot)
}
$targetExe = Join-Path $installRoot 'ModernDock.exe'
$restoreScript = Join-Path $PSScriptRoot 'restore_windows_taskbar.ps1'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

if (Test-Path -LiteralPath $restoreScript -PathType Leaf) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $restoreScript | Out-Host
}

$processes = @(Get-CimInstance Win32_Process -Filter "Name = 'ModernDock.exe'" | Where-Object {
    $_.ExecutablePath -and ($_.ExecutablePath -ieq $targetExe)
})
foreach ($process in $processes) {
    Stop-Process -Id ([int]$process.ProcessId) -Force -ErrorAction SilentlyContinue
}

if (Test-Path -LiteralPath $runKey) {
    Remove-ItemProperty -Path $runKey -Name 'ModernDock' -ErrorAction SilentlyContinue
}

foreach ($name in @('ModernDock.exe', 'install.ps1', 'uninstall.ps1', 'restore_windows_taskbar.ps1')) {
    $path = Join-Path $installRoot $name
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}

if ($RemoveUserData) {
    foreach ($name in @('dock_config.txt', 'dock_metadata.json')) {
        $path = Join-Path $installRoot $name
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    $iconDirectory = Join-Path $installRoot 'Icons'
    if (Test-Path -LiteralPath $iconDirectory) { Remove-Item -LiteralPath $iconDirectory -Recurse -Force }
    Write-Output 'User configuration, metadata and Icons were removed by explicit -RemoveUserData.'
} else {
    Write-Output 'User configuration, metadata and Icons were preserved.'
}

if (Test-Path -LiteralPath $installRoot -PathType Container) {
    $remaining = @(Get-ChildItem -LiteralPath $installRoot -Force)
    if ($remaining.Count -eq 0) { Remove-Item -LiteralPath $installRoot -Force }
}

Write-Output 'ModernDock was uninstalled and native taskbar recovery was requested.'
