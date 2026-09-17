# Capability coverage matrix

All rows below are implemented. Permissions are Jira user permissions; scoped API tokens additionally need the endpoint's selected classic or granular scopes. Exact authorization remains controlled by Jira project permissions and token scopes.

| Capability | CLI command | Jira Cloud endpoint | Permission / common scope | Contract tests | Status |
|---|---|---|---|---|---|
| Identity/connectivity | `auth check`, `auth doctor`, `site check` | `GET /rest/api/3/myself`, `/serverInfo` | Jira access; user read | transport auth/error and client map | Implemented |
| Site inventory | `site list` | Local configured profiles | Local file read | configuration behavior | Implemented |
| Broad site discovery | `site discover` | Not available to API-token Basic auth | OAuth accessible resources would be required | explicit unsupported result | Implemented limitation |
| Projects | `project list/search` | `GET /rest/api/3/project/search` | Browse Projects; `read:jira-work` | path/pagination | Implemented |
| Issue types | `project issue-types` | `GET /issue/createmeta/{project}/issuetypes` | Browse/Create Issues; field/project read | path contract | Implemented |
| Create fields | `project fields --project --issue-type` | `GET /issue/createmeta/{project}/issuetypes/{type}` | Browse/Create Issues | path/custom-field tests | Implemented |
| Edit fields | `project fields --issue` | `GET /issue/{issue}/editmeta` | Browse/Edit Issues | path contract | Implemented |
| Components | `project components` | `GET /project/{project}/component` | Browse Projects; component read | path/pagination | Implemented |
| Fix versions | `project versions` | `GET /project/{project}/version` | Browse Projects; version read | path/pagination | Implemented |
| Priorities | `project priorities` | `GET /priority/search` | Jira access; priority scope as required | path/pagination | Implemented |
| Users/account IDs | `user find/assignable` | `GET /user/search`, `/user/assignable/search` | Browse Users and Groups; user read | path and ambiguity | Implemented |
| Issue read | `issue get` | `GET /issue/{key}` | Browse Projects; `read:jira-work` | selectable fields/expand path | Implemented |
| JQL search | `issue search` | `POST /search/jql` | Browse Projects; JQL/issue read | token paging/retry contract | Implemented |
| Explicit count | `issue count` | `POST /search/approximate-count` | Browse Projects; issue read | endpoint and approximate label | Implemented |
| Create issue | `issue create` | `POST /issue` | Create Issues; `write:jira-work` | arbitrary fields, ADF, preview | Implemented |
| Edit/common fields | `issue update` | `PUT /issue/{key}` | Edit Issues; `write:jira-work` | fields, labels, components, priority, parent, versions | Implemented |
| Assign/unassign | `issue assign/unassign` | `PUT /issue/{key}/assignee` | Assign Issues | ambiguity/read-back | Implemented |
| Workflow | `issue transitions/transition` | `GET/POST /issue/{key}/transitions` | Transition Issues | duplicate names, IDs, read-back | Implemented |
| Delete issue | `issue delete` | `DELETE /issue/{key}` | Delete Issues | confirmation/read-only/recursive gate | Implemented |
| Comments CRUD | `comment ...` | `/issue/{key}/comment[/id]` | Browse/Add/Edit/Delete comments | all method/path contracts, ADF | Implemented |
| Worklogs CRUD | `worklog ...` | `/issue/{key}/worklog[/id]` | Work on Issues; edit/delete worklogs | all method/path contracts, ADF | Implemented |
| Remote links CRUD | `remote-link ...` | `/issue/{key}/remotelink[/id]` | Link Issues | all method/path contracts | Implemented |
| Boards | `board list/get/backlog` | `/rest/agile/1.0/board...` | Board visibility; board read | Software path/pagination | Implemented |
| Sprints | `sprint list/get/issues` | Board/sprint endpoints | Sprint/board visibility; sprint read | Software path/pagination | Implemented |
| Move to sprint | `sprint move` | `POST /sprint/{id}/issue` | Schedule Issues; software write | max-50/preview/read-back | Implemented |
| Move to backlog | `sprint move-to-backlog` | `POST /backlog[/boardId]/issue` | Schedule Issues; software write | max-50/preview/read-back | Implemented |

## Test classification

- Unit tests: redaction, secret validation, safety policy, ADF, and ambiguity.
- Credential contract tests: precedence and every diagnostic status using synthetic providers.
- HTTP contract tests: all required methods/paths with an in-memory handler and synthetic Basic credentials.
- Transport tests: redirect refusal, authentication errors, rate limits, cancellation, and uncertain writes.
- CLI tests: parse exit code, custom-field dry run, and central read-only enforcement.
- Platform CI: build/test/pack on Windows, macOS, and Linux.
- Actual OS-store integration: manual only because hosted CI lacks a reliably unlocked per-user store/session. No real secret is placed in CI.
- Live Jira validation: not performed. Initial live validation must be separately approved and read-only.
