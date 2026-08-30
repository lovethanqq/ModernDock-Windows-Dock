# Troubleshooting

[简体中文](TROUBLESHOOTING.zh-CN.md) | English

## The native taskbar is hidden

Run the recovery script from the release folder:

```powershell
.\restore_windows_taskbar.ps1
```

If Explorer itself is unresponsive, restart Windows Explorer from Task Manager after saving your work.

## ModernDock does not start

- Check that only one ModernDock instance is running.
- Confirm the release was extracted completely and `ModernDock.exe` is beside the scripts.
- Check `%LOCALAPPDATA%\Programs\ModernDock\dock_fatal.log` for a local error description.
- Verify that .NET Framework 4.8 is available on the machine.

## A pinned app no longer launches

ModernDock first tries the saved target, then performs bounded recovery using a running process path, shortcut source metadata, nearby versioned directories, the configured directory, process name and App Paths. If candidates are ambiguous it should ask you to choose rather than silently launch an arbitrary file. Re-add the `.exe` or `.lnk` if the original installation was removed.

## Icons look wrong

Drop the correct `.exe` or `.lnk` again, or use the item menu to choose a PNG/ICO. User-selected custom icons are preferred over automatic extraction and are not overwritten by automatic icon refresh.

## A program appears more than once

Check whether the windows truly belong to different normalized executable paths or application identities. If they should be one group, include the Windows version, process paths, window classes and a sanitized reproduction in an issue; do not attach private logs or configuration files.

## Reporting a problem

Use the issue templates and include the release version, Windows build, DPI scaling, reproduction steps and whether the behavior is background-safe or requires manual foreground interaction.
