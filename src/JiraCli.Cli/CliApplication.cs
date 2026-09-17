using System.CommandLine;
using JiraCli.Core;

namespace JiraCli.Cli;

public static class CliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        var globals = new GlobalOptions();
        var runtime = new CliRuntime(globals);
        var root = new RootCommand("Safety-focused Jira Cloud CLI for humans and automation.");
        foreach (var option in globals.All)
        {
            root.Options.Add(option);
        }

        root.Subcommands.Add(AuthCommands.Build(runtime, globals));
        root.Subcommands.Add(SiteCommands.Build(runtime, globals));
        root.Subcommands.Add(ProjectCommands.Build(runtime));
        root.Subcommands.Add(UserCommands.Build(runtime));
        root.Subcommands.Add(IssueCommands.Build(runtime, globals));
        root.Subcommands.Add(CommentCommands.Build(runtime, globals));
        root.Subcommands.Add(WorklogCommands.Build(runtime, globals));
        root.Subcommands.Add(RemoteLinkCommands.Build(runtime));
        root.Subcommands.Add(AgileCommands.BuildBoard(runtime));
        root.Subcommands.Add(AgileCommands.BuildSprint(runtime));

        var parseResult = root.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            var output = runtime.CreateOutput(parseResult);
            return output.Failure(
                "invalid_command_line",
                string.Join(Environment.NewLine, parseResult.Errors.Select(error => error.Message)),
                CliExitCode.UsageOrConfiguration);
        }

        return await parseResult.InvokeAsync();
    }
}
