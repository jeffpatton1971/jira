using System.CommandLine;
using System.Text.Json.Nodes;
using JiraCli.Core;
using JiraCli.JiraCloud;

namespace JiraCli.Cli;

internal static class IssueCommands
{
    public static Command Build(CliRuntime runtime, GlobalOptions globals)
    {
        var issue = new Command("issue", "Read, search, create, update, assign, transition, or delete issues.");
        issue.Subcommands.Add(BuildGet(runtime));
        issue.Subcommands.Add(BuildSearch(runtime));
        issue.Subcommands.Add(BuildCount(runtime));
        issue.Subcommands.Add(BuildCreate(runtime, globals));
        issue.Subcommands.Add(BuildUpdate(runtime, globals));
        issue.Subcommands.Add(BuildDelete(runtime));
        issue.Subcommands.Add(BuildAssign(runtime, unassign: false));
        issue.Subcommands.Add(BuildAssign(runtime, unassign: true));
        issue.Subcommands.Add(BuildTransitions(runtime));
        issue.Subcommands.Add(BuildTransition(runtime));
        return issue;
    }

    private static Command BuildGet(CliRuntime runtime)
    {
        var command = new Command("get", "Read an issue with selectable fields and expansions.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var fields = MultiOption("--fields", "Comma-separated or repeated field IDs/names.");
        var expand = MultiOption("--expand", "Comma-separated or repeated expansions.");
        var comments = CommandUtilities.Option<bool>("--comments", "Include the comments field.");
        command.Arguments.Add(issue);
        command.Options.Add(fields);
        command.Options.Add(expand);
        command.Options.Add(comments);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var selectedFields = CommandUtilities.SplitCsv(parse.GetValue(fields)).ToList();
            if (parse.GetValue(comments) && !selectedFields.Contains("comment", StringComparer.OrdinalIgnoreCase))
            {
                selectedFields.Add("comment");
            }
            var result = await context.Client.GetIssueAsync(
                parse.GetValue(issue)!,
                selectedFields.Count == 0 ? null : selectedFields,
                CommandUtilities.SplitCsv(parse.GetValue(expand)),
                token);
            return context.Output.Success(result.Body);
        }));
        return command;
    }

    private static Command BuildSearch(CliRuntime runtime)
    {
        var command = new Command("search", "Search issues with enhanced JQL search.");
        var jql = CommandUtilities.Option<string>("--jql", "JQL expression.", required: true);
        var fields = MultiOption("--fields", "Comma-separated or repeated field IDs/names.");
        var expand = MultiOption("--expand", "Comma-separated or repeated expansions.");
        var paging = new PagingOptions();
        command.Options.Add(jql);
        command.Options.Add(fields);
        command.Options.Add(expand);
        paging.AddTo(command);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var (pageSize, all) = paging.Read(parse, context.DefaultPageSize);
            var found = new JsonArray();
            string? nextPageToken = null;
            var isLast = false;
            do
            {
                var body = new JsonObject
                {
                    ["jql"] = parse.GetValue(jql)!,
                    ["maxResults"] = pageSize
                };
                var selectedFields = CommandUtilities.SplitCsv(parse.GetValue(fields));
                var selectedExpansions = CommandUtilities.SplitCsv(parse.GetValue(expand));
                if (selectedFields.Length > 0) body["fields"] = ToJsonArray(selectedFields);
                if (selectedExpansions.Length > 0) body["expand"] = string.Join(',', selectedExpansions);
                if (nextPageToken is not null)
                {
                    body["nextPageToken"] = nextPageToken;
                }
                var response = await context.Client.SearchIssuesAsync(body, token);
                var responseBody = response.Body as JsonObject;
                var page = responseBody?["issues"] as JsonArray ?? [];
                foreach (var item in page)
                {
                    found.Add(item?.DeepClone());
                }
                nextPageToken = responseBody?["nextPageToken"]?.GetValue<string>();
                isLast = responseBody?["isLast"]?.GetValue<bool?>() ?? string.IsNullOrEmpty(nextPageToken);
                if (!all || page.Count == 0)
                {
                    break;
                }
            } while (!isLast);
            return context.Output.Success(new { issues = found, retrieved = found.Count, isLast, nextPageToken = all ? null : nextPageToken });
        }));
        return command;
    }

    private static Command BuildCount(CliRuntime runtime)
    {
        var command = new Command("count", "Return Jira's explicitly approximate JQL issue count.");
        var jql = CommandUtilities.Option<string>("--jql", "JQL expression.", required: true);
        command.Options.Add(jql);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var response = await context.Client.CountIssuesAsync(parse.GetValue(jql)!, token);
            return context.Output.Success(new { approximate = true, result = response.Body });
        }));
        return command;
    }

    private static Command BuildCreate(CliRuntime runtime, GlobalOptions globals)
    {
        var command = new Command("create", "Create an issue using discovered field IDs and Jira create metadata.");
        var project = CommandUtilities.Option<string?>("--project", "Project key or ID.");
        var issueType = CommandUtilities.Option<string?>("--issue-type", "Issue type ID or name.");
        var summary = CommandUtilities.Option<string?>("--summary", "Issue summary.");
        var fieldsJson = CommandUtilities.Option<string?>("--fields-json", "Fields JSON object or @file path, including arbitrary custom fields.");
        var content = new ContentOptions();
        command.Options.Add(project);
        command.Options.Add(issueType);
        command.Options.Add(summary);
        command.Options.Add(fieldsJson);
        content.AddTo(command, required: false);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            new SafetyPolicy().EnsureMutationAllowed(context.Safety, "issue create");
            var fields = parse.GetValue(fieldsJson) is { } raw
                ? await CommandUtilities.ReadJsonObjectAsync(raw, token)
                : new JsonObject();
            var projectValue = parse.GetValue(project) ?? context.Profile.Defaults.Project;
            if (projectValue is not null)
            {
                fields["project"] = new JsonObject { ["key"] = projectValue };
            }
            if (parse.GetValue(issueType) is { } typeValue)
            {
                fields["issuetype"] = long.TryParse(typeValue, out _)
                    ? new JsonObject { ["id"] = typeValue }
                    : new JsonObject { ["name"] = typeValue };
            }
            if (parse.GetValue(summary) is { } summaryValue)
            {
                fields["summary"] = summaryValue;
            }
            var (description, warnings) = await content.ReadAsync(parse, parse.GetValue(globals.TokenStdin), token);
            foreach (var warning in warnings)
            {
                context.Output.Warning(warning);
            }
            if (description is not null)
            {
                fields["description"] = description;
            }
            if (fields["project"] is null || fields["issuetype"] is null || fields["summary"] is null)
            {
                throw new JiraCliException("required_create_fields_missing", "Create requires project, issue type, and summary, supplied explicitly or in --fields-json.", CliExitCode.UsageOrConfiguration);
            }
            var request = new JsonObject { ["fields"] = fields };
            if (context.Safety.DryRun)
            {
                return context.Output.Success(CommandUtilities.Preview("POST", "/rest/api/3/issue", request), "Dry run only; Jira did not validate or write this issue.");
            }
            var result = await context.Client.CreateIssueAsync(request, token);
            var createdKey = result.Body?["key"]?.GetValue<string>();
            if (createdKey is null)
            {
                throw new JiraCliException(
                    "write_verification_failed",
                    "Jira accepted the create request but did not return an issue key for read-back.",
                    CliExitCode.UncertainWrite,
                    new { writeSucceeded = true, verification = "failed", createResponse = result.Body });
            }
            return await VerifyIssueAsync(context, createdKey, token, "Issue created.");
        }));
        return command;
    }

    private static Command BuildUpdate(CliRuntime runtime, GlobalOptions globals)
    {
        var command = new Command("update", "Update issue fields, including arbitrary custom fields.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var fieldsJson = CommandUtilities.Option<string?>("--fields-json", "Fields JSON object or @file path.");
        var summary = CommandUtilities.Option<string?>("--summary", "New summary.");
        var labels = MultiOption("--label", "Set labels (repeat or comma separate).");
        var components = MultiOption("--component", "Set components by name.");
        var fixVersions = MultiOption("--fix-version", "Set fix versions by name.");
        var priority = CommandUtilities.Option<string?>("--priority", "Set priority by ID or name.");
        var parent = CommandUtilities.Option<string?>("--parent", "Set parent/epic by issue key or ID.");
        var clearParent = CommandUtilities.Option<bool>("--clear-parent", "Remove the parent relationship.");
        var content = new ContentOptions();
        command.Arguments.Add(issue);
        command.Options.Add(fieldsJson);
        command.Options.Add(summary);
        command.Options.Add(labels);
        command.Options.Add(components);
        command.Options.Add(fixVersions);
        command.Options.Add(priority);
        command.Options.Add(parent);
        command.Options.Add(clearParent);
        content.AddTo(command, required: false);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            new SafetyPolicy().EnsureMutationAllowed(context.Safety, $"issue update {parse.GetValue(issue)!}");
            if (parse.GetValue(parent) is not null && parse.GetValue(clearParent))
            {
                throw new JiraCliException("conflicting_parent_options", "Use either --parent or --clear-parent.", CliExitCode.UsageOrConfiguration);
            }
            var fields = parse.GetValue(fieldsJson) is { } raw
                ? await CommandUtilities.ReadJsonObjectAsync(raw, token)
                : new JsonObject();
            if (parse.GetValue(summary) is { } summaryValue) fields["summary"] = summaryValue;
            var labelValues = CommandUtilities.SplitCsv(parse.GetValue(labels));
            if (labelValues.Length > 0) fields["labels"] = ToJsonArray(labelValues);
            var componentValues = CommandUtilities.SplitCsv(parse.GetValue(components));
            if (componentValues.Length > 0) fields["components"] = ObjectArray("name", componentValues);
            var versionValues = CommandUtilities.SplitCsv(parse.GetValue(fixVersions));
            if (versionValues.Length > 0) fields["fixVersions"] = ObjectArray("name", versionValues);
            if (parse.GetValue(priority) is { } priorityValue)
            {
                fields["priority"] = long.TryParse(priorityValue, out _)
                    ? new JsonObject { ["id"] = priorityValue }
                    : new JsonObject { ["name"] = priorityValue };
            }
            if (parse.GetValue(parent) is { } parentValue) fields["parent"] = new JsonObject { ["key"] = parentValue };
            var (description, warnings) = await content.ReadAsync(parse, parse.GetValue(globals.TokenStdin), token);
            foreach (var warning in warnings) context.Output.Warning(warning);
            if (description is not null) fields["description"] = description;
            if (fields.Count == 0 && !parse.GetValue(clearParent))
            {
                throw new JiraCliException("no_updates", "No issue field updates were supplied.", CliExitCode.UsageOrConfiguration);
            }
            var request = new JsonObject { ["fields"] = fields };
            if (parse.GetValue(clearParent))
            {
                request["update"] = new JsonObject
                {
                    ["parent"] = new JsonArray(new JsonObject
                    {
                        ["set"] = new JsonObject { ["none"] = true }
                    })
                };
            }
            var key = parse.GetValue(issue)!;
            if (context.Safety.DryRun)
            {
                return context.Output.Success(CommandUtilities.Preview("PUT", $"/rest/api/3/issue/{Uri.EscapeDataString(key)}?returnIssue=true", request), "Dry run only; Jira did not validate or write this update.");
            }
            var result = await context.Client.UpdateIssueAsync(key, request, token);
            return context.Output.Success(new { result = result.Body, verification = "confirmed-by-update-response" }, "Issue updated.");
        }));
        return command;
    }

    private static Command BuildDelete(CliRuntime runtime)
    {
        var command = new Command("delete", "Delete an issue with exact-target confirmation.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var confirm = CommandUtilities.Option<string?>("--confirm", "Exact issue key/ID confirmation.");
        var deleteSubtasks = CommandUtilities.Option<bool>("--delete-subtasks", "Also delete subtasks.");
        var confirmRecursive = CommandUtilities.Option<string?>("--confirm-recursive", "Separate exact-target confirmation for recursive subtask deletion.");
        command.Arguments.Add(issue);
        command.Options.Add(confirm);
        command.Options.Add(deleteSubtasks);
        command.Options.Add(confirmRecursive);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var key = parse.GetValue(issue)!;
            await new SafetyPolicy().ConfirmDeletionAsync(
                key,
                parse.GetValue(confirm),
                parse.GetValue(deleteSubtasks),
                parse.GetValue(confirmRecursive),
                context.Safety,
                Console.In,
                Console.Error,
                token);
            if (context.Safety.DryRun)
            {
                return context.Output.Success(CommandUtilities.Preview("DELETE", $"/rest/api/3/issue/{Uri.EscapeDataString(key)}?deleteSubtasks={parse.GetValue(deleteSubtasks).ToString().ToLowerInvariant()}", null), "Dry run only; the issue was not deleted.");
            }
            await context.Client.DeleteIssueAsync(key, parse.GetValue(deleteSubtasks), token);
            return context.Output.Success(new { target = key, deleted = true }, "Issue deleted.");
        }));
        return command;
    }

    private static Command BuildAssign(CliRuntime runtime, bool unassign)
    {
        var command = new Command(unassign ? "unassign" : "assign", unassign ? "Unassign an issue." : "Assign an issue using an account ID or an unambiguous user lookup.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var accountId = CommandUtilities.Option<string?>("--account-id", "Exact Atlassian account ID.");
        var user = CommandUtilities.Option<string?>("--user-query", "User name/email query; must resolve unambiguously.");
        var project = CommandUtilities.Option<string?>("--project", "Project key for assignable-user resolution.");
        command.Arguments.Add(issue);
        if (!unassign)
        {
            command.Options.Add(accountId);
            command.Options.Add(user);
            command.Options.Add(project);
        }
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var key = parse.GetValue(issue)!;
            new SafetyPolicy().EnsureMutationAllowed(context.Safety, $"issue {(unassign ? "unassign" : "assign")} {key}");
            string? resolved = null;
            if (!unassign)
            {
                if (parse.GetValue(accountId) is not null && parse.GetValue(user) is not null)
                {
                    throw new JiraCliException("conflicting_user_targets", "Use either --account-id or --user-query.", CliExitCode.UsageOrConfiguration);
                }
                resolved = parse.GetValue(accountId) ?? await ResolveUserAsync(context, CommandUtilities.Require(parse.GetValue(user), "--user-query"), parse.GetValue(project) ?? context.Profile.Defaults.Project, token);
            }
            var request = new JsonObject { ["accountId"] = resolved };
            if (context.Safety.DryRun)
            {
                return context.Output.Success(CommandUtilities.Preview("PUT", $"/rest/api/3/issue/{Uri.EscapeDataString(key)}/assignee", request), "Dry run only; assignment was not changed.");
            }
            await context.Client.AssignIssueAsync(key, resolved, token);
            return await VerifyIssueAsync(context, key, token, unassign ? "Issue unassigned." : "Issue assigned.");
        }));
        return command;
    }

    private static Command BuildTransitions(CliRuntime runtime)
    {
        var command = new Command("transitions", "Discover transitions and optional required fields for an issue.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var fields = CommandUtilities.Option<bool>("--fields", "Include transition field metadata.");
        command.Arguments.Add(issue);
        command.Options.Add(fields);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var result = await context.Client.GetTransitionsAsync(parse.GetValue(issue)!, parse.GetValue(fields), token);
            return context.Output.Success(result.Body);
        }));
        return command;
    }

    private static Command BuildTransition(CliRuntime runtime)
    {
        var command = new Command("transition", "Perform a transition by exact ID or unambiguous name.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var transition = CommandUtilities.Argument<string>("transition", "Transition ID or exact name.");
        var fieldsJson = CommandUtilities.Option<string?>("--fields-json", "Transition field JSON object or @file path.");
        command.Arguments.Add(issue);
        command.Arguments.Add(transition);
        command.Options.Add(fieldsJson);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var key = parse.GetValue(issue)!;
            new SafetyPolicy().EnsureMutationAllowed(context.Safety, $"issue transition {key}");
            var available = await context.Client.GetTransitionsAsync(key, includeFields: true, token);
            var chosen = JiraLookupResolver.ResolveTransition(available.Body, parse.GetValue(transition)!);
            var request = new JsonObject
            {
                ["transition"] = new JsonObject { ["id"] = chosen["id"]?.GetValue<string>() }
            };
            if (parse.GetValue(fieldsJson) is { } raw)
            {
                request["fields"] = await CommandUtilities.ReadJsonObjectAsync(raw, token);
            }
            if (context.Safety.DryRun)
            {
                return context.Output.Success(CommandUtilities.Preview("POST", $"/rest/api/3/issue/{Uri.EscapeDataString(key)}/transitions", request), "Dry run only; Jira did not validate or perform the transition.");
            }
            await context.Client.TransitionIssueAsync(key, request, token);
            return await VerifyIssueAsync(context, key, token, "Issue transitioned.");
        }));
        return command;
    }

    internal static async Task<int> VerifyIssueAsync(CliContext context, string issue, CancellationToken token, string message)
    {
        try
        {
            var readBack = await context.Client.GetIssueAsync(issue, null, null, token);
            return context.Output.Success(new { issue = readBack.Body, verification = "confirmed" }, message);
        }
        catch (JiraCliException exception)
        {
            throw new JiraCliException(
                "write_verification_failed",
                "The write succeeded, but reading back the changed issue failed.",
                CliExitCode.UncertainWrite,
                new { issue, writeSucceeded = true, verification = "failed", cause = exception.ErrorCode },
                exception);
        }
    }

    internal static JsonArray ToJsonArray(IEnumerable<string> values)
        => new(values.Select(value => JsonValue.Create(value)).ToArray());

    private static JsonArray ObjectArray(string property, IEnumerable<string> values)
        => new(values.Select(value => (JsonNode)new JsonObject { [property] = value }).ToArray());

    private static Option<string[]> MultiOption(string name, string description)
    {
        var option = CommandUtilities.Option<string[]>(name, description);
        option.Arity = ArgumentArity.ZeroOrMore;
        return option;
    }

    private static async Task<string> ResolveUserAsync(CliContext context, string query, string? project, CancellationToken token)
    {
        var response = await context.Client.FindUsersAsync(query, assignable: project is not null, project, token);
        return JiraLookupResolver.ResolveAccountId(response.Body, query);
    }
}
