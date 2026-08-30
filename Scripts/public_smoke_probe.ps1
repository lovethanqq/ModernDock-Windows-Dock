[CmdletBinding()]
param(
    [string]$ProjectRoot = ''
)

# InteractionMode = BackgroundSafe
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
} else {
    $ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
}

$failures = New-Object System.Collections.Generic.List[string]
function Require([bool]$condition, [string]$message) {
    if (-not $condition) { [void]$script:failures.Add($message) }
}

$projectFile = Join-Path $ProjectRoot 'ModernDock.csproj'
$projectText = Get-Content -Raw -LiteralPath $projectFile
Require ($projectText -match '<TargetFrameworkVersion>v4\.8</TargetFrameworkVersion>') 'TargetFrameworkVersion must be v4.8.'
Require ($projectText -notmatch 'FrameworkPathOverride') 'FrameworkPathOverride must not be present.'
Require ($projectText -notmatch 'RefactorScaffold|LegacyFrameworkPath|LegacyWpfReferencePath') 'Private scaffold or legacy machine reference remains in the project.'

$configFile = Join-Path $ProjectRoot 'Config\dock_config.txt'
$rows = @(Get-Content -LiteralPath $configFile | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
Require ($rows.Count -eq 3) 'Public seed configuration must contain exactly three generic rows.'
$titles = @('File Explorer', 'Recycle Bin', 'Settings')
for ($index = 0; $index -lt $rows.Count; $index++) {
    $parts = $rows[$index].Split([char]9)
    Require ($parts.Count -eq 7) "Configuration row $($index + 1) must contain seven columns."
    if ($parts.Count -ge 1) { Require ($parts[0] -eq $titles[$index]) "Unexpected public default title at row $($index + 1)." }
}

$forbiddenNames = @('Bin', 'Icons', 'TestResults', 'RefactorScaffold', 'FINAL_ACCEPTANCE_REPORT.md', 'FINAL_RUNTIME_REPORT.md', 'REFACTOR_STAGE1_REPORT.md', 'Scripts\wallpaper_rotator.py')
foreach ($name in $forbiddenNames) {
    $exists = Test-Path -LiteralPath (Join-Path $ProjectRoot $name)
    Require (-not $exists) ("Private artifact remains: $name")
}

$sourceFiles = @(Get-ChildItem -LiteralPath $ProjectRoot -File -Recurse | Where-Object {
    $_.FullName -notmatch '[\\/]Build[\\/]|[\\/]\.vs[\\/]|[\\/]artifacts[\\/]' -and
    $_.Name -ne 'public_smoke_probe.ps1'
})
$privatePattern = 'C:\\Users\\[^\\\r\n]*|D:\\Apps\\|E:\\Program Files|launch-singapore|pythoncore-|wallpaper_rotator|OpenAI\.Codex_2p2nqsd0c76g0'
foreach ($file in $sourceFiles) {
    if ($file.Extension -in @('.cs', '.csproj', '.txt', '.ps1', '.md', '.yml', '.yaml')) {
        $content = Get-Content -Raw -LiteralPath $file.FullName
        Require ($content -notmatch $privatePattern) "Private path or maintainer-specific launch text remains: $($file.FullName.Substring($ProjectRoot.Length + 1))"
    }
}

$codeFiles = @(Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'Source') -File -Recurse -Filter '*.cs')
$codeText = (($codeFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n")
$maintainerNamePattern = '(?i)\b(?:Antigravity|Helium|Cromite)\b|\bIsAntigravity\w*\b|\bIsBrowserItem\b'
$titleArgumentPattern = '(?i)(?:Title|item\.Title).*(?:Helium|Chrome|Cromite)|(?:Helium|Chrome|Cromite).*(?:Title|item\.Title)'
Require ($codeText -notmatch $maintainerNamePattern) 'Maintainer-specific application names or special-case helpers remain in public C# source.'
Require ($codeText -notmatch $titleArgumentPattern) 'Public source still couples custom titles to browser launch arguments.'
Require ($codeText -notmatch '(?i)item\.Arguments\s*=.*--start-maximized') 'Public source still injects --start-maximized into configured arguments.'
Require ($codeText -match '(?i)GetWindowPlacement') 'Generic window restore state must use GetWindowPlacement.'
Require ($codeText -match '(?i)WINDOWPLACEMENT') 'Generic window restore state must define WINDOWPLACEMENT.'
Require ($codeText -match '(?i)IsLauncherHostPath') 'Launcher/host icon handling must use a generic path predicate.'

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    Write-Output 'RED [PublicSmokeProbe]'
    exit 1
}

Write-Output 'BackgroundSafe: Mouse movement = 0; Coordinate click = 0; User application focus steal = 0'
Write-Output 'GREEN [PublicSmokeProbe]'
