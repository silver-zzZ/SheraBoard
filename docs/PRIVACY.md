# 隐私与本地数据说明

SheraBoard 是本地剪贴板历史工具。当前实现不包含联网同步、账号登录或遥测上传。

## 保存哪些数据

SheraBoard 会在本机保存：

- 剪贴板正文 payload：纯文本、富文本、图片、文件列表等。
- 搜索和展示所需元数据：预览文本、来源应用、捕获时间、格式列表、大小、收藏/置顶状态等。
- 应用设置：快捷键、开机启动、忽略进程、存储上限等。

默认数据目录：

```text
%LOCALAPPDATA%\SheraBoard
```

用户也可以在设置页修改存储位置。
更详细的路径规则见 [DATA_STORAGE.md](DATA_STORAGE.md)。

## 加密边界

剪贴板正文、图片等 payload 文件使用 Windows DPAPI 按当前 Windows 用户加密保存。通常只有同一台机器上的同一 Windows 用户可以解密。

SQLite 数据库中的预览文本和元数据用于列表展示与搜索，不应视为完整加密存储。如果你复制过敏感内容，请把 SheraBoard 的数据目录也视为敏感数据。

## 不会提交到仓库的内容

开源仓库通过 `.gitignore` 排除了：

- 本地数据库。
- payload 文件。
- 本机设置文件。
- 发布 exe 和 zip。
- 备份、临时目录和构建产物。

发布或提交代码前，请确认没有把 `data/`、`payloads/`、`*.sqlite3`、`*.payload`、`SheraBoard.exe` 等文件加入 Git。

## 删除本地数据

1. 退出 SheraBoard。
2. 删除数据目录。
3. 如使用自定义存储目录，也删除对应目录。

删除后历史记录无法从 SheraBoard 内恢复。
