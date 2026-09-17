using System.CommandLine;

namespace JiraCli.Cli;

internal static class UserCommands
{
    public static Command Build(CliRuntime runtime)
    {
        var user = new Command("user", "Look up Jira users and account IDs.");
        user.Subcommands.Add(BuildLookup(runtime, assignable: false));
        user.Subcommands.Add(BuildLookup(runtime, assignable: true));
        return user;
    }

    private static Command BuildLookup(CliRuntime runtime, bool assignable)
    {
        var command = new Command(assignable ? "assignable" : "find", assignable ? "Find users assignable in a project." : "Find visible Jira users.");
        var query = CommandUtilities.Argument<string>("query", "Name, email, or account search text.");
        var project = CommandUtilities.Option<string?>("--project", "Project key required for assignable-user lookup.");
        command.Arguments.Add(query);
        if (assignable)
        {
            command.Options.Add(project);
        }
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var result = await context.Client.FindUsersAsync(
                parse.GetValue(query)!,
                assignable,
                assignable ? CommandUtilities.Require(parse.GetValue(project), "--project") : null,
                token);
            return context.Output.Success(result.Body);
        }));
        return command;
    }
}
