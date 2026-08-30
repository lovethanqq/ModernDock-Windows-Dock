# Building ModernDock

[简体中文](BUILDING.zh-CN.md) | English

## Requirements

- Windows 11 recommended
- Visual Studio 2022 or Visual Studio Build Tools
- .NET Framework 4.8 Developer Pack
- MSBuild available in the developer command prompt

The public project is a standard Visual Studio `.csproj` targeting .NET Framework 4.8. It uses WPF and focused Win32 interop, with no third-party NuGet dependency.

## Build

From the repository root in a Developer PowerShell:

```powershell
msbuild ModernDock.sln /t:Rebuild /p:Configuration=Debug /p:Platform="Any CPU"
msbuild ModernDock.sln /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU"
```

Outputs are written below `Build\Debug` and `Build\Release`, which are ignored by Git.

## Package

After a successful Release build:

```powershell
.\Scripts\package_release.ps1
```

The package script creates a ZIP containing only the public executable, installation/recovery scripts and MIT license. It never reads or packages a user's installed configuration or icon directory.

## Notes

Do not set a machine-specific `FrameworkPathOverride`. Do not copy `dock_config.txt`, `dock_metadata.json`, `Icons`, raw logs, private screenshots or local test results into a release.
