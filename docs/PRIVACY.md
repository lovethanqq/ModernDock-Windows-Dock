# Privacy

[简体中文](PRIVACY.zh-CN.md) | English

ModernDock is a local-first desktop utility. Core functionality requires no ModernDock account, cloud backend, analytics, advertising or telemetry service.

To group and control desktop applications, ModernDock may inspect local process/window metadata such as executable paths, process names, window titles, window classes and window handles. This data is processed locally for matching, display and control. It is not sent to a ModernDock server by the open-source runtime.

The user configuration and metadata are stored under the per-user ModernDock installation data directory. User-selected PNG/ICO files remain local. The public repository intentionally contains no maintainer configuration, private icon directory, raw logs or machine-specific runtime evidence.

ModernDock can hide the native Windows taskbar while running. Normal shutdown restores it; the public distribution includes a recovery script for cases where a forced termination prevents cleanup.
