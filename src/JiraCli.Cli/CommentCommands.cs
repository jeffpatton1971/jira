using System.CommandLine;
using System.Text.Json.Nodes;
using JiraCli.Core;

namespace JiraCli.Cli;

internal static class CommentCommands
{
    public static Command Build(CliRuntime runtime, GlobalOptions globals)
    {
        var root = new Command("comment", "List, read, add, update, or delete issue comments.");
        root.Subcommands.Add(BuildList(runtime));
        root.Subcommands.Add(BuildGet(runtime));
        root.Subcommands.Add(BuildWrite(runtime, globals, update: false));
        root.Subcommands.Add(BuildWrite(runtime, globals, update: true));
        root.Subcommands.Add(BuildDelete(runtime));
        return root;
    }

    private static Command BuildList(CliRuntime runtime)
    {
        var command = new Command("list", "List comments with bounded pagination.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var paging = new PagingOptions();
        command.Arguments.Add(issue);
        paging.AddTo(command);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var (pageSize, all) = paging.Read(parse, context.DefaultPageSize);
            var comments = new JsonArray();
            var startAt = 0;
            int? total = null;
            do
            {
                var response = await context.Client.GetCommentsAsync(parse.GetValue(issue)!, startAt, pageSize, token);
                var body = response.Body as JsonObject;
                var page = body?["comments"] as JsonArray ?? [];
                foreach (var item in page) comments.Add(item?.DeepClone());
                total = body?["total"]?.GetValue<int?>();
                startAt += page.Count;
                if (!all || page.Count == 0 || (total is not null && startAt >= total)) break;
            } while (true);
            return context.Output.Success(new { comments, retrieved = comments.Count, total, complete = all || total == comments.Count });
        }));
        return command;
    }

    private static Command BuildGet(CliRuntime runtime)
    {
        var command = new Command("get", "Read one comment.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var id = CommandUtilities.Argument<string>("comment-id", "Comment ID.");
        command.Arguments.Add(issue);
        command.Arguments.Add(id);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var response = await context.Client.GetCommentAsync(parse.GetValue(issue)!, parse.GetValue(id)!, token);
            return context.Output.Success(response.Body);
        }));
        return command;
    }

    private static Command BuildWrite(CliRuntime runtime, GlobalOptions globals, bool update)
    {
        var command = new Command(update ? "update" : "add", update ? "Update a comment." : "Add a comment.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var id = CommandUtilities.Argument<string>("comment-id", "Comment ID.");
        var content = new ContentOptions();
        command.Arguments.Add(issue);
        if (update) command.Arguments.Add(id);
        content.AddTo(command, required: true);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var key = parse.GetValue(issue)!;
            new SafetyPolicy().EnsureMutationAllowed(context.Safety, $"comment {(update ? "update" : "add")} {key}");
            var (document, warnings) = await content.ReadAsync(parse, parse.GetValue(globals.TokenStdin), token);
            foreach (var warning in warnings) context.Output.Warning(warning);
            var request = new JsonObject { ["body"] = document };
            var path = update
                ? $"/rest/api/3/issue/{Uri.EscapeDataString(key)}/comment/{Uri.EscapeDataString(parse.GetValue(id)!)}"
                : $"/rest/api/3/issue/{Uri.EscapeDataString(key)}/comment";
            if (context.Safety.DryRun)
            {
                return context.Output.Success(CommandUtilities.Preview(update ? "PUT" : "POST", path, request), "Dry run only; Jira did not validate or write this comment.");
            }
            var response = update
                ? await context.Client.UpdateCommentAsync(key, parse.GetValue(id)!, request, token)
                : await context.Client.AddCommentAsync(key, request, token);
            return context.Output.Success(new { result = response.Body, verification = "confirmed-by-write-response" }, update ? "Comment updated." : "Comment added.");
        }));
        return command;
    }

    private static Command BuildDelete(CliRuntime runtime)
    {
        var command = new Command("delete", "Delete a comment with exact-target confirmation.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var id = CommandUtilities.Argument<string>("comment-id", "Comment ID.");
        var confirm = CommandUtilities.Option<string?>("--confirm", "Exact confirmation in ISSUE/comment/ID form.");
        command.Arguments.Add(issue);
        command.Arguments.Add(id);
        command.Options.Add(confirm);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var target = $"{parse.GetValue(issue)!}/comment/{parse.GetValue(id)!}";
            await new SafetyPolicy().ConfirmDeletionAsync(target, parse.GetValue(confirm), false, null, context.Safety, Console.In, Console.Error, token);
            if (context.Safety.DryRun)
            {
                return context.Output.Success(CommandUtilities.Preview("DELETE", $"/rest/api/3/issue/{Uri.EscapeDataString(parse.GetValue(issue)!)}/comment/{Uri.EscapeDataString(parse.GetValue(id)!)}", null), "Dry run only; the comment was not deleted.");
            }
            await context.Client.DeleteCommentAsync(parse.GetValue(issue)!, parse.GetValue(id)!, token);
            return context.Output.Success(new { target, deleted = true }, "Comment deleted.");
        }));
        return command;
    }
}
