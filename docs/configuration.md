# Configuration reference

JiraCli reads one user-owned configuration file. It never creates or rewrites the file or its parent directory.

## File location and discovery

The configuration path is resolved in this order:

1. `--config /path/to/config.json` for the current invocation.
2. The `JIRACLI_CONFIG` environment variable.
3. The automatically detected OS default.

| OS | Automatically detected default |
|---|---|
| Windows | `%APPDATA%\jiracli\config.json` |
| macOS | `~/Library/Application Support/jiracli/config.json` |
| Linux | `${XDG_CONFIG_HOME:-$HOME/.config}/jiracli/config.json` |

### File permissions

On macOS and Linux, restrict the configuration directory to its owner and make the file owner-readable and owner-writable only. This applies even when the JSON contains only a credential-store reference: Jira URLs, usernames, profile names, and lookup identifiers are still user-owned configuration data.

macOS Terminal (`zsh`):

```zsh
chmod 700 "$HOME/Library/Application Support/jiracli"
chmod 600 "$HOME/Library/Application Support/jiracli/config.json"
ls -l "$HOME/Library/Application Support/jiracli/config.json"
```

Linux terminal (`bash`):

```bash
chmod 700 "${XDG_CONFIG_HOME:-$HOME/.config}/jiracli"
chmod 600 "${XDG_CONFIG_HOME:-$HOME/.config}/jiracli/config.json"
ls -l "${XDG_CONFIG_HOME:-$HOME/.config}/jiracli/config.json"
```

The expected file mode begins with `-rw-------`. JiraCli emits a warning if the file is readable or writable by the group or other users. It does not change permissions automatically.

Windows uses access-control lists instead of Unix mode bits. A configuration under `%APPDATA%` normally inherits the current user's profile permissions, and JiraCli does not apply the macOS/Linux mode check on Windows.

Use an explicit path or environment override when the file is stored elsewhere:

Windows PowerShell:

```powershell
jcli site list --config "$env:APPDATA\jiracli\config.json" --json
$env:JIRACLI_CONFIG = "$env:APPDATA\jiracli\config.json"
```

macOS Terminal (`zsh`):

```zsh
jcli site list --config "$HOME/Library/Application Support/jiracli/config.json" --json
export JIRACLI_CONFIG="$HOME/Library/Application Support/jiracli/config.json"
```

Linux terminal (`bash`):

```bash
jcli site list --config "${XDG_CONFIG_HOME:-$HOME/.config}/jiracli/config.json" --json
export JIRACLI_CONFIG="${XDG_CONFIG_HOME:-$HOME/.config}/jiracli/config.json"
```

`jcli site list --json` reports the absolute loaded path in its `config` field. It reports `null` when the default file is absent. Supplying a nonexistent file through `--config` or `JIRACLI_CONFIG` is an error instead of silently falling back.

## Schema

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

## Plaintext token compatibility

An OS credential store is recommended. If that is not available and the plaintext risk is explicitly accepted, JiraCli can read a token stored in a user-owned configuration document:

```json
{
  "schemaVersion": 1,
  "defaultProfile": "work",
  "profiles": {
    "work": {
      "url": "https://example.atlassian.net",
      "user": "user@example.com",
      "tokenMode": "unscoped",
      "token": "<JIRA_API_TOKEN>",
      "defaults": {
        "project": "DEMO",
        "pageSize": 50
      }
    }
  }
}
```

Replace the placeholder only in the local file. Never commit, share, or place the file in a synchronized folder. On macOS and Linux, apply the directory and file permissions described above. JiraCli reads this compatibility field and emits a plaintext-source warning, but it never creates or updates the field.

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
