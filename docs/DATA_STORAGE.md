# Data Storage

SheraBoard stores user data per Windows user and per computer. Paths are not hard-coded to the maintainer's machine.

## Default Location

On a new computer, SheraBoard uses:

```text
%LOCALAPPDATA%\SheraBoard
```

This expands differently for every Windows user, for example:

```text
C:\Users\<UserName>\AppData\Local\SheraBoard
```

## Files

The data root contains:

- `SheraBoard.sqlite3`: history metadata, preview text, source app, timestamps, flags, and payload references.
- `settings.json`: app settings.
- `payloads\`: encrypted clipboard payload files.
- `file-snapshots\`: saved file-list snapshots.

Payload files are protected with Windows DPAPI for the current Windows user. Preview text and metadata in SQLite are used for search and display.

## Custom Location

Users can choose another storage location in Settings. SheraBoard saves that choice in:

```text
%LOCALAPPDATA%\SheraBoard\storage-location.txt
```

That file is local to the current Windows user. It is not committed to Git and does not affect other users.

## Startup Path

The "Start with Windows" setting writes the current `SheraBoard.exe` path to the current user's Windows Run key:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

The path is resolved from the executable that is actually running. If a user moves SheraBoard to another folder, they should run it once from the new location while startup is enabled so the Run entry is refreshed.

## Source Repository

The GitHub source repository intentionally excludes:

- `data/`
- `payloads/`
- `file-snapshots/`
- `*.sqlite3`
- `*.payload`
- `SheraBoard.exe`
- release zip files
- build output

Release binaries belong on GitHub Releases, not in Git history.
