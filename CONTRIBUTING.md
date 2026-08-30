# Contributing to ModernDock

[简体中文](CONTRIBUTING.zh-CN.md) | English

ModernDock should remain a focused, lightweight Windows Dock rather than a second taskbar or desktop shell.

## Good contributions

- Application identity and multi-window grouping
- Path-aware matching and bounded launch recovery
- Windows-version, DPI and accessibility fixes
- Taskbar lifecycle and single-instance safety
- Icon extraction and optical rendering quality
- Focused performance work on the 250 ms reconciliation path
- Documentation and reproducible public-safe tests

## Before opening a pull request

- Do not commit personal absolute paths, usernames, emails, PIDs or machine-specific launch commands.
- Do not commit a user's `dock_config.txt`, `dock_metadata.json`, `Icons` directory, raw logs or private screenshots.
- Do not redistribute third-party application logos unless their license explicitly permits it.
- Preserve seven-column configuration compatibility and atomic writes.
- Preserve taskbar recovery on normal shutdown, window close and session-ending paths.
- Prefer bounded, background-safe tests. Do not move the user's mouse, click coordinates or steal another application's focus merely to satisfy a test.
- Explain the problem, behavior change, test method, Windows build/DPI context and whether user state was touched.

## Development notes

The public project uses the classic Visual Studio `.csproj` format, .NET Framework 4.8, WPF and Win32 interop. Keep dependencies small and avoid adding work to the 250 ms refresh path unless the change is measured.
