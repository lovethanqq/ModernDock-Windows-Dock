# 构建 ModernDock

简体中文 | [English](BUILDING.md)

## 环境要求

- 推荐 Windows 11
- Visual Studio 2022 或 Visual Studio Build Tools
- .NET Framework 4.8 Developer Pack
- 在 Developer PowerShell 中可用的 MSBuild

公共工程使用标准 Visual Studio `.csproj`，目标框架为 .NET Framework 4.8。UI 使用 WPF，系统能力使用必要的 Win32 互操作，不依赖第三方 NuGet 包。

## 构建

在 Developer PowerShell 中进入仓库根目录，运行：

```powershell
msbuild ModernDock.sln /t:Rebuild /p:Configuration=Debug /p:Platform="Any CPU"
msbuild ModernDock.sln /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU"
```

构建结果会写入 `Build\Debug` 和 `Build\Release`，这些目录已被 Git 忽略。

## 打包

Release 构建成功后运行：

```powershell
.\Scripts\package_release.ps1
```

打包脚本只会生成包含公共 EXE、安装/恢复脚本和 MIT License 的 ZIP，不会读取或打包用户安装目录中的配置和图标。

## 注意事项

不要设置机器专属的 `FrameworkPathOverride`。不要把 `dock_config.txt`、`dock_metadata.json`、`Icons`、原始日志、私人截图或本地测试结果复制进发布包。
