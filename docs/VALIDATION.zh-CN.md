# 验证说明

简体中文 | [English](VALIDATION.md)

公共版验证以可重复和不打扰用户为原则。

## BackgroundSafe 检查

CI 和本地公开 probes 可以检查：

- .NET Framework 目标版本和是否存在机器专属 framework override
- 通用默认配置和七列格式兼容性
- 应用身份和路径感知匹配契约
- 受控的启动路径修复和快捷方式 metadata 契约
- 配置原子写入和任务栏恢复脚本语法
- Debug/Release 构建结果和发布 ZIP 文件清单

以下统计必须保持为零：

```text
Mouse movement = 0
Coordinate click = 0
User application focus steal = 0
```

## 人工边界

真实启动、关闭、最小化、右键菜单和焦点相关行为可能影响用户正在使用的程序，因此不会自动对用户桌面执行。应标记为 `MANUAL_INTERACTION_REQUIRED`，只在独立的受控环境中验证。

## 维护者参考数据

一台 Windows 11 维护者机器完成过五分钟 BackgroundSafe soak，共 31 个样本；期间只有一个 ModernDock 实例，Dock 始终可见且 Topmost，新异常为 0，refresh 中位耗时约 2.75ms，Working Set 约 95MB。这只是单台机器的证据，不是兼容性或性能保证。
