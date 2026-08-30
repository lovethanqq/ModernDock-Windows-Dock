# Validation

[简体中文](VALIDATION.zh-CN.md) | English

The public validation contract favors reproducibility and user safety.

## Background-safe checks

CI and local public probes may check:

- .NET Framework target and absence of machine-specific framework overrides
- generic default configuration and seven-column compatibility
- application identity and path-aware matching contracts
- bounded launch recovery and shortcut metadata contracts
- atomic config behavior and taskbar recovery script syntax
- Debug/Release build output and release ZIP file list

These checks must keep the following counters at zero:

```text
Mouse movement = 0
Coordinate click = 0
User application focus steal = 0
```

## Manual boundaries

Real launching, closing, minimizing, context menus and focus-sensitive behavior can affect user applications. They are not run automatically against a user's desktop. Mark them `MANUAL_INTERACTION_REQUIRED` and test them only in a separately controlled environment.

## Maintainer reference observation

One Windows 11 maintainer machine observed a five-minute BackgroundSafe soak with 31 samples, one ModernDock instance throughout, Dock visible and Topmost throughout, zero new exceptions, refresh median about 2.75 ms and working set about 95 MB. This is evidence from one machine, not a compatibility or performance guarantee.
