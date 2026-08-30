# Security Policy

[简体中文](SECURITY.zh-CN.md) | English

## Supported versions

Security fixes target the latest public release and the default branch. This is an early public release, so please include the exact release or commit when reporting an issue.

## Reporting a vulnerability

Please do not open a public issue for an unpatched security vulnerability. Use the repository's GitHub private vulnerability reporting flow when it is available, or contact the maintainers through the security contact shown on the repository profile.

Include:

- affected release or commit
- Windows version and architecture
- clear reproduction steps that do not include secrets
- impact and any safe proof of concept
- suggested mitigation, if known

Remove personal paths, usernames, tokens, screenshots and private configuration from the report. Never publish a private key, password or access token.

## Scope

ModernDock is a local desktop utility. Relevant security areas include unsafe process launching, shortcut parsing, configuration tampering, privilege escalation, DLL/search-path issues and taskbar recovery. It does not provide a cloud service or collect telemetry.
