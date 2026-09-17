# Repository instructions

This repository contains a standalone Jira Cloud CLI. It is not part of BuildAutomationTool.

## Required boundaries

- Target Jira Cloud REST API v3 and Jira Software Cloud APIs only. Do not claim Data Center compatibility.
- Never add a generic unrestricted HTTP command.
- Never execute Jira issue, comment, worklog, or field content.
- Never persist API tokens. Configuration-writing features may save only non-secret settings and credential-store references.
- Keep OS credential-store access read-only and behind its platform adapter.
- Keep `--read-only`, dry-run behavior, deletion confirmation, redirect refusal, and secret redaction centralized.
- New list commands need a bounded default and explicit `--all` behavior.
- New writes must not retry after ambiguous failures and should read back state where practical.
- Do not use real Jira credentials in tests, fixtures, examples, or CI.
- Live Jira mutation requires separate approval naming the disposable project/issues and cleanup plan.

## Verification

```text
dotnet restore JiraCli.slnx
dotnet build JiraCli.slnx --configuration Release --no-restore
dotnet test JiraCli.slnx --configuration Release --no-build
dotnet pack src/JiraCli.Cli/JiraCli.Cli.csproj --configuration Release --no-build --output artifacts
```

Update `docs/coverage.md` whenever command/API coverage changes.

## Versioning and merge policy

- `src/JiraCli.Cli/JiraCli.Cli.csproj` is the authoritative CLI and package version.
- Every branch merged into `main` with code or documentation changes must include an intentional Semantic Versioning update and a matching `CHANGELOG.md` update before merge.
- Do not reuse the same version for different source states on `main`.
- A source version bump does not authorize a tag, package publication, executable upload, GitHub release, or release workflow.

## Human-only release gate

Agents must never create, move, delete, or push Git tags; create a GitHub release; publish a NuGet package; upload release executables; or dispatch a release workflow. Report a candidate source state for a human to release manually.
