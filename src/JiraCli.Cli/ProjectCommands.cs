using System.CommandLine;
using System.Text.Json.Nodes;

namespace JiraCli.Cli;

internal static class ProjectCommands
{
    public static Command Build(CliRuntime runtime)
    {
        var project = new Command("project", "List projects and inspect issue metadata.");
        project.Subcommands.Add(BuildList(runtime, "list", null));

        var searchQuery = CommandUtilities.Option<string?>("--query", "Project name/key search text.");
        var search = BuildList(runtime, "search", searchQuery);
        project.Subcommands.Add(search);

        var issueTypes = new Command("issue-types", "Discover issue types available for creating issues.");
        var projectArg = CommandUtilities.Argument<string>("project", "Project key or ID.");
        issueTypes.Arguments.Add(projectArg);
        issueTypes.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var result = await context.Client.GetCreateIssueTypesAsync(parse.GetValue(projectArg)!, token);
            return context.Output.Success(result.Body);
        }));
        project.Subcommands.Add(issueTypes);

        var fields = new Command("fields", "Discover create fields for an issue type or edit fields for an existing issue.");
        var projectOption = CommandUtilities.Option<string?>("--project", "Project key or ID for create metadata.");
        var issueTypeOption = CommandUtilities.Option<string?>("--issue-type", "Issue type ID for create metadata.");
        var issueOption = CommandUtilities.Option<string?>("--issue", "Issue key for edit metadata.");
        fields.Options.Add(projectOption);
        fields.Options.Add(issueTypeOption);
        fields.Options.Add(issueOption);
        fields.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var issue = parse.GetValue(issueOption);
            var projectValue = parse.GetValue(projectOption);
            var issueType = parse.GetValue(issueTypeOption);
            if (issue is not null && (projectValue is not null || issueType is not null))
            {
                throw new JiraCli.Core.JiraCliException("conflicting_metadata_targets", "Use --issue alone, or use --project with --issue-type.", JiraCli.Core.CliExitCode.UsageOrConfiguration);
            }
            var result = issue is not null
                ? await context.Client.GetEditFieldsAsync(issue, token)
                : await context.Client.GetCreateFieldsAsync(
                    CommandUtilities.Require(projectValue, "--project"),
                    CommandUtilities.Require(issueType, "--issue-type"),
                    token);
            return context.Output.Success(result.Body);
        }));
        project.Subcommands.Add(fields);
        project.Subcommands.Add(BuildResourceList(runtime, "components", "List components available in a project.", "project"));
        project.Subcommands.Add(BuildResourceList(runtime, "versions", "List fix versions available in a project.", "project"));
        project.Subcommands.Add(BuildResourceList(runtime, "priorities", "List priorities visible to the current Jira account.", null));
        return project;
    }

    private static Command BuildResourceList(CliRuntime runtime, string name, string description, string? argumentName)
    {
        var command = new Command(name, description);
        Argument<string>? project = null;
        if (argumentName is not null)
        {
            project = CommandUtilities.Argument<string>(argumentName, "Project key or ID.");
            command.Arguments.Add(project);
        }
        var paging = new PagingOptions();
        paging.AddTo(command);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var (pageSize, all) = paging.Read(parse, context.DefaultPageSize);
            var values = new JsonArray();
            var startAt = 0;
            int? total = null;
            do
            {
                var response = name switch
                {
                    "components" => await context.Client.GetProjectComponentsAsync(parse.GetValue(project!)!, startAt, pageSize, token),
                    "versions" => await context.Client.GetProjectVersionsAsync(parse.GetValue(project!)!, startAt, pageSize, token),
                    _ => await context.Client.GetPrioritiesAsync(startAt, pageSize, token)
                };
                var body = response.Body as JsonObject;
                var page = body?["values"] as JsonArray ?? [];
                foreach (var item in page) values.Add(item?.DeepClone());
                total = body?["total"]?.GetValue<int?>();
                startAt += page.Count;
                if (!all || page.Count == 0 || body?["isLast"]?.GetValue<bool?>() == true || (total is not null && startAt >= total)) break;
            } while (true);
            return context.Output.Success(new { values, retrieved = values.Count, total, complete = all || total == values.Count });
        }));
        return command;
    }

    private static Command BuildList(CliRuntime runtime, string name, Option<string?>? queryOption)
    {
        var command = new Command(name, name == "list" ? "List accessible projects." : "Search accessible projects.");
        var paging = new PagingOptions();
        paging.AddTo(command);
        if (queryOption is not null)
        {
            command.Options.Add(queryOption);
        }
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var (pageSize, all) = paging.Read(parse, context.DefaultPageSize);
            var values = new JsonArray();
            var startAt = 0;
            int? total = null;
            do
            {
                var response = await context.Client.SearchProjectsAsync(startAt, pageSize, queryOption is null ? null : parse.GetValue(queryOption), token);
                var body = response.Body as JsonObject;
                var page = body?["values"] as JsonArray ?? [];
                foreach (var item in page)
                {
                    values.Add(item?.DeepClone());
                }
                total = body?["total"]?.GetValue<int?>();
                startAt += page.Count;
                if (!all || page.Count == 0 || body?["isLast"]?.GetValue<bool?>() == true || (total is not null && startAt >= total))
                {
                    break;
                }
            } while (true);
            return context.Output.Success(new { values, retrieved = values.Count, total, complete = all || total == values.Count });
        }));
        return command;
    }
}
