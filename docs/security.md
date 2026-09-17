# Security model

## Protected properties

- Tokens are never intentionally persisted or logged.
- Authorization headers are created only after validating the destination.
- Requests are restricted to HTTPS Jira Cloud site origins or the configured Cloud-ID gateway path.
- Redirects are rejected and never receive forwarded credentials.
- Ordinary platform certificate validation is mandatory; there is no `--insecure` option.
- Tokens containing control characters are rejected instead of normalized or concatenated.
- Response bodies and file inputs are bounded to 16 MiB.
- Read retries are bounded and honor `Retry-After`; ambiguous writes are not retried.
- Exceptions, JSON errors, previews, and diagnostics pass through redaction.
- `--read-only` is checked in a central policy before mutations.
- Jira content is untrusted data and is never sent to a shell or treated as instructions.

## Mutation states

`--dry-run` creates a local preview with `serverValidated: false`. A successful HTTP write is reported separately from read-back verification. A timeout or network failure during a write exits with code 8 and instructs the caller to inspect the target before retrying.

Issue assignment, transitions, and sprint/backlog moves perform read-back where practical. Create/update comment/worklog/link endpoints return changed state directly. Deletes report the server's successful response but cannot read back a deleted resource.

## Deletion

Issue confirmation is the exact issue key/ID. Comment, worklog, and remote-link confirmations use `ISSUE/comment/ID`, `ISSUE/worklog/ID`, and `ISSUE/remote-link/ID`. Recursive subtask deletion needs both `--delete-subtasks` and `--confirm-recursive ISSUE`.

## Trust limits

Agent instructions and documentation guide behavior but are not a technical security boundary. Jira permissions, API-token scopes, OS account isolation, credential-store policy, TLS, the read-only switch, and destination validation provide technical controls.

The CLI cannot prevent a user from placing a token in shell history via `--token`, exposing it through an environment variable, granting excessive Jira permissions, or provisioning an OS-store item with weak local policy.

## Testing boundary

CI uses synthetic values and in-memory HTTP handlers. Platform adapter contract behavior is tested without a real secret. Actual unlocked Credential Manager, Keychain, and Secret Service tests require an interactive platform session and are intentionally manual. No live Jira mutation is part of CI.
