using System.CommandLine;
using System.Text.Json.Nodes;
using JiraCli.Core;

namespace JiraCli.Cli;

internal static class WorklogCommands
{
    public static Command Build(CliRuntime runtime, GlobalOptions globals)
    {
        var root = new Command("worklog", "List, read, add, update, or delete worklogs.");
        root.Subcommands.Add(BuildList(runtime));
        root.Subcommands.Add(BuildGet(runtime));
        root.Subcommands.Add(BuildWrite(runtime, globals, update: false));
        root.Subcommands.Add(BuildWrite(runtime, globals, update: true));
        root.Subcommands.Add(BuildDelete(runtime));
        return root;
    }

    private static Command BuildList(CliRuntime runtime)
    {
        var command = new Command("list", "List worklogs with bounded pagination.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var paging = new PagingOptions();
        command.Arguments.Add(issue);
        paging.AddTo(command);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var (pageSize, all) = paging.Read(parse, context.DefaultPageSize);
            var worklogs = new JsonArray();
            var startAt = 0;
            int? total = null;
            do
            {
                var response = await context.Client.GetWorklogsAsync(parse.GetValue(issue)!, startAt, pageSize, token);
                var body = response.Body as JsonObject;
                var page = body?["worklogs"] as JsonArray ?? [];
                foreach (var item in page) worklogs.Add(item?.DeepClone());
                total = body?["total"]?.GetValue<int?>();
                startAt += page.Count;
                if (!all || page.Count == 0 || (total is not null && startAt >= total)) break;
            } while (true);
            return context.Output.Success(new { worklogs, retrieved = worklogs.Count, total, complete = all || total == worklogs.Count });
        }));
        return command;
    }

    private static Command BuildGet(CliRuntime runtime)
    {
        var command = new Command("get", "Read one worklog.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var id = CommandUtilities.Argument<string>("worklog-id", "Worklog ID.");
        command.Arguments.Add(issue);
        command.Arguments.Add(id);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var response = await context.Client.GetWorklogAsync(parse.GetValue(issue)!, parse.GetValue(id)!, token);
            return context.Output.Success(response.Body);
        }));
        return command;
    }

    private static Command BuildWrite(CliRuntime runtime, GlobalOptions globals, bool update)
    {
        var command = new Command(update ? "update" : "add", update ? "Update a worklog." : "Add a worklog.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var id = CommandUtilities.Argument<string>("worklog-id", "Worklog ID.");
        var timeSpent = CommandUtilities.Option<string?>("--time-spent", "Jira duration such as 1h 30m.");
        var seconds = CommandUtilities.Option<int?>("--time-spent-seconds", "Duration in seconds.");
        var started = CommandUtilities.Option<string?>("--started", "Jira worklog timestamp.");
        var content = new ContentOptions();
        command.Arguments.Add(issue);
        if (update) command.Arguments.Add(id);
        command.Options.Add(timeSpent);
        command.Options.Add(seconds);
        command.Options.Add(started);
        content.AddTo(command, required: false);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var key = parse.GetValue(issue)!;
            new SafetyPolicy().EnsureMutationAllowed(context.Safety, $"worklog {(update ? "update" : "add")} {key}");
            if (parse.GetValue(timeSpent) is not null && parse.GetValue(seconds) is not null)
            {
                throw new JiraCliException("conflicting_duration", "Use either --time-spent or --time-spent-seconds.", CliExitCode.UsageOrConfiguration);
            }
            var request = new JsonObject();
            if (parse.GetValue(timeSpent) is { } duration) request["timeSpent"] = duration;
            if (parse.GetValue(seconds) is { } durationSeconds)
            {
                if (durationSeconds <= 0) throw new JiraCliException("invalid_duration", "Worklog duration must be positive.", CliExitCode.UsageOrConfiguration);
                request["timeSpentSeconds"] = durationSeconds;
            }
            if (parse.GetValue(started) is { } startedValue) request["started"] = startedValue;
            var (comment, warnings) = await content.ReadAsync(parse, parse.GetValue(globals.TokenStdin), token);
            foreach (var warning in warnings) context.Output.Warning(warning);
            if (comment is not null) request["comment"] = comment;
            if (!update && request["timeSpent"] is null && request["timeSpentSeconds"] is null)
            {
                throw new JiraCliException("duration_required", "Adding a worklog requires --time-spent or --time-spent-seconds.", CliExitCode.UsageOrConfiguration);
            }
            if (update && request.Count == 0)
            {
                throw new JiraCliException("no_updates", "No worklog updates were supplied.", CliExitCode.UsageOrConfiguration);
            }
            var path = update
                ? $"/rest/api/3/issue/{Uri.EscapeDataString(key)}/worklog/{Uri.EscapeDataString(parse.GetValue(id)!)}"
                : $"/rest/api/3/issue/{Uri.EscapeDataString(key)}/worklog";
            if (context.Safety.DryRun)
            {
                return context.Output.Success(CommandUtilities.Preview(update ? "PUT" : "POST", path, request), "Dry run only; Jira did not validate or write this worklog.");
            }
            var response = update
                ? await context.Client.UpdateWorklogAsync(key, parse.GetValue(id)!, request, token)
                : await context.Client.AddWorklogAsync(key, request, token);
            return context.Output.Success(new { result = response.Body, verification = "confirmed-by-write-response" }, update ? "Worklog updated." : "Worklog added.");
        }));
        return command;
    }

    private static Command BuildDelete(CliRuntime runtime)
    {
        var command = new Command("delete", "Delete a worklog with exact-target confirmation.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var id = CommandUtilities.Argument<string>("worklog-id", "Worklog ID.");
        var confirm = CommandUtilities.Option<string?>("--confirm", "Exact confirmation in ISSUE/worklog/ID form.");
        command.Arguments.Add(issue);
        command.Arguments.Add(id);
        command.Options.Add(confirm);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var target = $"{parse.GetValue(issue)!}/worklog/{parse.GetValue(id)!}";
            await new SafetyPolicy().ConfirmDeletionAsync(target, parse.GetValue(confirm), false, null, context.Safety, Console.In, Console.Error, token);
            if (context.Safety.DryRun)
            {
                return context.Output.Success(CommandUtilities.Preview("DELETE", $"/rest/api/3/issue/{Uri.EscapeDataString(parse.GetValue(issue)!)}/worklog/{Uri.EscapeDataString(parse.GetValue(id)!)}", null), "Dry run only; the worklog was not deleted.");
            }
            await context.Client.DeleteWorklogAsync(parse.GetValue(issue)!, parse.GetValue(id)!, token);
            return context.Output.Success(new { target, deleted = true }, "Worklog deleted.");
        }));
        return command;
    }
}
