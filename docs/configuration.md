# Configuration reference

JiraCli reads an explicit `--config` file or the platform application-data path under `jiracli/config.json`. It never creates or rewrites a configuration file.

```json
{
  "schemaVersion": 1,
  "defaultProfile": "work",
  "profiles": {
    "work": {
      "url": "https://example.atlassian.net",
      "user": "user@example.com",
      "tokenMode": "unscoped",
      "credential": {
        "provider": "windows-credential-manager",
        "target": "jiracli/work"
      },
      "defaults": {
        "project": "DEMO",
        "pageSize": 50
      }
    },
    "scoped": {
      "url": "https://example.atlassian.net",
      "user": "user@example.com",
      "tokenMode": "scoped",
      "cloudId": "00000000-0000-0000-0000-000000000000",
      "credential": {
        "provider": "linux-secret-service",
        "service": "jiracli",
        "account": "scoped"
      }
    }
  }
}
```

`url` must be HTTPS on a `*.atlassian.net` host. Scoped tokens require `cloudId` and use `https://api.atlassian.com/ex/jira/{cloudId}`. Token mode is explicit; token length or prefix is never inspected.

## Precedence

Non-secret settings resolve independently:

1. Command-line flag.
2. `JIRACLI_*` environment variable.
3. Selected profile.
4. Built-in default.

Profile selection is `--profile`, then `JIRACLI_PROFILE`, then `defaultProfile`.

Secret selection is deterministic:

1. Exactly one of `--token`, `--token-stdin`, or `--token-prompt`.
2. `JIRACLI_TOKEN`.
3. Existing `token` or `tokenFile` in the selected user-owned profile.
4. The selected profile's explicit `credential` reference.
5. Failure.

Conflicting explicit sources fail. A failed selected source never falls back to another identity.

The CLI never writes `token` or `tokenFile`. They are read-only compatibility options for files the user already owns. Plaintext sources produce a warning; on Unix, group/other-readable token files are rejected.

## Environment variables

| Variable | Meaning |
|---|---|
| `JIRACLI_CONFIG` | Configuration path |
| `JIRACLI_PROFILE` | Profile name |
| `JIRACLI_URL` | Jira site URL |
| `JIRACLI_USER` | Atlassian account email |
| `JIRACLI_TOKEN` | API token override |
| `JIRACLI_TOKEN_MODE` | `unscoped` or `scoped` |
| `JIRACLI_CLOUD_ID` | Cloud ID for scoped tokens |
| `JIRACLI_READ_ONLY` | `true` centrally blocks mutations |

Environment variables can be exposed through process inspection, crash reporting, or inherited child environments and are not equivalent to an OS credential store.
