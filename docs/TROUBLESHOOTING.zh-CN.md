# 故障排查

简体中文 | [English](TROUBLESHOOTING.md)

## Windows 原生任务栏被隐藏

在发布目录运行恢复脚本：

```powershell
.\restore_windows_taskbar.ps1
```

如果 Explorer 本身没有响应，请先保存工作，再通过任务管理器重启 Windows Explorer。

## ModernDock 无法启动

- 检查是否已经有一个 ModernDock 实例在运行。
- 确认发布 ZIP 已完整解压，`ModernDock.exe` 与脚本位于同一目录。
- 查看 `%LOCALAPPDATA%\Programs\ModernDock\dock_fatal.log` 中的本地错误信息。
- 确认系统提供 .NET Framework 4.8。

## 固定应用升级后无法启动

ModernDock 会先尝试保存的目标，然后按受控范围检查正在运行的进程路径、快捷方式来源、附近的版本目录、原配置目录、进程名和 App Paths。如果候选不唯一，它应当请求用户选择，而不是静默启动一个不确定的文件。如果原安装已被删除，请重新拖入 `.exe` 或 `.lnk`。

## 图标显示不对

重新把正确的 `.exe` 或 `.lnk` 拖入，或者在项目菜单中选择 PNG/ICO。用户选择的自定义图标优先级最高，自动刷新不会覆盖它。

## 一个程序出现多个图标

先确认这些窗口是否真的属于不同的规范化可执行路径或不同应用身份。如果本应合并，请在 Issue 中提供 Windows 版本、进程路径、窗口类名和脱敏后的复现步骤；不要上传私人日志或配置文件。

## 如何报告问题

使用 Issue 模板，并注明发布版本、Windows 构建号、DPI 缩放、复现步骤，以及该行为是否可以 BackgroundSafe 验证，还是必须人工前台操作。
