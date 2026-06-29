using System.Text.RegularExpressions;
using Buelo.Engine.Ir;

namespace Buelo.Engine.Declarative;

/// <summary>
/// Lowers an HTML subset into IR text nodes (blueprint §4 — secondary rich-content format, sibling of
/// Markdown). Blocks: <c>&lt;h1&gt;…&lt;h6&gt;</c>, <c>&lt;p&gt;</c>, <c>&lt;ul&gt;&lt;li&gt;</c>; inline:
/// <c>&lt;b&gt;/&lt;strong&gt;</c>, <c>&lt;i&gt;/&lt;em&gt;</c>. QuestPDF renders no HTML natively, so it's
/// transpiled to runs here. Unknown tags are stripped.
/// </summary>
public static partial class HtmlLowering
{
    private static readonly float[] HeadingSizes = [22, 18, 16, 14, 13, 12];

    [GeneratedRegex(@"<(?<tag>h[1-6]|p|ul)\b[^>]*>(?<inner>.*?)</\k<tag>>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BlockRegex();

    [GeneratedRegex(@"<li\b[^>]*>(.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"<(b|strong)\b[^>]*>(.*?)</\1>|<(i|em)\b[^>]*>(.*?)</\3>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    public static IReadOnlyList<Node> Parse(string html)
    {
        var text = html.Replace("\r", " ").Replace("\n", " ");
        var nodes = new List<Node>();

        foreach (Match block in BlockRegex().Matches(text))
        {
            var tag = block.Groups["tag"].Value.ToLowerInvariant();
            var inner = block.Groups["inner"].Value;

            if (tag == "ul")
            {
                foreach (Match item in ListItemRegex().Matches(inner))
                {
                    var runs = new List<Run> { new() { Text = "•  " } };
                    runs.AddRange(InlineRuns(item.Groups[1].Value, null));
                    nodes.Add(new TextNode { Runs = runs });
                }
            }
            else if (tag == "p")
            {
                nodes.Add(new TextNode { Runs = InlineRuns(inner, null) });
            }
            else // h1..h6
            {
                var level = Math.Clamp(tag[1] - '0', 1, HeadingSizes.Length);
                var style = new Style { Size = HeadingSizes[level - 1], Bold = true };
                nodes.Add(new TextNode { Style = style, Runs = InlineRuns(inner, style.Size) });
            }
        }

        // Fallback: no recognized block elements → treat the whole thing as one paragraph.
        if (nodes.Count == 0)
            nodes.Add(new TextNode { Runs = InlineRuns(text, null) });

        return nodes;
    }

    private static List<Run> InlineRuns(string html, float? size)
    {
        var runs = new List<Run>();
        var last = 0;

        foreach (Match match in InlineRegex().Matches(html))
        {
            if (match.Index > last)
                AddPlain(runs, html[last..match.Index], size);

            var bold = match.Groups[1].Success;
            var content = bold ? match.Groups[2].Value : match.Groups[4].Value;
            runs.Add(MakeRun(Clean(content), size, bold: bold, italic: !bold));
            last = match.Index + match.Length;
        }

        if (last < html.Length)
            AddPlain(runs, html[last..], size);

        if (runs.Count == 0)
            runs.Add(MakeRun(string.Empty, size, bold: false, italic: false));

        return runs;
    }

    private static void AddPlain(List<Run> runs, string segment, float? size)
    {
        var clean = Clean(segment);
        if (clean.Length > 0)
            runs.Add(MakeRun(clean, size, bold: false, italic: false));
    }

    private static Run MakeRun(string text, float? size, bool bold, bool italic) => new()
    {
        Text = text,
        Style = new Style { Size = size, Bold = bold ? true : null, Italic = italic ? true : null },
    };

    /// <summary>Strips remaining tags and decodes the common HTML entities.</summary>
    private static string Clean(string html) => TagRegex().Replace(html, string.Empty)
        .Replace("&amp;", "&")
        .Replace("&lt;", "<")
        .Replace("&gt;", ">")
        .Replace("&quot;", "\"")
        .Replace("&#39;", "'")
        .Replace("&nbsp;", " ");
}
