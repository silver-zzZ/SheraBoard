# Maintainer Release Guide

This document is for maintainers publishing a new SheraBoard release.

## Preflight

Before publishing, check the working tree:

```powershell
git status --short
```

These files and directories must not be tracked by Git:

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

Run tests:

```powershell
dotnet test SheraBoard.sln
```

## Build Release Assets

```powershell
.\scripts\publish.ps1 -Version v0.1.1 -Mode all
```

Output directory:

```text
artifacts\release
```

The script creates:

- `SheraBoard-<version>-win-x64-standalone.exe`
- `SheraBoard-<version>-win-x64-standalone.zip`
- `SheraBoard-<version>-win-x64-framework-dependent.zip`
- `SHA256SUMS.txt`

It also refreshes the local root `SheraBoard.exe` for maintainer testing. That file is ignored by Git and must not be committed.

## Publish

Publish by pushing a version tag:

```powershell
git tag -a v0.1.1 -m "SheraBoard v0.1.1"
git push origin main
git push origin v0.1.1
```

`.github/workflows/release.yml` runs tests, builds release assets on Windows, and creates or updates the GitHub Release.

The Release workflow can also be run manually from GitHub Actions with a version input, for example:

```text
v0.1.1
```

## Versioning

Use semantic version tags:

- `v0.1.1`: bug fix or documentation correction.
- `v0.2.0`: user-facing feature.
- `v1.0.0`: stable core experience.
