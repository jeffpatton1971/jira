# Changelog

## Unreleased

- Added the initial .NET 10 cross-platform Jira Cloud CLI.
- Added .NET tool packaging and self-contained publishing support.
- Added issue, project, user, metadata, comment, worklog, transition, remote-link, board, sprint, and backlog operations.
- Added unscoped and scoped API-token Basic authentication.
- Added read-only Windows Credential Manager, macOS Keychain, and Linux Secret Service adapters.
- Allowed native macOS Keychain authorization for interactive commands while keeping `--non-interactive` lookups prompt-free and time-bounded.
- Kept `jcli --version` concise by omitting source-revision build metadata.
- Added centralized read-only mode, previews, confirmations, redaction, destination validation, bounded responses, safe-read retries, and uncertain-write handling.
- Added stable JSON output, documented exit codes, Windows/macOS/Linux CI, and synthetic tests.
