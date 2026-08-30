# ModernDock — A lightweight Windows Dock that understands your apps

[简体中文](README.zh-CN.md) | English

ModernDock is a lightweight WPF/Win32 dock for Windows 11. It focuses on application identity, window switching and a small set of useful desktop controls instead of recreating the whole Windows taskbar.

![ModernDock screenshot](docs/assets/moderndock.png)

## Why ModernDock

- **One app, one icon.** Multiple windows and helper processes are grouped under one application identity.
- **Path-aware matching.** Apps that share a process name such as `chrome.exe` can still remain separate when their normalized installation paths differ.
- **Self-healing pinned apps.** After an update moves an executable, ModernDock searches only bounded, high-confidence locations and asks for help when the result is ambiguous.
- **Optical icon normalization.** The renderer measures visible alpha content and normalizes optical size; it does not pretend that every `32×32` canvas has the same visual weight.
- **Drag to pin and reorder.** Drop an `.exe` or `.lnk` onto the Dock, then drag fixed items into the order you want.
- **Window lists.** One application icon can expose its currently matched windows for quick switching.
- **Custom icons.** Choose a PNG or ICO when the automatic source is not the right one.
- **Clock and Core Audio volume flyout.** Compact time/date and volume controls without reproducing the entire system tray.
- **Generic true-fullscreen auto-hide.** The Dock hides for a detected fullscreen foreground window and returns when fullscreen ends.
- **WPF + Win32.** The UI is native WPF with focused Win32 interop; it does not use Electron or a WebView UI layer.
- **Safer local state.** Single-instance protection, atomic configuration writes and taskbar recovery are part of the runtime design.

ModernDock is intended to stay a small Windows Dock, not become a second Windows Taskbar, notification center or widget platform.

## Requirements

- Windows 11 is the primary validated target.
- .NET Framework 4.8 Developer Pack for building.
- Visual Studio 2022 or Visual Studio Build Tools with MSBuild.
- Primary-monitor behavior is the currently validated setup; multi-monitor and unusual packaged-app cases need community testing.

## Install

1. Download the release ZIP from [Releases](https://github.com/lovethanqq/ModernDock-Windows-Dock/releases).
2. Extract it to a temporary folder.
3. Open PowerShell in that folder and run:

   ```powershell
   .\install.ps1
   ```

The default per-user install location is `%LOCALAPPDATA%\Programs\ModernDock`. Installation does not require administrator access. It does not replace an existing user configuration or custom icon directory.

To remove the program while keeping user data:

```powershell
.\uninstall.ps1
```

Use `-RemoveUserData` only when you explicitly want to remove the installed configuration, metadata and custom icons.

## First run

The public seed contains only generic Windows entries: File Explorer, Recycle Bin and Settings. No maintainer application list, private launcher command or bundled third-party application logo is included.

To add an app, drag its `.exe` or `.lnk` from Explorer onto the Dock. ModernDock extracts an icon locally, records the seven-column configuration, and keeps shortcut source metadata in a small sidecar when needed for recovery.

## Safety and recovery

ModernDock hides the native taskbar while it is running and restores it during normal shutdown, window close and session-ending cleanup. If a forced termination prevents cleanup, run:

```powershell
.\restore_windows_taskbar.ps1
```

or restart Windows Explorer from Task Manager.

The recovery script only shows the native taskbar. It does not delete ModernDock data.

## Performance evidence

On one Windows 11 maintainer test machine, the frozen build completed a five-minute BackgroundSafe soak with:

| Metric | Observed |
| --- | ---: |
| Refresh median | ~2.75 ms |
| Working set | ~95 MB |
| New exceptions | 0 |
| ModernDock instances | 1 |

These are observations from one machine, not performance promises or compatibility guarantees. They should not be read as claims of 0% CPU, universal Windows support or absolute stability.

## Privacy

ModernDock is local-first. Core functionality requires no account, telemetry service, advertising, cloud backend or network connection. To group and control windows it may inspect local process/window metadata such as executable paths, process names, window titles and window handles; this data is processed locally.

Read [Privacy](docs/PRIVACY.md) and [Security](SECURITY.md) before distributing a build or reporting a vulnerability.

## Known limitations

- Windows 11, DPI variants, multi-monitor layouts and packaged applications are not equally validated.
- A few Windows or application icon sources may still be low resolution; users can select a PNG/ICO override.
- A force-killed process can prevent cleanup code from restoring the taskbar.
- Some focus-sensitive launch, close and context-menu scenarios are intentionally left for manual testing rather than automated tests that steal focus.
- This project does not include the maintainer's private configuration, private icons or wallpaper scripts.

## Build and validation

- [Building](docs/BUILDING.md) / [构建说明](docs/BUILDING.zh-CN.md)
- [Validation](docs/VALIDATION.md) / [验证说明](docs/VALIDATION.zh-CN.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md) / [故障排查](docs/TROUBLESHOOTING.zh-CN.md)

The repository CI builds Debug and Release on Windows and runs public-safe probes. It does not click the user's applications or run foreground-destructive automation.

## Contributing

Issues and pull requests are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) before changing the window identity, taskbar lifecycle, persistence or icon pipeline.

## License and third-party assets

ModernDock is released under the MIT License. Third-party application logos, trademarks, proprietary icons and a user's local configuration are not part of this repository or its release ZIP.
