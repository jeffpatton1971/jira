using System.Text.Json.Nodes;
using JiraCli.Core;
using JiraCli.JiraCloud;

namespace JiraCli.Tests;

public sealed class ContentAndLookupTests
{
    [Fact]
    public void Plain_text_and_markdown_create_adf()
    {
        var plain = AtlassianDocument.FromPlainText("first\nsecond");
        Assert.Equal("doc", plain["type"]!.GetValue<string>());
        Assert.Equal("hardBreak", plain["content"]![0]!["content"]![1]!["type"]!.GetValue<string>());

        var markdown = AtlassianDocument.FromMarkdown("# Heading\n\n**bold** and [link](https://example.com)");
        Assert.Equal("heading", markdown.Document["content"]![0]!["type"]!.GetValue<string>());
        Assert.Contains("strong", markdown.Document.ToJsonString(), StringComparison.Ordinal);
        Assert.Contains("link", markdown.Document.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Transition_names_must_be_unambiguous()
    {
        var response = JsonNode.Parse("""{"transitions":[{"id":"1","name":"Done"},{"id":"2","name":"Done"}]}""");
        var exception = Assert.Throws<JiraCliException>(() => JiraLookupResolver.ResolveTransition(response, "Done"));
        Assert.Equal(CliExitCode.AmbiguousLookup, exception.ExitCode);
        Assert.Equal("2", JiraLookupResolver.ResolveTransition(response, "2")["id"]!.GetValue<string>());
    }

    [Fact]
    public void User_names_must_be_unambiguous()
    {
        var response = JsonNode.Parse("""[{"accountId":"a","displayName":"Alex"},{"accountId":"b","displayName":"Alex"}]""");
        var exception = Assert.Throws<JiraCliException>(() => JiraLookupResolver.ResolveAccountId(response, "Alex"));
        Assert.Equal("ambiguous_user", exception.ErrorCode);
        Assert.Equal("a", JiraLookupResolver.ResolveAccountId(response, "a"));
    }
}
