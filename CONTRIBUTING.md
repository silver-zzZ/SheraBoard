# Contributing

感谢你愿意改进 SheraBoard。这个项目目前优先保证 Windows 本地剪贴板体验稳定、可验证、隐私边界清楚。

## 开发环境

- Windows 10/11
- Visual Studio 2022
- Visual Studio 安装“.NET 桌面开发”工作负载
- .NET 8 SDK

## 本地检查

提交前请运行：

```powershell
dotnet restore SheraBoard.sln
dotnet build SheraBoard.sln
dotnet test SheraBoard.sln
```

## Pull Request 建议

- 保持改动聚焦，避免把功能、重构、格式化混在一个 PR。
- 涉及剪贴板存储、加密、恢复行为时，请补充或更新测试。
- 不要提交本地数据库、payload、发布 exe、zip、截图缓存或 IDE 私有配置。
- 如果改变用户可见行为，请同步更新 `README.md` 或 `docs/`。

## Issue 建议

请尽量说明：

- Windows 版本。
- SheraBoard 版本或提交号。
- 复现步骤。
- 预期行为和实际行为。
- 是否涉及特定应用、文件类型或剪贴板格式。
