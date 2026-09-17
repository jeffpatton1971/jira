# Credential-provider setup

JiraCli only reads credentials. It has no login or token-writing command and never stores tokens in configuration, caches, temporary files, shell profiles, or an OS credential store.

## How credential references work

The profile-level `user` is the Atlassian account email used for Jira authentication. The nested `credential` object does not contain the token; it identifies an existing item in the operating system's credential store. The password or secret value of that native item must contain only the Jira API token.

| OS | `provider` | Lookup properties | Native item fields |
|---|---|---|---|
| Windows | `windows-credential-manager` | `target` | Generic Credential **Internet or network address** / target name |
| macOS | `macos-keychain` | `service`, `account` | Generic Password **Name/service** and **Account** |
| Linux | `linux-secret-service` | `service`, `account` | Secret Service string attributes named `service` and `account` |

The lookup values are user-chosen identifiers, must match the native item exactly, and are not sent to Jira. The Keychain or Secret Service `account` often contains an email address, but it can instead be a label such as `work`; it is independent of the profile-level Jira `user`. In JSON, write an email address with a plain `@`, not `\@`.

Only include the properties shown for the selected provider. `target` is used only on Windows; `service` and `account` are used only on macOS and Linux; `attributes` is not currently used by any provider.

## Windows Credential Manager

Provision a **Generic Credential** separately:

1. Open **Credential Manager** from Windows Control Panel.
2. Select **Windows Credentials** and then **Add a generic credential**.
3. Set **Internet or network address** to a unique target such as `jiracli/work`.
4. The username is descriptive metadata and is not used by JiraCli.
5. Put only the Jira API token in the password field and save the credential.

Configure the same target value:

```json
"credential": {
  "provider": "windows-credential-manager",
  "target": "jiracli/work"
}
```

- `provider` selects the Windows Credential Manager adapter.
- `target` maps exactly to the Generic Credential's target/**Internet or network address**.
- The credential's password is the Jira API token.
- The credential's username is not read; the profile-level `user` supplies the Atlassian email.

The adapter calls `CredReadW` for that one Generic Credential and does not enumerate other credentials. Generic credential blobs are application-defined; JiraCli accepts UTF-16 values created by common Windows tooling and UTF-8 values created by compatible tooling.

## macOS Keychain

Provision a **Generic Password** separately in Keychain Access, using a **Name** (service), an **Account**, and the Jira API token as its password. The equivalent native Terminal command is:

```zsh
security add-generic-password -a work -s jiracli -w
```

The command prompts for the token. Do not put the token directly on the command line. Configure the same service and account values:

```json
"credential": {
  "provider": "macos-keychain",
  "service": "jiracli",
  "account": "work"
}
```

- `provider` selects the macOS Keychain adapter.
- `service` maps to the Generic Password's **Name/service** and to `security ... -s`.
- `account` maps to the Generic Password's **Account** and to `security ... -a`.
- The Generic Password's password is the Jira API token.
- The profile-level `user` supplies the Atlassian email. It may equal the Keychain account, but the two fields have separate purposes.

The pair of `service` and `account` identifies the item, so both values must match exactly. The adapter uses Security.framework directly. During a normal interactive command, macOS may request the login/keychain password and offer **Allow Once**, **Always Allow**, or **Deny**; this is macOS authorizing `jcli`, not Jira requesting the API token. Choose **Always Allow** only when the displayed requesting executable is the expected `jcli` installation.

With `--non-interactive`, JiraCli disables Keychain UI and limits the lookup to 15 seconds. An item that requires a prompt then fails safely instead of hanging an automation or agent process. Pre-authorize the installed `jcli` executable in the item's Keychain Access **Access Control** pane before using it noninteractively.

If access unexpectedly fails, confirm that the item is a Generic Password in the user's default keychain and verify interactive access first:

```zsh
jcli auth doctor --profile work --json
```

After interactive authorization succeeds, verify the automation path separately:

```zsh
jcli auth doctor --profile work --non-interactive --json
```

## Linux Secret Service

Install `libsecret` for the distribution and ensure a Secret Service implementation such as GNOME Keyring or KWallet is running and unlocked. Provision the secret separately. This example disables terminal echo and sends the token to `secret-tool` through standard input:

```bash
printf 'API token: ' >&2
stty -echo
IFS= read -r token
stty echo
printf '\n' >&2
printf '%s' "$token" | secret-tool store --label='JiraCli work token' service jiracli account work
unset token
```

```json
"credential": {
  "provider": "linux-secret-service",
  "service": "jiracli",
  "account": "work"
}
```

- `provider` selects the Linux Secret Service adapter.
- `service` maps to the Secret Service string attribute named `service`.
- `account` maps to the Secret Service string attribute named `account`.
- The stored secret is the Jira API token.
- `--label` controls the human-readable display name only; JiraCli does not use it for lookup.

The `service` and `account` attributes must both match exactly. The adapter calls `libsecret-1` directly and searches only those two attributes. It does not start a shell, pass a secret in process arguments, capture a helper's output, enumerate unrelated items, or write secrets.

## Diagnostics

After creating the native item and configuration profile, verify the reference without displaying the secret:

```text
jcli auth doctor --profile work --json
```

The command distinguishes item not found, unavailable/locked store, access denied, interactive authorization required, and unsupported backend. It reports the provider name but never a secret value. If an item is not found, compare the configured lookup values character-for-character with the native item; provider lookups do not fall back to other items.

API tokens with scopes and tokens without scopes are both account-email/API-token Basic authentication. Unscoped tokens call the site URL; scoped tokens call the Atlassian gateway and require a Cloud ID. OAuth bearer access tokens are a different credential type and are not supported by this implementation.
