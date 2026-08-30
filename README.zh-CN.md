# ModernDock — 一个真正懂应用窗口的轻量 Windows Dock

简体中文 | [English](README.md)

ModernDock 是一个面向 Windows 11 的轻量 WPF/Win32 Dock。它专注于应用识别、窗口切换和少量实用的桌面控制，不试图重做整个 Windows 任务栏。

![ModernDock 截图](docs/assets/moderndock.png)

## ModernDock 的核心特点

- **一个应用，一个图标。** 多窗口、多进程辅助程序会按应用身份聚合。
- **路径感知匹配。** 即使多个程序都叫 `chrome.exe`，只要规范化后的安装路径不同，也可以保持独立识别。
- **固定应用路径自修复。** 软件升级导致 EXE 换到新目录后，ModernDock 只在受控的高置信度范围内寻找新目标；出现多个候选时会请用户处理，不会随便启动错误程序。
- **图标视觉尺寸归一化。** 渲染时分析 Alpha 的实际可见区域，统一肉眼大小，而不是简单地把所有图片放进 `32×32` 画布就宣称它们一样大。
- **拖入固定并拖动排序。** 可以把 `.exe` 或 `.lnk` 拖到 Dock，再拖动固定图标调整顺序。
- **多窗口列表。** 一个应用图标可以展开它当前匹配到的窗口，快速切换。
- **自定义图标。** 自动图标不合适时，可以选择 PNG 或 ICO。
- **时间和 Core Audio 音量浮层。** 提供紧凑的时间/日期和音量控制，不复刻完整系统托盘。
- **通用真全屏自动隐藏。** 检测到前台窗口真正覆盖屏幕时隐藏 Dock，退出全屏后自动恢复。
- **WPF + Win32。** 使用原生 WPF 和必要的 Win32 互操作，不使用 Electron 或 WebView 作为 UI 层。
- **更安全的本地状态。** 内置单实例保护、原子配置写入和任务栏恢复流程。

ModernDock 的定位是轻量 Windows Dock，不是第二套任务栏、通知中心或小组件平台。

## 运行和构建要求

- 主要验证目标是 Windows 11。
- 构建需要 .NET Framework 4.8 Developer Pack。
- 需要 Visual Studio 2022 或带 MSBuild 的 Visual Studio Build Tools。
- 当前主要验证的是主显示器；多显示器和少见的打包应用场景还需要社区测试。

## 安装

最新版本：[v0.1.1 — 公共清理版本](https://github.com/lovethanqq/ModernDock-Windows-Dock/releases/tag/v0.1.1)

1. 从 [Releases](https://github.com/lovethanqq/ModernDock-Windows-Dock/releases) 下载发布 ZIP。
2. 解压到临时目录。
3. 在该目录打开 PowerShell，运行：

   ```powershell
   .\install.ps1
   ```

默认按当前用户安装到 `%LOCALAPPDATA%\Programs\ModernDock`，不需要管理员权限。安装不会覆盖已有的用户配置和自定义图标目录。

卸载程序但保留用户数据：

```powershell
.\uninstall.ps1
```

只有在明确要删除配置、metadata 和自定义图标时才使用 `-RemoveUserData`。

## 第一次运行

公共版本只带通用 Windows 项目：文件资源管理器、回收站和设置，不包含维护者的私人软件列表、私人启动命令或第三方应用 Logo。

添加应用时，直接把 Explorer 中的 `.exe` 或 `.lnk` 拖到 Dock。ModernDock 会在本机提取图标，保存七列配置；需要自更新时，快捷方式来源等额外信息会放进小型 sidecar 文件。

## 安全和恢复

ModernDock 运行时会隐藏 Windows 原生任务栏，并在正常退出、窗口关闭和会话结束清理时恢复任务栏。如果强制结束进程导致清理没有执行，可以运行：

```powershell
.\restore_windows_taskbar.ps1
```

或者在任务管理器中重启 Windows Explorer。

恢复脚本只负责显示原生任务栏，不会删除 ModernDock 数据。

## 性能参考

在一台 Windows 11 维护者测试机器上，冻结版本完成了五分钟 BackgroundSafe soak：

| 指标 | 实测 |
| --- | ---: |
| Refresh 中位耗时 | 约 2.75 ms |
| Working Set | 约 95 MB |
| 新异常 | 0 |
| ModernDock 实例 | 1 |

这些只是单台机器的观察结果，不是性能承诺或兼容性保证。它们不代表 0% CPU、完美兼容所有 Windows 或绝对稳定。

## 隐私

ModernDock 以本地处理为主，不需要账号、遥测服务、广告、云后端或网络连接。为了完成应用分组和窗口控制，它可能读取本机进程/窗口元数据，例如可执行文件路径、进程名、窗口标题和窗口句柄；这些数据只在本地处理。

请先阅读[隐私说明](docs/PRIVACY.zh-CN.md)和[安全策略](SECURITY.zh-CN.md)，再分发构建版本或报告安全问题。

## 已知限制

- 不同 Windows 11 版本、DPI、多显示器布局和打包应用并未得到同等程度的验证。
- 少量 Windows 或应用图标的真实来源可能仍然低清；用户可以选择 PNG/ICO 覆盖。
- 如果强制结束进程，清理代码可能无法执行，任务栏不一定会自动恢复。
- 部分依赖焦点的启动、关闭和右键菜单场景不会用会抢焦点的自动化测试，需人工验证。
- 仓库和发布 ZIP 不包含维护者的私人配置、私人图标或 wallpaper 脚本。

## 构建和验证

- [Building](docs/BUILDING.md) / [构建说明](docs/BUILDING.zh-CN.md)
- [Validation](docs/VALIDATION.md) / [验证说明](docs/VALIDATION.zh-CN.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md) / [故障排查](docs/TROUBLESHOOTING.zh-CN.md)

仓库 CI 会在 Windows 上构建 Debug 和 Release，并运行公开安全的 probes；不会点击用户应用，也不会执行前台破坏性自动化。

## 参与贡献

欢迎提交 Issue 和 Pull Request。修改窗口身份、任务栏生命周期、持久化或图标管线前，请先阅读 [CONTRIBUTING.zh-CN.md](CONTRIBUTING.zh-CN.md)。

## 许可证和第三方资产

ModernDock 使用 MIT License 发布。第三方应用 Logo、商标、专有图标以及用户本机配置不属于本仓库或发布 ZIP 的开源内容。
