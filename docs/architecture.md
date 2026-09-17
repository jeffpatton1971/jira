# Architecture and dependencies

```text
JiraCli.Cli
  -> JiraCli.Core
  -> JiraCli.JiraCloud
  -> JiraCli.Credentials
       -> JiraCli.Credentials.Windows
       -> JiraCli.Credentials.Mac
       -> JiraCli.Credentials.Linux
```

`JiraCli.Cli` owns parsing and presentation. `JiraCli.Core` owns domain-neutral safety, redaction, errors, results, and ADF conversion. `JiraCli.JiraCloud` owns endpoint construction, transport, Jira operations, pagination shapes, and ambiguity rules. Credential contracts and configuration are reusable without the executable; platform adapters contain native interop only.

## Runtime dependencies

- `System.CommandLine` 2.0.12: Microsoft's stable parser supplies discoverable nested commands, generated help, arity, required-option validation, and cancellation-aware handlers. Reimplementing these behaviors would enlarge the security-sensitive parsing surface.
- `Markdig` 0.41.3: parses Markdown into an AST for the documented, deliberately limited ADF conversion. It is not used to render or execute HTML.
- .NET platform libraries: `HttpClient`, `System.Text.Json`, cryptography, and native interop cover transport, output, and credential access without additional runtime frameworks.
- Windows uses `CredReadW`; macOS uses Security.framework; Linux uses the system `libsecret-1` API. No platform adapter writes or enumerates secrets.

## Test-only dependencies

- xUnit v3 and its Visual Studio runner provide cross-platform tests.
- `Microsoft.NET.Test.Sdk` integrates tests with `dotnet test` and CI.
- `coverlet.collector` permits optional coverage collection without affecting runtime artifacts.

Package versions are centrally pinned in `Directory.Packages.props`. The production dependency graph intentionally remains small.
