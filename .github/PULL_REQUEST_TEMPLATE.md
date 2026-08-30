## Summary / 摘要

<!-- What problem does this change solve? / 这次改动解决什么问题？ -->

## Change scope / 改动范围

- [ ] Application identity or matching / 应用身份或匹配
- [ ] Persistence or taskbar lifecycle / 持久化或任务栏生命周期
- [ ] Icon rendering / 图标渲染
- [ ] UI or accessibility / UI 或无障碍
- [ ] Documentation / 文档

## Validation / 验证

- Windows version/build and DPI:
- Commands/tests run:
- BackgroundSafe: Mouse movement = 0; Coordinate click = 0; User application focus steal = 0
- Manual interaction required, if any:

## Safety checklist / 安全检查

- [ ] No personal absolute paths, usernames, emails, PIDs or private launch commands.
- [ ] No user `dock_config.txt`, `dock_metadata.json`, `Icons`, raw logs or private screenshots.
- [ ] No third-party application logos or proprietary assets without a compatible license.
- [ ] Configuration writes remain atomic and user data is preserved by default.
- [ ] Debug/Release build or the reason it cannot run is recorded.
