# Command reference

Run `jcli --help` or `jcli <group> <command> --help` for generated option details.

## Global options

`--url`, `--user`, `--token`, `--token-stdin`, `--token-prompt`, `--profile`, `--config`, `--token-mode`, `--cloud-id`, `--json`, `--quiet`, `--non-interactive`, `--read-only`, `--dry-run`, and `--timeout` are inherited by subcommands.

## Commands

| Group | Commands |
|---|---|
| `auth` | `check`, `doctor` |
| `site` | `list`, `check`, `discover` |
| `project` | `list`, `search`, `issue-types`, `fields`, `components`, `versions`, `priorities` |
| `user` | `find`, `assignable` |
| `issue` | `get`, `search`, `count`, `create`, `update`, `delete`, `assign`, `unassign`, `transitions`, `transition` |
| `comment` | `list`, `get`, `add`, `update`, `delete` |
| `worklog` | `list`, `get`, `add`, `update`, `delete` |
| `remote-link` | `list`, `get`, `create`, `update`, `delete` |
| `board` | `list`, `get`, `backlog` |
| `sprint` | `list`, `get`, `issues`, `move`, `move-to-backlog` |

`site discover` reports that broad accessible-site enumeration is unavailable for API-token Basic authentication. It does not claim OAuth accessible-resource behavior. `site list` lists configured profiles, and `site check` validates one site.

## Content and custom fields

Comments, descriptions, and worklog comments accept exactly one of `--text`, `--file`, or `--stdin`, plus `--format plain|markdown|adf`.

- Plain text becomes ADF paragraphs and hard breaks.
- ADF must be a JSON `doc` with `version: 1`.
- Markdown supports paragraphs, headings, emphasis, strong text, inline code, fenced code, HTTP(S) links, quotes, lists, rules, and line breaks. Unsupported extensions degrade to text with a warning; conversion is not lossless.

`issue create/update --fields-json` accepts an object inline or `@file`. It is merged with convenience options and preserves arbitrary supported custom-field JSON. Use `project fields` to discover create/edit field IDs and schemas.

## Pagination

List/search commands default to 50 items or the profile's bounded `pageSize`. `--page-size` accepts 1–100. Use `--all` explicitly to follow every endpoint-specific page. Enhanced JQL uses `nextPageToken`; Jira Platform and Software endpoints retain their documented pagination shapes.

`issue count` calls Jira's approximate count API and always labels the result approximate.

## Name resolution

Assignment accepts an exact `--account-id` or an unambiguous `--user-query`. Transitions accept an exact transition ID or an unambiguous case-insensitive name. Multiple matches exit with code 6; the CLI never chooses arbitrarily.
