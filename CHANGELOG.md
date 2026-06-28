# Changelog

All notable changes to SheraBoard will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project uses semantic version tags such as `v0.1.0`.

## Unreleased

### Added

- Prepared the repository for public GitHub release.
- Added CI and release packaging workflows.
- Added usage and privacy documentation.

## v0.1.1

### Changed

- Corrected shortcut wording across the app and docs: `Win+V` is the primary SheraBoard shortcut and replaces the Windows clipboard history shortcut while SheraBoard is running.
- Clarified that `Ctrl+Alt+V` is a configurable backup shortcut.

## v0.1.0

### Added

- Windows clipboard history app built with .NET 8 and WPF.
- Local history for text, rich text, images, and file lists.
- `Win+V` main history window, tray menu, settings, favorites, and pinning.
- Local SQLite storage with DPAPI-protected payload files.
