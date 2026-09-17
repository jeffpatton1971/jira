# JiraCli

`jcli` is a standalone, safety-focused Jira Cloud CLI for humans and automation. It targets .NET 10 LTS and runs on Windows, macOS, and Linux.

It is not a BAT component, a Jira Data Center client, a Jira administrator, or an unrestricted HTTP client.

## Status

The implemented commands cover Jira Cloud projects, create/edit metadata, users, issues, enhanced JQL search and approximate counts, workflow transitions, comments, worklogs, remote links, boards, sprints, and board backlogs. See [the coverage matrix](docs/coverage.md) for endpoints, permissions, and tests.

Rovo's federated or natural-language search is not equivalent to JQL. This tool provides Jira JQL search and does not claim federated Rovo search parity.

## Workstation setup

Building and installing `jcli` from this repository requires Git and the **.NET 10 SDK**. The runtime alone is not enough to restore, test, pack, or publish the project. Installing the SDK also installs the corresponding runtime.

First identify the operating-system architecture:

| OS | Command | Architecture result |
|---|---|---|
| Windows PowerShell | `Get-CimInstance Win32_ComputerSystem \| Select-Object SystemType` | `x64-based PC` = `x64`; `ARM64-based PC` = `arm64` |
| macOS | `uname -m` | `x86_64` = `x64`; `arm64` = `arm64` |
| Linux | `uname -m` | `x86_64` = `x64`; `aarch64` or `arm64` = `arm64` |

Then obtain the .NET 10 SDK for that architecture:

### Windows

WinGet automatically selects the native architecture:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact --source winget
```

If WinGet is unavailable, select the matching `x64` or `Arm64` SDK installer from [Microsoft's Windows installation page](https://learn.microsoft.com/dotnet/core/install/windows).

### macOS

Download the .NET 10 SDK installer from [Microsoft's macOS installation page](https://learn.microsoft.com/dotnet/core/install/macos). Choose `Arm64` for Apple silicon (M-series) or `x64` for an Intel Mac.

### Linux

Use [Microsoft's distribution-specific Linux instructions](https://learn.microsoft.com/dotnet/core/install/linux). When the appropriate package feed is configured, the SDK package is normally named `dotnet-sdk-10.0`. For example:

```bash
sudo apt update
sudo apt install dotnet-sdk-10.0
```

Microsoft's .NET 10 Linux packages are available for `x64` and `arm64`; package availability and prerequisite setup vary by distribution.

Open a new terminal after installation and verify the SDK:

```text
dotnet --version
dotnet --info
dotnet --list-sdks
```

This repository requests SDK `10.0.400` with `latestFeature` roll-forward in `global.json`, so `dotnet --version` must resolve to `10.0.400` or a later .NET 10 SDK. If an older SDK is selected, install a current .NET 10 SDK before continuing.

## Build

```text
dotnet restore JiraCli.slnx
dotnet build JiraCli.slnx --configuration Release --no-restore
dotnet test JiraCli.slnx --configuration Release --no-build
```

## Install `jcli`

No package or executable release has been published yet. Each operating system currently installs `jcli` by cloning this repository, creating a local .NET tool package, and installing that package globally for the current user.

### Windows PowerShell

```powershell
git clone https://github.com/jeffpatton1971/jira.git
Set-Location jira
dotnet restore JiraCli.slnx
dotnet test JiraCli.slnx --configuration Release
dotnet pack src/JiraCli.Cli/JiraCli.Cli.csproj --configuration Release --output artifacts
dotnet tool install --global JiraCli.Tool --add-source (Resolve-Path artifacts)
jcli --version
jcli --help
```

The SDK installer normally makes `%USERPROFILE%\.dotnet\tools` available on `PATH`. If `jcli` is not found, add that directory to the user `PATH`, then open a new terminal.

### macOS Terminal (`zsh`)

Run these commands in Terminal using the default macOS shell, `zsh`:

```bash
git clone https://github.com/jeffpatton1971/jira.git
cd jira
dotnet restore JiraCli.slnx
dotnet test JiraCli.slnx --configuration Release
dotnet pack src/JiraCli.Cli/JiraCli.Cli.csproj --configuration Release --output artifacts
dotnet tool install --global JiraCli.Tool --add-source "$PWD/artifacts"
export PATH="$PATH:$HOME/.dotnet/tools"
jcli --version
jcli --help
```

Add the `export PATH=...` line to `~/.zprofile` to make it persistent, then open a new Terminal window.

### Linux terminal (`bash`)

```bash
git clone https://github.com/jeffpatton1971/jira.git
cd jira
dotnet restore JiraCli.slnx
dotnet test JiraCli.slnx --configuration Release
dotnet pack src/JiraCli.Cli/JiraCli.Cli.csproj --configuration Release --output artifacts
dotnet tool install --global JiraCli.Tool --add-source "$PWD/artifacts"
export PATH="$PATH:$HOME/.dotnet/tools"
jcli --version
jcli --help
```

Add the `PATH` export to `~/.profile`, `~/.bashrc`, or the appropriate shell startup file. Install `libsecret-1` and provision a Secret Service session only if the Linux Secret Service credential provider will be used; token input through standard input or an environment variable does not require it.

For a repository-pinned local tool instead of a user-wide installation:

```text
dotnet new tool-manifest
dotnet tool install JiraCli.Tool --add-source artifacts
dotnet tool run jcli --help
```

A global tool is convenient and user-wide. A local manifest pins a version per repository. Both require a compatible .NET runtime. Self-contained executables avoid that prerequisite but are larger and OS/architecture-specific:

```text
dotnet publish src/JiraCli.Cli/JiraCli.Cli.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
dotnet publish src/JiraCli.Cli/JiraCli.Cli.csproj -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
dotnet publish src/JiraCli.Cli/JiraCli.Cli.csproj -c Release -r osx-arm64 --self-contained true -o artifacts/osx-arm64
```

Trimming and Native AOT remain disabled until command parsing, JSON, and native credential adapters have dedicated compatibility coverage.

## Configuration file location

`jcli` looks for one configuration document. The path is resolved in this order:

1. The `--config` command-line option.
2. The `JIRACLI_CONFIG` environment variable.
3. The default path for the current OS.

| OS | Automatically detected default |
|---|---|
| Windows | `%APPDATA%\jiracli\config.json` |
| macOS | `~/Library/Application Support/jiracli/config.json` |
| Linux | `${XDG_CONFIG_HOME:-$HOME/.config}/jiracli/config.json` |

The CLI reads the file but does not create it or its parent directory. Create it yourself at the default location for automatic detection, or pass another location explicitly.

On macOS and Linux, protect the directory and file from other local users. `jcli` checks the file mode and warns when the configuration is group- or other-accessible:

```zsh
# macOS
chmod 700 "$HOME/Library/Application Support/jiracli"
chmod 600 "$HOME/Library/Application Support/jiracli/config.json"
```

```bash
# Linux
chmod 700 "${XDG_CONFIG_HOME:-$HOME/.config}/jiracli"
chmod 600 "${XDG_CONFIG_HOME:-$HOME/.config}/jiracli/config.json"
```

The expected file mode shown by `ls -l` begins with `-rw-------`. Windows protects the file through the user profile's Windows ACLs and does not use these Unix `chmod` modes.

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

`--config` applies to that invocation. An exported environment variable applies to commands launched from that shell. `jcli site list --json` reports the absolute loaded path in its `config` field, or `null` if no configuration file was found.

## Quick start

Use an OS credential store when possible. Configuration never needs to contain a token:

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
        "provider": "macos-keychain",
        "service": "jiracli",
        "account": "work"
      },
      "defaults": { "project": "DEMO", "pageSize": 50 }
    }
  }
}
```

The `credential` object is a reference to an existing OS credential-store item; its fields do not contain the token:

| OS | Provider | Required lookup fields |
|---|---|---|
| Windows | `windows-credential-manager` | `target`, matching a Generic Credential target |
| macOS | `macos-keychain` | `service` and `account`, matching a Generic Password's Name/service and Account |
| Linux | `linux-secret-service` | `service` and `account`, matching Secret Service attributes with those names |

The native item's password/secret contains the Jira API token. Its lookup fields must match the JSON exactly. The profile-level `user` is the Atlassian login email and is independent of a credential-store account label. See the [credential-provider setup guide](docs/credentials.md) for native provisioning steps and a property-by-property reference.

### Authorize macOS Keychain access on first use

Run the first Keychain-backed check **without** `--non-interactive`:

```zsh
jcli auth doctor --profile work --json
```

When macOS requests the login/keychain password, verify that the requesting program is the expected `jcli` installation, enter the macOS password, and click **Always Allow**. This authorizes later invocations to read that one Keychain item without prompting. **Allow Once** works only for the current invocation; subsequent `--non-interactive` commands will fail because they are not permitted to display another authorization prompt.

After selecting **Always Allow**, verify prompt-free access:

```zsh
jcli auth doctor --profile work --non-interactive --json
```

If you intentionally accept the risk of storing a plaintext token, `jcli` can read a `token` property from a user-owned configuration file:

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
      "defaults": { "project": "DEMO", "pageSize": 50 }
    }
  }
}
```

Replace the placeholder locally. Never commit, share, or place this file in a synchronized folder. On macOS and Linux, apply the directory and file permissions described above. The CLI reads this compatibility field but never writes it; an OS credential store is safer and remains recommended.

Alternatively, pass a token safely for one invocation:

```bash
printf '%s\n' "$TOKEN" | jcli auth check --url https://example.atlassian.net --user user@example.com --token-stdin
```

Command-line tokens and environment variables are supported but are not equivalent to an OS secret store.

Examples:

```text
jcli project search --query demo --all --profile work
jcli project issue-types DEMO --profile work
jcli project fields --project DEMO --issue-type 10001 --profile work
jcli issue get DEMO-123 --fields summary,status,customfield_10001 --expand names --json
jcli issue search --jql "project = DEMO ORDER BY created DESC" --all --json
jcli issue count --jql "project = DEMO AND statusCategory != Done" --json
jcli issue create --project DEMO --issue-type Bug --summary "Example" --file description.md --format markdown --dry-run
jcli issue update DEMO-123 --priority High --label triaged --fix-version "1.2" --dry-run
jcli issue transitions DEMO-123 --fields
jcli issue transition DEMO-123 Done --dry-run
jcli comment add DEMO-123 --stdin --format plain
jcli worklog add DEMO-123 --time-spent "30m" --text "Investigation"
jcli remote-link create DEMO-123 --remote-url https://example.com/item/1 --title "Related item"
jcli board backlog 42 --all --json
jcli sprint move --sprint 73 --issue DEMO-123 --issue DEMO-124 --dry-run
jcli sprint move-to-backlog --board 42 --issue DEMO-123 --dry-run
```

A board backlog is placement in a particular board/sprint system. A workflow status named “Backlog” is an organization-defined status reached through a transition. The commands do not treat those concepts as interchangeable.

## Safety

- `--read-only` centrally blocks every mutation, including mutation previews.
- `--dry-run` produces a local request preview labeled `serverValidated: false`; it is not server-side validation.
- Deletion requires interactive exact-target confirmation or `--confirm` with the exact target. Recursive issue deletion additionally requires `--delete-subtasks --confirm-recursive ISSUE`.
- Reads retry selected transient failures and honor `Retry-After`. Writes are sent once and report an uncertain outcome after ambiguous transport failures.
- HTTPS certificate validation cannot be disabled. Redirects are not followed, so credentials cannot cross origins.
- Responses and content inputs are bounded. Noninteractive mode never prompts.
- Jira descriptions, comments, and fields are treated as untrusted data and never executed.

See [installation instructions](docs/install.md), [architecture and dependencies](docs/architecture.md), [the security model](docs/security.md), [configuration reference](docs/configuration.md), [credential setup](docs/credentials.md), [command reference](docs/commands.md), and [agent quick start](docs/agent-quick-start.md).

## Output and exit codes

Human output is the default. `--json` emits a versioned envelope with `schemaVersion`, `ok`, and either `data` or a machine-readable `error`.

| Code | Meaning |
|---:|---|
| 0 | Success |
| 2 | Command or configuration error |
| 3 | Authentication or credential failure |
| 4 | Authorization failure or Jira not found |
| 5 | Network, protocol, or Jira API failure |
| 6 | Ambiguous user or transition lookup |
| 7 | Read-only or destructive-safety refusal |
| 8 | Write outcome uncertain or verification failed |
| 130 | Cancelled |

## Release policy

This source can be built and packed locally. NuGet publication, executable publication, tags, releases, pushes, PR creation/merge, and release workflow dispatch remain human-only actions. No release has been created by this implementation.

The authoritative CLI/package version is the `<Version>` value in `src/JiraCli.Cli/JiraCli.Cli.csproj`. Every code or documentation change merged into `main` must include an intentional Semantic Versioning update and a matching changelog update before merge. A source version bump prepares a candidate; it does not create or publish a release.

## Official references

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Jira Cloud REST API v3 introduction](https://developer.atlassian.com/cloud/jira/platform/rest/v3/intro/)
- [Jira Software Cloud REST API](https://developer.atlassian.com/cloud/jira/software/rest/intro/)
- [Atlassian API-token guidance](https://support.atlassian.com/atlassian-account/docs/manage-api-tokens-for-your-atlassian-account/)
