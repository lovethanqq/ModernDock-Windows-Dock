[CmdletBinding()]
param(
    [string]$OutputDirectory = '',
    [ValidatePattern('^v?\d+\.\d+\.\d+$')]
    [string]$Version = '0.1.1'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$releaseExe = Join-Path $repoRoot 'Build\Release\ModernDock.exe'
if (-not (Test-Path -LiteralPath $releaseExe -PathType Leaf)) {
    throw 'Build\Release\ModernDock.exe is missing. Build the Release configuration first.'
}

$normalizedVersion = $Version.Trim()
if (-not $normalizedVersion.StartsWith('v', [System.StringComparison]::OrdinalIgnoreCase)) {
    $normalizedVersion = 'v' + $normalizedVersion
}
$staging = Join-Path $OutputDirectory ("ModernDock-" + $normalizedVersion + '-staging')
$zipPath = Join-Path $OutputDirectory ("ModernDock-" + $normalizedVersion + '.zip')
if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
New-Item -ItemType Directory -Force -Path $staging | Out-Null

Copy-Item -LiteralPath $releaseExe -Destination (Join-Path $staging 'ModernDock.exe')
foreach ($name in @('install.ps1', 'uninstall.ps1', 'restore_windows_taskbar.ps1', 'LICENSE')) {
    $source = Join-Path $repoRoot $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Required release file is missing: $name" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $staging $name)
}

Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -CompressionLevel Optimal

Add-Type -AssemblyName System.IO.Compression.FileSystem
$allowed = @('ModernDock.exe', 'install.ps1', 'uninstall.ps1', 'restore_windows_taskbar.ps1', 'LICENSE')
$archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $actual = @($archive.Entries | ForEach-Object { $_.FullName } | Sort-Object)
    $expected = @($allowed | Sort-Object)
    if (($actual -join '|') -ne ($expected -join '|')) {
        throw ('Release ZIP contents are not exactly the public allow-list: ' + ($actual -join ', '))
    }
} finally {
    $archive.Dispose()
}

Remove-Item -LiteralPath $staging -Recurse -Force
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash
Write-Output "Package=$zipPath"
Write-Output "SHA256=$hash"
Write-Output "Files=$($allowed -join ',')"
