# Installation

No package has been published. These instructions build the current source and install the resulting local tool package.

## Requirements and architecture

Install Git and the **.NET 10 SDK** before cloning the repository. The .NET runtime by itself can run framework-dependent applications, but it cannot restore, test, pack, or publish this project. The SDK includes the runtime.

Determine the native architecture before choosing an installer:

| OS | Command | Choose |
|---|---|---|
| Windows PowerShell | `Get-CimInstance Win32_ComputerSystem \| Select-Object SystemType` | `x64` for an x64-based PC; `arm64` for an ARM64-based PC |
| macOS | `uname -m` | `x64` for `x86_64`; `arm64` for Apple silicon |
| Linux | `uname -m` | `x64` for `x86_64`; `arm64` for `aarch64` or `arm64` |

After installing the SDK, open a new terminal and run:

```text
dotnet --version
dotnet --info
dotnet --list-sdks
```

The repository's `global.json` requires SDK `10.0.400` or a later .NET 10 feature band through controlled roll-forward. Install a current .NET 10 SDK if the repository reports that no compatible SDK was found.

## Windows PowerShell

Install the SDK with WinGet, which selects the native architecture automatically:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact --source winget
```

Alternatively, choose the matching SDK installer using [Microsoft's Windows instructions](https://learn.microsoft.com/dotnet/core/install/windows). Open a new PowerShell terminal after installation, then install `jcli`:

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

## macOS Terminal (`zsh`)

Use [Microsoft's macOS instructions](https://learn.microsoft.com/dotnet/core/install/macos) to download the .NET 10 SDK installer. Choose `Arm64` for an Apple silicon Mac or `x64` for an Intel Mac. Open Terminal using the default macOS shell, `zsh`, then install `jcli`:

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

For persistent path setup, add the `export PATH` line to `~/.zprofile`, then open a new Terminal window. Verify the Keychain-backed profile with `jcli auth doctor --profile work --json`.

## Linux terminal (`bash`)

Use [Microsoft's Linux instructions](https://learn.microsoft.com/dotnet/core/install/linux) for the specific distribution and architecture. With the appropriate package feed configured, the package is normally named `dotnet-sdk-10.0`; for example:

```bash
sudo apt update
sudo apt install dotnet-sdk-10.0
```

Microsoft's .NET 10 Linux packages cover `x64` and `arm64`, but feed setup and prerequisites vary by distribution. Install `libsecret-1` only if Linux Secret Service integration is required, then install `jcli`:

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
