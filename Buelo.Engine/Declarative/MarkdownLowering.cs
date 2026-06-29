using System.Text.RegularExpressions;
using Buelo.Engine.Ir;

namespace Buelo.Engine.Declarative;

/// <summary>
/// Lowers a Markdown subset into IR text nodes (blueprint §4 — "HTML" = formatted content block,
/// Markdown leads). Supports headings (<c># … ######</c>), unordered lists (<c>- </c>/<c>* </c>),
/// paragraphs, and inline <c>**bold**</c> / <c>*italic*</c>. QuestPDF renders no HTML natively, so
/// rich content is transpiled to runs here.
/// </summary>
public static partial class MarkdownLowering
{
    [GeneratedRegex(@"\*\*(?<b>.+?)\*\*|\*(?<i>.+?)\*")]
    private static partial Regex InlineRegex();

    private static readonly float[] HeadingSizes = [22, 18, 16, 14, 13, 12];

    public static IReadOnlyList<Node> Parse(string markdown)
    {
        var nodes = new List<Node>();
        var paragraph = new List<string>();

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
                return;
            nodes.Add(new TextNode { Runs = InlineRuns(string.Join(' ', paragraph), null) });
            paragraph.Clear();
        }

        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                continue;
            }

            var heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                FlushParagraph();
                var level = Math.Min(heading.Groups[1].Value.Length, HeadingSizes.Length);
                var style = new Style { Size = HeadingSizes[level - 1], Bold = true };
                nodes.Add(new TextNode { Style = style, Runs = InlineRuns(heading.Groups[2].Value, style.Size) });
                continue;
            }

            var bullet = BulletRegex().Match(line);
            if (bullet.Success)
            {
                FlushParagraph();
                var runs = new List<Run> { new() { Text = "•  " } };
                runs.AddRange(InlineRuns(bullet.Groups[1].Value, null));
                nodes.Add(new TextNode { Runs = runs });
                continue;
            }

            paragraph.Add(line.Trim());
        }

        FlushParagraph();
        return nodes;
    }

    private static List<Run> InlineRuns(string text, float? size)
    {
        var runs = new List<Run>();
        var last = 0;

        foreach (Match match in InlineRegex().Matches(text))
        {
            if (match.Index > last)
                runs.Add(MakeRun(text[last..match.Index], size, bold: false, italic: false));

            if (match.Groups["b"].Success)
                runs.Add(MakeRun(match.Groups["b"].Value, size, bold: true, italic: false));
            else
                runs.Add(MakeRun(match.Groups["i"].Value, size, bold: false, italic: true));

            last = match.Index + match.Length;
        }

        if (last < text.Length)
            runs.Add(MakeRun(text[last..], size, bold: false, italic: false));

        if (runs.Count == 0)
            runs.Add(MakeRun(text, size, bold: false, italic: false));

        return runs;
    }

    private static Run MakeRun(string text, float? size, bool bold, bool italic) => new()
    {
        Text = text,
        Style = new Style { Size = size, Bold = bold ? true : null, Italic = italic ? true : null },
    };

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^[-*]\s+(.*)$")]
    private static partial Regex BulletRegex();
}
