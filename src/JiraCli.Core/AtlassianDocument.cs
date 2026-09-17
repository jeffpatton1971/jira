using System.Text.Json.Nodes;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace JiraCli.Core;

public sealed record AdfConversionResult(JsonObject Document, IReadOnlyList<string> Warnings);

public static class AtlassianDocument
{
    public static JsonObject Parse(string json)
    {
        try
        {
            var document = JsonNode.Parse(json) as JsonObject;
            if (document?["type"]?.GetValue<string>() != "doc" || document["version"]?.GetValue<int>() != 1)
            {
                throw new JiraCliException("invalid_adf", "ADF input must be a version 1 document object.", CliExitCode.UsageOrConfiguration);
            }
            return document;
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new JiraCliException("invalid_adf", "ADF input is not valid JSON.", CliExitCode.UsageOrConfiguration, innerException: exception);
        }
    }

    public static JsonObject FromPlainText(string text)
    {
        var blocks = new JsonArray();
        foreach (var paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split("\n\n"))
        {
            var content = new JsonArray();
            var lines = paragraph.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].Length > 0)
                {
                    content.Add(TextNode(lines[index]));
                }
                if (index < lines.Length - 1)
                {
                    content.Add(new JsonObject { ["type"] = "hardBreak" });
                }
            }
            blocks.Add(new JsonObject { ["type"] = "paragraph", ["content"] = content });
        }
        return Document(blocks);
    }

    public static AdfConversionResult FromMarkdown(string markdown)
    {
        var parsed = Markdown.Parse(markdown, new MarkdownPipelineBuilder().UseEmphasisExtras().Build());
        var warnings = new List<string>();
        var blocks = ConvertBlocks(parsed, warnings);
        return new AdfConversionResult(Document(blocks), warnings);
    }

    private static JsonArray ConvertBlocks(ContainerBlock container, ICollection<string> warnings)
    {
        var output = new JsonArray();
        foreach (var block in container)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    output.Add(new JsonObject
                    {
                        ["type"] = "heading",
                        ["attrs"] = new JsonObject { ["level"] = Math.Clamp(heading.Level, 1, 6) },
                        ["content"] = ConvertInlines(heading.Inline)
                    });
                    break;
                case ParagraphBlock paragraph:
                    output.Add(new JsonObject { ["type"] = "paragraph", ["content"] = ConvertInlines(paragraph.Inline) });
                    break;
                case FencedCodeBlock code:
                    var codeBlock = new JsonObject
                    {
                        ["type"] = "codeBlock",
                        ["content"] = new JsonArray(TextNode(code.Lines.ToString()))
                    };
                    if (!string.IsNullOrWhiteSpace(code.Info))
                    {
                        codeBlock["attrs"] = new JsonObject { ["language"] = code.Info.ToString() };
                    }
                    output.Add(codeBlock);
                    break;
                case QuoteBlock quote:
                    output.Add(new JsonObject { ["type"] = "blockquote", ["content"] = ConvertBlocks(quote, warnings) });
                    break;
                case ListBlock list:
                    var items = new JsonArray();
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        items.Add(new JsonObject { ["type"] = "listItem", ["content"] = ConvertBlocks(item, warnings) });
                    }
                    var listNode = new JsonObject
                    {
                        ["type"] = list.IsOrdered ? "orderedList" : "bulletList",
                        ["content"] = items
                    };
                    if (list.IsOrdered)
                    {
                        listNode["attrs"] = new JsonObject { ["order"] = list.OrderedStart };
                    }
                    output.Add(listNode);
                    break;
                case ThematicBreakBlock:
                    output.Add(new JsonObject { ["type"] = "rule" });
                    break;
                default:
                    warnings.Add($"Markdown block '{block.GetType().Name}' was converted to plain text.");
                    output.Add(new JsonObject
                    {
                        ["type"] = "paragraph",
                        ["content"] = new JsonArray(TextNode(block.ToString() ?? string.Empty))
                    });
                    break;
            }
        }
        return output;
    }

    private static JsonArray ConvertInlines(ContainerInline? container, IReadOnlyList<JsonObject>? inheritedMarks = null)
    {
        var output = new JsonArray();
        for (var inline = container?.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    output.Add(TextNode(literal.Content.ToString(), inheritedMarks));
                    break;
                case CodeInline code:
                    output.Add(TextNode(code.Content, AddMark(inheritedMarks, new JsonObject { ["type"] = "code" })));
                    break;
                case LineBreakInline:
                    output.Add(new JsonObject { ["type"] = "hardBreak" });
                    break;
                case EmphasisInline emphasis:
                    var mark = emphasis.DelimiterCount >= 2 ? "strong" : "em";
                    Append(output, ConvertInlines(emphasis, AddMark(inheritedMarks, new JsonObject { ["type"] = mark })));
                    break;
                case LinkInline link when !link.IsImage && Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http":
                    Append(output, ConvertInlines(link, AddMark(inheritedMarks, new JsonObject
                    {
                        ["type"] = "link",
                        ["attrs"] = new JsonObject { ["href"] = uri.AbsoluteUri }
                    })));
                    break;
                case ContainerInline nested:
                    Append(output, ConvertInlines(nested, inheritedMarks));
                    break;
                default:
                    output.Add(TextNode(inline.ToString() ?? string.Empty, inheritedMarks));
                    break;
            }
        }
        return output;
    }

    private static JsonObject Document(JsonArray blocks) => new()
    {
        ["version"] = 1,
        ["type"] = "doc",
        ["content"] = blocks
    };

    private static JsonObject TextNode(string value, IReadOnlyList<JsonObject>? marks = null)
    {
        var node = new JsonObject { ["type"] = "text", ["text"] = value };
        if (marks is { Count: > 0 })
        {
            node["marks"] = new JsonArray(marks.Select(mark => mark.DeepClone()).ToArray());
        }
        return node;
    }

    private static IReadOnlyList<JsonObject> AddMark(IReadOnlyList<JsonObject>? marks, JsonObject mark)
    {
        var result = marks?.ToList() ?? [];
        result.Add(mark);
        return result;
    }

    private static void Append(JsonArray target, JsonArray source)
    {
        foreach (var item in source.ToArray())
        {
            target.Add(item?.DeepClone());
        }
    }
}
