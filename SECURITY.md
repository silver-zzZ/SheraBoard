# 安全策略

## 支持版本

安全修复优先处理最新发布版本。

## 报告安全问题

请不要把敏感安全问题直接发到公开 issue。可使用 GitHub Security Advisory，或通过私密方式联系维护者。

报告时请尽量包含：

- SheraBoard 版本或提交号。
- Windows 版本。
- 清晰的复现步骤。
- 影响范围和可能涉及的数据。

SheraBoard 会处理本地剪贴板数据。涉及存储路径、payload 加密、开机自启、意外数据暴露的问题，都按安全敏感问题处理。

## 本地数据边界

SheraBoard 的剪贴板历史默认只保存在本机。payload 文件使用 Windows DPAPI 按当前 Windows 用户保护，但用于搜索和展示的预览文本、来源应用、时间等元数据会保存在 SQLite 数据库中。

请把 SheraBoard 的数据目录视为敏感目录。
