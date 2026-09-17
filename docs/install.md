# Installation

No package has been published. These instructions build the current source and install the resulting local tool package.

## Windows PowerShell

```powershell
git clone https://github.com/jeffpatton1971/jira.git
Set-Location jira
dotnet --info
dotnet restore JiraCli.slnx
dotnet test JiraCli.slnx -c Release
dotnet pack src/JiraCli.Cli/JiraCli.Cli.csproj -c Release -o artifacts
dotnet tool install --global JiraCli.Tool --add-source (Resolve-Path artifacts)
jcli --version
jcli --help
```

The .NET SDK normally puts `%USERPROFILE%\.dotnet\tools` on the user path. If it is missing, add that directory to the user `PATH` and open a new terminal.

## macOS

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

For persistent path setup, add the `export PATH` line to `~/.zprofile` or the startup file for the selected shell. Verify the Keychain-backed profile with `jcli auth doctor --profile work --json`.

## Linux

Install the .NET 10 SDK using Microsoft's instructions for the distribution. Install `libsecret-1` if Linux Secret Service integration is required, then:

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

Add the path export to `~/.profile`, `~/.bashrc`, or the appropriate shell startup file. A Secret Service session such as GNOME Keyring or KWallet must already be provisioned and available; JiraCli does not start or unlock it.

## Self-contained candidate executables

Replace the runtime identifier as needed:

```text
dotnet publish src/JiraCli.Cli/JiraCli.Cli.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
dotnet publish src/JiraCli.Cli/JiraCli.Cli.csproj -c Release -r win-arm64 --self-contained true -o artifacts/win-arm64
dotnet publish src/JiraCli.Cli/JiraCli.Cli.csproj -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
dotnet publish src/JiraCli.Cli/JiraCli.Cli.csproj -c Release -r linux-arm64 --self-contained true -o artifacts/linux-arm64
dotnet publish src/JiraCli.Cli/JiraCli.Cli.csproj -c Release -r osx-x64 --self-contained true -o artifacts/osx-x64
dotnet publish src/JiraCli.Cli/JiraCli.Cli.csproj -c Release -r osx-arm64 --self-contained true -o artifacts/osx-arm64
```

These are candidate local artifacts only. Publication and releases are human-only gates.
