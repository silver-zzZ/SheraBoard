# SheraBoard

SheraBoard 是一款 Windows 本地剪贴板历史工具，帮你保存、搜索和找回复制过的文字、图片和文件。数据默认只保存在本机。

## 功能

- 记录纯文本、富文本、图片、文件/文件夹复制列表。
- 支持 `Win+V` 打开 SheraBoard 主历史窗口，并提供托盘菜单。
- 支持搜索、来源应用筛选、收藏、置顶和本地保留上限。
- 支持从历史记录一键恢复到系统剪贴板。
- 支持开机启动、忽略指定进程、关闭窗口后复制等设置。
- 剪贴板正文、图片等 payload 使用 Windows DPAPI 按当前用户加密保存。

## 下载

正式版本在 [GitHub Releases](https://github.com/silver-zzZ/SheraBoard/releases/latest) 下载。

- 普通用户：不要下载源码压缩包，去 Releases 下载带 exe 的发布包。
- 开发者：需要源码时再 clone 仓库或下载 `Code` 页的源码包。
- `SheraBoard-*-win-x64-standalone.exe`：单文件便携版，内置 .NET 运行时，下载后直接运行。
- `SheraBoard-*-win-x64-standalone.zip`：推荐普通用户下载，内置 .NET 运行时，解压后直接运行。
- `SheraBoard-*-win-x64-framework-dependent.zip`：体积更小，但需要先安装 [.NET 8 Windows Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)。

源码仓库不提交 exe、zip、数据库、剪贴板 payload 或其他本机产物。

## 使用

1. 解压发布包。
2. 运行 `SheraBoard.exe`。
3. 使用 `Win+V` 打开 SheraBoard 主窗口，或从托盘图标打开主窗口。
4. 在设置页调整开机启动、保留空间、忽略进程和存储位置。

注意：SheraBoard 运行时会接管 `Win+V`，因此 Windows 原生剪贴板历史面板不会响应这个快捷键。设置页里的备用快捷键默认是 `Ctrl+Alt+V`，可作为额外打开方式。

默认数据目录：

```text
%LOCALAPPDATA%\SheraBoard
```

更完整的使用说明见 [docs/USAGE.md](docs/USAGE.md)，本地数据与隐私说明见 [docs/PRIVACY.md](docs/PRIVACY.md)。
数据路径、自启路径和每台电脑的本地存储规则见 [docs/DATA_STORAGE.md](docs/DATA_STORAGE.md)。

## 开发

开发环境要求：

- Windows 10/11
- Visual Studio 2022
- Visual Studio 安装“.NET 桌面开发”工作负载
- .NET 8 SDK

技术栈：.NET 8 + WPF。

```powershell
dotnet restore SheraBoard.sln
dotnet build SheraBoard.sln
dotnet test SheraBoard.sln
dotnet run --project src/SheraBoard.App/SheraBoard.App.csproj
```

## 贡献

欢迎提交 issue 和 pull request。提交前请至少运行：

```powershell
dotnet test SheraBoard.sln
```

维护者发布流程见 [docs/RELEASING.md](docs/RELEASING.md)。

## 许可证

SheraBoard 使用 MIT License，见 [LICENSE](LICENSE)。
