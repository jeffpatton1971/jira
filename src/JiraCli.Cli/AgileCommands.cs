using System.CommandLine;
using System.Text.Json.Nodes;
using JiraCli.Core;

namespace JiraCli.Cli;

internal static class AgileCommands
{
    public static Command BuildBoard(CliRuntime runtime)
    {
        var board = new Command("board", "Discover Jira Software boards and board backlogs.");
        board.Subcommands.Add(BuildBoardList(runtime));
        board.Subcommands.Add(BuildBoardGet(runtime));
        board.Subcommands.Add(BuildBoardBacklog(runtime));
        return board;
    }

    public static Command BuildSprint(CliRuntime runtime)
    {
        var sprint = new Command("sprint", "Discover sprints and move issues between sprints and board backlogs.");
        sprint.Subcommands.Add(BuildSprintList(runtime));
        sprint.Subcommands.Add(BuildSprintGet(runtime));
        sprint.Subcommands.Add(BuildSprintIssues(runtime));
        sprint.Subcommands.Add(BuildMove(runtime, toBacklog: false));
        sprint.Subcommands.Add(BuildMove(runtime, toBacklog: true));
        return sprint;
    }

    private static Command BuildBoardList(CliRuntime runtime)
    {
        var command = new Command("list", "List visible boards.");
        var project = CommandUtilities.Option<string?>("--project", "Filter by project key or ID.");
        var paging = new PagingOptions();
        command.Options.Add(project);
        paging.AddTo(command);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var (pageSize, all) = paging.Read(parse, context.DefaultPageSize);
            var values = new JsonArray();
            var startAt = 0;
            var isLast = false;
            do
            {
                var response = await context.Client.GetBoardsAsync(startAt, pageSize, parse.GetValue(project), token);
                var body = response.Body as JsonObject;
                var page = body?["values"] as JsonArray ?? [];
                foreach (var item in page) values.Add(item?.DeepClone());
                startAt += page.Count;
                isLast = body?["isLast"]?.GetValue<bool?>() ?? page.Count < pageSize;
                if (!all || page.Count == 0) break;
            } while (!isLast);
            return context.Output.Success(new { values, retrieved = values.Count, isLast });
        }));
        return command;
    }

    private static Command BuildBoardGet(CliRuntime runtime)
    {
        var command = new Command("get", "Read one board.");
        var id = CommandUtilities.Argument<string>("board-id", "Board ID.");
        command.Arguments.Add(id);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var response = await context.Client.GetBoardAsync(parse.GetValue(id)!, token);
            return context.Output.Success(response.Body);
        }));
        return command;
    }

    private static Command BuildBoardBacklog(CliRuntime runtime)
    {
        var command = new Command("backlog", "List a board's backlog; this is distinct from workflow status.");
        var id = CommandUtilities.Argument<string>("board-id", "Board ID.");
        var paging = new PagingOptions();
        command.Arguments.Add(id);
        paging.AddTo(command);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var (pageSize, all) = paging.Read(parse, context.DefaultPageSize);
            var issues = new JsonArray();
            var startAt = 0;
            var total = 0;
            do
            {
                var response = await context.Client.GetBoardBacklogAsync(parse.GetValue(id)!, startAt, pageSize, token);
                var body = response.Body as JsonObject;
                var page = body?["issues"] as JsonArray ?? [];
                foreach (var item in page) issues.Add(item?.DeepClone());
                total = body?["total"]?.GetValue<int?>() ?? issues.Count;
                startAt += page.Count;
                if (!all || page.Count == 0 || startAt >= total) break;
            } while (true);
            return context.Output.Success(new { issues, retrieved = issues.Count, total, meaning = "board sprint backlog; not a workflow status" });
        }));
        return command;
    }

    private static Command BuildSprintList(CliRuntime runtime)
    {
        var command = new Command("list", "List sprints for a board.");
        var board = CommandUtilities.Option<string>("--board", "Board ID.", required: true);
        var state = CommandUtilities.Option<string?>("--state", "Comma-separated state filter: active,future,closed.");
        var paging = new PagingOptions();
        command.Options.Add(board);
        command.Options.Add(state);
        paging.AddTo(command);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var (pageSize, all) = paging.Read(parse, context.DefaultPageSize);
            var values = new JsonArray();
            var startAt = 0;
            var isLast = false;
            do
            {
                var response = await context.Client.GetSprintsAsync(parse.GetValue(board)!, startAt, pageSize, parse.GetValue(state), token);
                var body = response.Body as JsonObject;
                var page = body?["values"] as JsonArray ?? [];
                foreach (var item in page) values.Add(item?.DeepClone());
                startAt += page.Count;
                isLast = body?["isLast"]?.GetValue<bool?>() ?? page.Count < pageSize;
                if (!all || page.Count == 0) break;
            } while (!isLast);
            return context.Output.Success(new { values, retrieved = values.Count, isLast });
        }));
        return command;
    }

    private static Command BuildSprintGet(CliRuntime runtime)
    {
        var command = new Command("get", "Read one sprint.");
        var id = CommandUtilities.Argument<string>("sprint-id", "Sprint ID.");
        command.Arguments.Add(id);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var response = await context.Client.GetSprintAsync(parse.GetValue(id)!, token);
            return context.Output.Success(response.Body);
        }));
        return command;
    }

    private static Command BuildSprintIssues(CliRuntime runtime)
    {
        var command = new Command("issues", "List issues in a sprint.");
        var id = CommandUtilities.Argument<string>("sprint-id", "Sprint ID.");
        var paging = new PagingOptions();
        command.Arguments.Add(id);
        paging.AddTo(command);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var (pageSize, all) = paging.Read(parse, context.DefaultPageSize);
            var issues = new JsonArray();
            var startAt = 0;
            var total = 0;
            do
            {
                var response = await context.Client.GetSprintIssuesAsync(parse.GetValue(id)!, startAt, pageSize, token);
                var body = response.Body as JsonObject;
                var page = body?["issues"] as JsonArray ?? [];
                foreach (var item in page) issues.Add(item?.DeepClone());
                total = body?["total"]?.GetValue<int?>() ?? issues.Count;
                startAt += page.Count;
                if (!all || page.Count == 0 || startAt >= total) break;
            } while (true);
            return context.Output.Success(new { issues, retrieved = issues.Count, total });
        }));
        return command;
    }

    private static Command BuildMove(CliRuntime runtime, bool toBacklog)
    {
        var command = new Command(toBacklog ? "move-to-backlog" : "move", toBacklog ? "Move issues to a board backlog without implying a workflow transition." : "Move issues into an active or future sprint.");
        var destination = CommandUtilities.Option<string?>(toBacklog ? "--board" : "--sprint", toBacklog ? "Optional board ID for board-specific backlog movement." : "Destination sprint ID.", required: !toBacklog);
        var issues = CommandUtilities.Option<string[]>("--issue", "Issue keys/IDs; repeat or comma separate.", required: true);
        issues.Arity = ArgumentArity.OneOrMore;
        command.Options.Add(destination);
        command.Options.Add(issues);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var issueKeys = CommandUtilities.SplitCsv(parse.GetValue(issues));
            if (issueKeys.Length is < 1 or > 50)
            {
                throw new JiraCliException("invalid_move_count", "Jira Software accepts between 1 and 50 issues per move.", CliExitCode.UsageOrConfiguration);
            }
            new SafetyPolicy().EnsureMutationAllowed(context.Safety, toBacklog ? "move issues to backlog" : "move issues to sprint");
            var request = new JsonObject { ["issues"] = IssueCommands.ToJsonArray(issueKeys) };
            var path = toBacklog
                ? parse.GetValue(destination) is { } board ? $"/rest/agile/1.0/backlog/{Uri.EscapeDataString(board)}/issue" : "/rest/agile/1.0/backlog/issue"
                : $"/rest/agile/1.0/sprint/{Uri.EscapeDataString(CommandUtilities.Require(parse.GetValue(destination), "--sprint"))}/issue";
            if (context.Safety.DryRun)
            {
                return context.Output.Success(CommandUtilities.Preview("POST", path, request), "Dry run only; Jira did not validate or move these issues.");
            }
            if (toBacklog)
            {
                await context.Client.MoveToBacklogAsync(parse.GetValue(destination), request, token);
            }
            else
            {
                await context.Client.MoveToSprintAsync(parse.GetValue(destination)!, request, token);
            }
            var snapshots = new JsonArray();
            try
            {
                foreach (var issue in issueKeys)
                {
                    var snapshot = await context.Client.GetIssueAsync(issue, ["status", "sprint"], null, token);
                    snapshots.Add(snapshot.Body?.DeepClone());
                }
            }
            catch (JiraCliException exception)
            {
                throw new JiraCliException(
                    "write_verification_failed",
                    "The Jira Software move succeeded, but one or more issue read-backs failed.",
                    CliExitCode.UncertainWrite,
                    new { issues = issueKeys, writeSucceeded = true, verification = "failed", cause = exception.ErrorCode },
                    exception);
            }
            return context.Output.Success(new
            {
                issues = snapshots,
                verification = "confirmed-by-read-back",
                meaning = toBacklog ? "board sprint backlog placement; workflow status was not changed by this command" : "sprint placement"
            }, toBacklog ? "Issues moved to backlog." : "Issues moved to sprint.");
        }));
        return command;
    }
}
