# SheraBoard

SheraBoard 是一个 Windows 本地剪贴板历史工具，使用 .NET 8 + WPF 构建。它面向日常复制、检索和恢复场景，数据默认保存在当前用户本机。

## 功能

- 记录纯文本、富文本、图片、文件/文件夹复制列表。
- 支持主历史窗口、`Ctrl+Alt+V` 快速面板和托盘菜单。
- 支持搜索、来源应用筛选、收藏、置顶和本地保留上限。
- 支持从历史记录一键恢复到系统剪贴板。
- 支持开机启动、忽略指定进程、关闭窗口后复制等设置。
- 剪贴板正文、图片等 payload 使用 Windows DPAPI 按当前用户加密保存。

## 下载

正式版本会发布到 GitHub Releases。

- `SheraBoard-*-win-x64-standalone.zip`：推荐普通用户下载，内置 .NET 运行时，解压后直接运行。
- `SheraBoard-*-win-x64-framework-dependent.zip`：体积更小，但需要先安装 [.NET 8 Windows Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)。

源码仓库不提交 exe、zip、数据库、剪贴板 payload 或其他本机产物。

## 使用

1. 解压发布包。
2. 运行 `SheraBoard.exe`。
3. 使用 `Ctrl+Alt+V` 打开快速面板，或从托盘图标打开主窗口。
4. 在设置页调整开机启动、保留空间、忽略进程和存储位置。

默认数据目录：

```text
%LOCALAPPDATA%\SheraBoard
```

更完整的使用说明见 [docs/USAGE.md](docs/USAGE.md)，本地数据与隐私说明见 [docs/PRIVACY.md](docs/PRIVACY.md)。

## 开发

需要 Windows 和 .NET 8 SDK。

```powershell
dotnet restore SheraBoard.sln
dotnet build SheraBoard.sln
dotnet test SheraBoard.sln
dotnet run --project src/SheraBoard.App/SheraBoard.App.csproj
```

## 发布

生成 GitHub Releases 使用的 zip 包：

```powershell
.\scripts\publish.ps1 -Version v0.1.0 -Mode all
```

输出目录：

```text
artifacts\release
```

脚本会生成 standalone、framework-dependent 两类 Windows x64 包，并写入 `SHA256SUMS.txt`。

第一次上传和 GitHub Release 流程见 [docs/RELEASING.md](docs/RELEASING.md)。

## 贡献

欢迎提交 issue 和 pull request。提交前请至少运行：

```powershell
dotnet test SheraBoard.sln
```

## 许可证

SheraBoard 使用 MIT License，见 [LICENSE](LICENSE)。
