# Credential-provider setup

JiraCli only reads credentials. It has no login or token-writing command and never stores tokens in configuration, caches, temporary files, shell profiles, or an OS credential store.

## Windows Credential Manager

Provision a **Generic Credential** separately using Windows Credential Manager. Set its target to the exact configured value, such as `jiracli/work`, and put only the API token in the credential value. The adapter calls `CredReadW` for that exact target; it does not enumerate credentials.

```json
"credential": {
  "provider": "windows-credential-manager",
  "target": "jiracli/work"
}
```

Generic credential blobs are application-defined. JiraCli accepts UTF-16 values created by common Windows credential tooling and UTF-8 values created by compatible tooling.

## macOS Keychain

Provision a generic password separately:

```bash
security add-generic-password -a work -s jiracli -w
```

The command prompts for the token. Do not put the token directly on its command line.

```json
"credential": {
  "provider": "macos-keychain",
  "service": "jiracli",
  "account": "work"
}
```

The adapter uses Security.framework directly with Keychain interaction disabled during lookup. It reports when authorization must be performed separately instead of hanging for UI input.

## Linux Secret Service

Install `libsecret` for the distribution and provision the secret separately. This example asks `secret-tool` to read the secret from standard input:

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

The adapter calls `libsecret-1` directly and searches only the named `service` and `account` attributes. It does not start a shell, pass a secret in process arguments, capture a helper's output, enumerate unrelated items, or write secrets.

## Diagnostics

`jcli auth doctor` distinguishes item not found, unavailable/locked store, access denied, interactive authorization required, and unsupported backend. It reports the provider name but never a secret value.

API tokens with scopes and tokens without scopes are both account-email/API-token Basic authentication. Unscoped tokens call the site URL; scoped tokens call the Atlassian gateway and require a Cloud ID. OAuth bearer access tokens are a different credential type and are not supported by this implementation.
