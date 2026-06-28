# 发布与上传手册

## 第一次上传到 GitHub

在项目根目录执行：

```powershell
git init -b main
git add .
git commit -m "Prepare open source release"
git remote add origin https://github.com/<your-name>/SheraBoard.git
git push -u origin main
```

如果本机 Git 版本不支持 `git init -b main`：

```powershell
git init
git branch -M main
```

上传前检查：

```powershell
git status --short
```

确认没有这些文件：

- `data/`
- `payloads/`
- `*.sqlite3`
- `*.payload`
- `SheraBoard.exe`
- `*.zip`
- `bin/`
- `obj/`
- `backups/`
- `tmp/`

## 本地生成发布包

```powershell
.\scripts\publish.ps1 -Version v0.1.0 -Mode all
```

输出目录：

```text
artifacts\release
```

同时会生成一个本地运行用文件：

```text
SheraBoard.exe
```

这个根目录 exe 方便本机直接运行，已被 `.gitignore` 排除，不进入 Git 提交。

发布包说明：

- `standalone.exe`：体积较大，内置 .NET 运行时，普通用户可直接下载运行。
- `standalone.zip`：同样内置 .NET 运行时，适合希望先解压再运行的用户。
- `framework-dependent.zip`：体积较小，需要用户安装 .NET 8 Windows Desktop Runtime。
- `SHA256SUMS.txt`：发布包校验值。

普通用户下载 `standalone.exe` 后可直接运行；下载 `standalone.zip` 后，解压即可看到并运行 `SheraBoard.exe`。exe 是发布资产，不提交到 Git 源码历史里。

开源桌面项目通常这样安排：

- GitHub `Code` 页：源码、文档、测试、构建脚本。
- GitHub `Releases` 页：给普通用户下载的 exe、zip、安装包、校验文件。
- GitHub Actions：每次打版本 tag 时自动测试、打包并上传 Release assets。

## GitHub Release

推荐用 tag 触发自动发布：

```powershell
git tag v0.1.0
git push origin v0.1.0
```

`.github/workflows/release.yml` 会在 Windows runner 上运行测试、生成发布包，并创建或更新 GitHub Release。

也可以在 GitHub Actions 页面手动运行 `Release` workflow，输入版本号，例如：

```text
v0.1.0
```

## 版本号建议

使用语义化版本：

- `v0.1.0`：首个公开版本。
- `v0.1.1`：修 bug。
- `v0.2.0`：增加功能。
- `v1.0.0`：认为核心体验稳定后再发布。
