# Changelog

All notable changes to SheraBoard will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project uses semantic version tags such as `v0.1.0`.

## Unreleased

### Added

- Prepared the repository for public GitHub release.
- Added CI and release packaging workflows.
- Added usage and privacy documentation.

### Changed

- Polished public-facing Chinese copy and made development environment requirements explicit.

## v0.1.1

### Changed

- Corrected shortcut wording across the app and docs: `Win+V` is the primary SheraBoard shortcut and replaces the Windows clipboard history shortcut while SheraBoard is running.
- Clarified that `Ctrl+Alt+V` is a configurable backup shortcut.

## v0.1.0

### Added

- Windows 本地剪贴板历史工具，支持保存和找回复制过的文字、富文本、图片和文件列表。
- `Win+V` 主历史窗口、托盘菜单、设置页、收藏和置顶。
- 本地 SQLite 存储，剪贴板 payload 使用 Windows DPAPI 按当前用户保护。
