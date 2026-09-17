# JiraCli

`jcli` is a standalone, safety-focused Jira Cloud CLI for humans and automation. It targets .NET 10 LTS and runs on Windows, macOS, and Linux.

It is not a BAT component, a Jira Data Center client, a Jira administrator, or an unrestricted HTTP client.

## Status

The implemented commands cover Jira Cloud projects, create/edit metadata, users, issues, enhanced JQL search and approximate counts, workflow transitions, comments, worklogs, remote links, boards, sprints, and board backlogs. See [the coverage matrix](docs/coverage.md) for endpoints, permissions, and tests.

Rovo's federated or natural-language search is not equivalent to JQL. This tool provides Jira JQL search and does not claim federated Rovo search parity.

## Build

Prerequisite: a supported .NET 10 SDK.

```text
dotnet restore JiraCli.slnx
dotnet build JiraCli.slnx --configuration Release --no-restore
dotnet test JiraCli.slnx --configuration Release --no-build
```

The repository selects SDK feature band `10.0.400` with controlled roll-forward in `global.json`.

## Install as a .NET tool

Create and install a local package:

```text
dotnet pack src/JiraCli.Cli/JiraCli.Cli.csproj -c Release -o artifacts
dotnet tool install --global JiraCli.Tool --add-source artifacts
jcli --help
```

For a repository-pinned local tool:

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

## Exact macOS installation and verification

These steps require no PowerShell:

```bash
git clone https://github.com/jeffpatton1971/jira.git
cd jira
dotnet --info
dotnet restore JiraCli.slnx
dotnet test JiraCli.slnx -c Release
dotnet pack src/JiraCli.Cli/JiraCli.Cli.csproj -c Release -o artifacts
dotnet tool install --global JiraCli.Tool --add-source "$PWD/artifacts"
export PATH="$PATH:$HOME/.dotnet/tools"
jcli --version
jcli --help
```

Verify a configured profile without exposing the token:

```bash
jcli auth doctor --profile work --json
```

If `jcli` is not found, add `export PATH="$PATH:$HOME/.dotnet/tools"` to the appropriate shell startup file.

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

## Official references

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Jira Cloud REST API v3 introduction](https://developer.atlassian.com/cloud/jira/platform/rest/v3/intro/)
- [Jira Software Cloud REST API](https://developer.atlassian.com/cloud/jira/software/rest/intro/)
- [Atlassian API-token guidance](https://support.atlassian.com/atlassian-account/docs/manage-api-tokens-for-your-atlassian-account/)
