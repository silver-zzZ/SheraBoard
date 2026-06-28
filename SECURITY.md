# Security Policy

## Supported Versions

Security fixes are handled on the latest released version.

## Reporting a Vulnerability

Please do not publish sensitive reports as public issues. Open a GitHub security advisory if available, or contact the maintainer privately.

Include:

- SheraBoard version or commit.
- Windows version.
- Clear reproduction steps.
- Impact and affected data.

SheraBoard handles local clipboard data, so reports involving storage paths, payload encryption, startup behavior, or unintended data exposure are treated as security-sensitive.

## Local Data Boundary

SheraBoard stores clipboard history locally. Payload files are protected with Windows DPAPI for the current Windows user, but preview text and metadata are stored in SQLite for search and display. Treat the data directory as sensitive.
