# 参与 ModernDock 贡献

简体中文 | [English](CONTRIBUTING.md)

ModernDock 应保持为专注、轻量的 Windows Dock，不要变成第二套任务栏或完整桌面 Shell。

## 欢迎的贡献方向

- 应用身份和多窗口聚合
- 路径感知匹配和受控的启动路径恢复
- Windows 版本、DPI 和无障碍兼容性
- 任务栏生命周期和单实例安全
- 图标提取与视觉渲染质量
- 针对 250ms reconciliation 路径的实测性能改进
- 文档和可重复的公开安全测试

## 提交 Pull Request 前

- 不要提交个人绝对路径、用户名、邮箱、PID 或机器专属启动命令。
- 不要提交用户的 `dock_config.txt`、`dock_metadata.json`、`Icons` 目录、原始日志或私人截图。
- 除非许可证明确允许，否则不要重新分发第三方应用 Logo。
- 保持七列配置格式兼容，并保持原子写入。
- 保持正常退出、窗口关闭和会话结束路径上的任务栏恢复。
- 优先使用受控的 BackgroundSafe 测试。不要为了满足测试而移动用户鼠标、坐标点击或抢其他应用焦点。
- 说明问题、行为变化、测试方法、Windows 版本/DPI，以及是否触碰了用户状态。

## 开发说明

公共项目使用传统 Visual Studio `.csproj`、.NET Framework 4.8、WPF 和 Win32 互操作。依赖应保持精简；除非有实测数据，否则不要给 250ms refresh 路径增加额外工作。
