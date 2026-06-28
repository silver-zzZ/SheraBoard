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

发布包说明：

- `standalone`：体积较大，内置 .NET 运行时，普通用户优先下载。
- `framework-dependent`：体积较小，需要用户安装 .NET 8 Windows Desktop Runtime。
- `SHA256SUMS.txt`：发布包校验值。

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
