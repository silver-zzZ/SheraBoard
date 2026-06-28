using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaFontFamily = System.Windows.Media.FontFamily;

namespace SheraBoard.App.Services;

public static partial class RichPreviewDocumentBuilder
{
    private const double BaseFontSize = 12.5;
    private const double CodeFontSize = 12.2;
    private const double MinPreviewFontSize = 10.5;
    private const double MaxPreviewFontSize = 21;

    public static FlowDocument FromPlainText(string text)
    {
        var document = CreateDocument();
        var paragraph = CreateParagraph();
        AddText(paragraph, text, InlineStyle.Default, preserveWhitespace: true);
        document.Blocks.Add(paragraph);
        return document;
    }

    public static FlowDocument FromHtml(string html)
    {
        var fragment = ExtractHtmlFragment(html);
        var document = CreateDocument();
        var context = new HtmlBuildContext(document);

        foreach (Match match in HtmlTokenRegex().Matches(fragment))
        {
            var token = match.Value;
            if (token.StartsWith('<'))
            {
                HandleTag(token, context);
                continue;
            }

            if (context.SkipDepth > 0)
            {
                continue;
            }

            AddText(context.CurrentParagraph, WebUtility.HtmlDecode(token), context.CurrentStyle, context.CurrentStyle.PreserveWhitespace);
        }

        context.CommitParagraph();
        if (document.Blocks.Count == 0)
        {
            document.Blocks.Add(CreateParagraph("无可预览内容"));
        }

        return document;
    }

    public static FlowDocument FromRtf(string rtf)
    {
        var document = CreateDocument();
        var range = new TextRange(document.ContentStart, document.ContentEnd);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(NormalizeRtfFontSizes(rtf)));
        range.Load(stream, System.Windows.DataFormats.Rtf);
        NormalizeDocument(document);
        return document;
    }

    private static FlowDocument CreateDocument()
    {
        return new FlowDocument
        {
            FontFamily = new MediaFontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = BaseFontSize,
            Foreground = new SolidColorBrush(MediaColor.FromRgb(17, 24, 39)),
            PagePadding = new Thickness(0),
            LineHeight = 18.5,
            ColumnWidth = 560
        };
    }

    private static Paragraph CreateParagraph(string? text = null)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 6),
            LineHeight = 18.5
        };
        if (!string.IsNullOrEmpty(text))
        {
            paragraph.Inlines.Add(new Run(text));
        }

        return paragraph;
    }

    private static void HandleTag(string token, HtmlBuildContext context)
    {
        var tag = ParseTag(token);
        if (tag is null)
        {
            return;
        }

        if (tag.Name is "script" or "style")
        {
            if (tag.IsClosing)
            {
                context.SkipDepth = Math.Max(0, context.SkipDepth - 1);
            }
            else if (!tag.IsSelfClosing)
            {
                context.SkipDepth++;
            }

            return;
        }

        if (context.SkipDepth > 0)
        {
            return;
        }

        if (tag.IsClosing)
        {
            if (tag.Name is "p" or "div" or "section" or "article" or "blockquote" or "pre" or "li" or "tr" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
            {
                context.CommitParagraph();
            }

            context.PopStyle();
            return;
        }

        if (tag.Name == "br")
        {
            context.CurrentParagraph.Inlines.Add(new LineBreak());
            return;
        }

        if (tag.IsBlock)
        {
            context.StartBlock();
        }

        context.PushStyle(ApplyTagStyle(context.CurrentStyle, tag));

        if (tag.Name == "li")
        {
            context.CurrentParagraph.Inlines.Add(new Run("• ")
            {
                Foreground = new SolidColorBrush(MediaColor.FromRgb(100, 116, 139)),
                FontWeight = FontWeights.SemiBold
            });
        }

        if (tag.IsSelfClosing)
        {
            context.PopStyle();
        }
    }

    private static InlineStyle ApplyTagStyle(InlineStyle current, HtmlTag tag)
    {
        var style = current;
        style = tag.Name switch
        {
            "strong" or "b" => style with { FontWeight = FontWeights.SemiBold },
            "em" or "i" => style with { FontStyle = FontStyles.Italic },
            "u" => style with { TextDecorations = TextDecorations.Underline },
            "s" or "strike" => style with { TextDecorations = TextDecorations.Strikethrough },
            "mark" => style with { Background = new SolidColorBrush(MediaColor.FromRgb(254, 243, 199)) },
            "a" => style with
            {
                Foreground = new SolidColorBrush(MediaColor.FromRgb(37, 99, 235)),
                TextDecorations = TextDecorations.Underline
            },
            "code" => style with
            {
                FontFamily = new MediaFontFamily("Cascadia Mono, Consolas"),
                FontSize = CodeFontSize,
                PreserveWhitespace = true
            },
            "pre" => style with
            {
                FontFamily = new MediaFontFamily("Cascadia Mono, Consolas"),
                FontSize = CodeFontSize,
                PreserveWhitespace = true
            },
            "h1" => style with { FontWeight = FontWeights.SemiBold, FontSize = 20 },
            "h2" => style with { FontWeight = FontWeights.SemiBold, FontSize = 17 },
            "h3" or "h4" => style with { FontWeight = FontWeights.SemiBold, FontSize = 15 },
            "h5" or "h6" => style with { FontWeight = FontWeights.SemiBold, FontSize = 13.5 },
            _ => style
        };

        if (tag.Attributes.TryGetValue("color", out var color))
        {
            style = style with { Foreground = TryCreateBrush(color) ?? style.Foreground };
        }

        if (tag.Name == "font" && tag.Attributes.TryGetValue("size", out var fontSize) && double.TryParse(fontSize, NumberStyles.Any, CultureInfo.InvariantCulture, out var sizeValue))
        {
            style = style with { FontSize = Math.Clamp(BaseFontSize + (sizeValue - 3) * 1.6, MinPreviewFontSize, MaxPreviewFontSize) };
        }

        if (tag.Attributes.TryGetValue("style", out var inlineCss))
        {
            style = ApplyCssStyle(style, inlineCss);
        }

        return style;
    }

    private static InlineStyle ApplyCssStyle(InlineStyle style, string inlineCss)
    {
        foreach (var declaration in inlineCss.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = declaration.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            var property = parts[0].ToLowerInvariant();
            var value = parts[1];
            switch (property)
            {
                case "color":
                    style = style with { Foreground = TryCreateBrush(value) ?? style.Foreground };
                    break;
                case "background":
                case "background-color":
                    style = style with { Background = TryCreateBrush(value) ?? style.Background };
                    break;
                case "font-size":
                    style = style with { FontSize = NormalizeCssFontSize(value) ?? style.FontSize };
                    break;
                case "font-family":
                    style = style with { FontFamily = new MediaFontFamily(CleanFontFamily(value)) };
                    break;
                case "font-weight":
                    if (value.Contains("bold", StringComparison.OrdinalIgnoreCase) ||
                        (int.TryParse(value, out var weight) && weight >= 600))
                    {
                        style = style with { FontWeight = FontWeights.SemiBold };
                    }
                    break;
                case "font-style":
                    if (value.Contains("italic", StringComparison.OrdinalIgnoreCase))
                    {
                        style = style with { FontStyle = FontStyles.Italic };
                    }
                    break;
                case "text-decoration":
                    if (value.Contains("underline", StringComparison.OrdinalIgnoreCase))
                    {
                        style = style with { TextDecorations = TextDecorations.Underline };
                    }
                    else if (value.Contains("line-through", StringComparison.OrdinalIgnoreCase))
                    {
                        style = style with { TextDecorations = TextDecorations.Strikethrough };
                    }
                    break;
                case "white-space":
                    if (value.Contains("pre", StringComparison.OrdinalIgnoreCase))
                    {
                        style = style with { PreserveWhitespace = true };
                    }
                    break;
            }
        }

        return style;
    }

    private static void AddText(Paragraph paragraph, string text, InlineStyle style, bool preserveWhitespace)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var normalized = preserveWhitespace
            ? text.Replace("\r\n", "\n").Replace('\r', '\n')
            : WhitespaceRegex().Replace(text, " ");

        if (!preserveWhitespace && string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var lines = normalized.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            if (lines[i].Length == 0)
            {
                continue;
            }

            paragraph.Inlines.Add(new Run(lines[i])
            {
                FontWeight = style.FontWeight,
                FontStyle = style.FontStyle,
                FontFamily = style.FontFamily,
                FontSize = style.FontSize,
                Foreground = style.Foreground,
                Background = style.Background,
                TextDecorations = style.TextDecorations
            });
        }
    }

    private static HtmlTag? ParseTag(string token)
    {
        var match = TagNameRegex().Match(token);
        if (!match.Success)
        {
            return null;
        }

        var name = match.Groups["name"].Value.ToLowerInvariant();
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match attrMatch in AttributeRegex().Matches(token))
        {
            var value = attrMatch.Groups["dq"].Success ? attrMatch.Groups["dq"].Value :
                attrMatch.Groups["sq"].Success ? attrMatch.Groups["sq"].Value :
                attrMatch.Groups["bare"].Value;
            attributes[attrMatch.Groups["name"].Value] = WebUtility.HtmlDecode(value);
        }

        return new HtmlTag(
            name,
            token.StartsWith("</", StringComparison.Ordinal),
            token.EndsWith("/>", StringComparison.Ordinal) || name is "br" or "img" or "hr" or "meta" or "link",
            attributes);
    }

    private static SolidColorBrush? TryCreateBrush(string value)
    {
        value = value.Trim().Trim('"', '\'');
        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var numbers = NumberRegex().Matches(value)
                .Select(match => int.TryParse(match.Value, CultureInfo.InvariantCulture, out var number) ? Math.Clamp(number, 0, 255) : 0)
                .Take(3)
                .ToArray();
            if (numbers.Length == 3)
            {
                return new SolidColorBrush(MediaColor.FromRgb((byte)numbers[0], (byte)numbers[1], (byte)numbers[2]));
            }
        }

        try
        {
            return MediaColorConverter.ConvertFromString(value) is MediaColor color
                ? new SolidColorBrush(color)
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static double? NormalizeCssFontSize(string value)
    {
        var match = CssFontSizeRegex().Match(value);
        if (!match.Success || !double.TryParse(match.Groups["value"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var rawValue))
        {
            return null;
        }

        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        var points = unit switch
        {
            "pt" => rawValue,
            "em" or "rem" => rawValue * BaseFontSize,
            "%" => BaseFontSize * rawValue / 100,
            _ => rawValue * 0.75
        };

        // Compress extreme source sizes: preserve relative big/small feeling,
        // but avoid huge clipboard fonts dominating the small hover panel.
        var normalized = BaseFontSize + (points - 12) * 0.45;
        return Math.Clamp(normalized, MinPreviewFontSize, MaxPreviewFontSize);
    }

    private static string NormalizeRtfFontSizes(string rtf)
    {
        return RtfFontSizeRegex().Replace(rtf, match =>
        {
            if (!int.TryParse(match.Groups["size"].Value, out var halfPoints))
            {
                return match.Value;
            }

            var points = halfPoints / 2.0;
            var normalized = BaseFontSize + (points - 12) * 0.45;
            var normalizedHalfPoints = (int)Math.Round(Math.Clamp(normalized, MinPreviewFontSize, MaxPreviewFontSize) * 2);
            return $@"\fs{normalizedHalfPoints}";
        });
    }

    private static string CleanFontFamily(string value)
    {
        var family = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "Segoe UI";
        return family.Trim('"', '\'');
    }

    private static string ExtractHtmlFragment(string html)
    {
        const string startMarker = "<!--StartFragment-->";
        const string endMarker = "<!--EndFragment-->";

        var start = html.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return html;
        }

        start += startMarker.Length;
        var end = html.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
        return end > start ? html[start..end] : html[start..];
    }

    private static void NormalizeDocument(FlowDocument document)
    {
        document.PagePadding = new Thickness(0);
        document.FontSize = BaseFontSize;
        document.LineHeight = 18.5;
        foreach (var block in document.Blocks.OfType<Paragraph>())
        {
            block.Margin = new Thickness(0, 0, 0, 6);
            block.LineHeight = 18.5;
        }
    }

    [GeneratedRegex("(<[^>]+>|[^<]+)", RegexOptions.Singleline)]
    private static partial Regex HtmlTokenRegex();

    [GeneratedRegex(@"^<\s*/?\s*(?<name>[a-zA-Z0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex TagNameRegex();

    [GeneratedRegex(@"(?<name>[a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<bare>[^\s>]+))", RegexOptions.IgnoreCase)]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?<value>-?\d+(?:\.\d+)?)\s*(?<unit>px|pt|em|rem|%)?", RegexOptions.IgnoreCase)]
    private static partial Regex CssFontSizeRegex();

    [GeneratedRegex(@"\\fs(?<size>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex RtfFontSizeRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberRegex();

    private sealed class HtmlBuildContext
    {
        private readonly FlowDocument _document;
        private readonly Stack<InlineStyle> _styles = new();
        private Paragraph? _paragraph;

        public HtmlBuildContext(FlowDocument document)
        {
            _document = document;
        }

        public int SkipDepth { get; set; }

        public InlineStyle CurrentStyle => _styles.Count > 0 ? _styles.Peek() : InlineStyle.Default;

        public Paragraph CurrentParagraph
        {
            get
            {
                _paragraph ??= CreateParagraph();
                return _paragraph;
            }
        }

        public void PushStyle(InlineStyle style)
        {
            _styles.Push(style);
        }

        public void PopStyle()
        {
            if (_styles.Count > 0)
            {
                _styles.Pop();
            }
        }

        public void StartBlock()
        {
            if (_paragraph is { Inlines.Count: > 0 })
            {
                CommitParagraph();
            }
        }

        public void CommitParagraph()
        {
            if (_paragraph is not null && _paragraph.Inlines.Count > 0)
            {
                _document.Blocks.Add(_paragraph);
            }

            _paragraph = null;
        }
    }

    private sealed record HtmlTag(
        string Name,
        bool IsClosing,
        bool IsSelfClosing,
        IReadOnlyDictionary<string, string> Attributes)
    {
        public bool IsBlock => Name is "p" or "div" or "section" or "article" or "blockquote" or "pre" or "li" or "tr" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
    }

    private sealed record InlineStyle(
        FontWeight FontWeight,
        System.Windows.FontStyle FontStyle,
        TextDecorationCollection? TextDecorations,
        MediaFontFamily FontFamily,
        double FontSize,
        System.Windows.Media.Brush? Foreground,
        System.Windows.Media.Brush? Background,
        bool PreserveWhitespace)
    {
        public static InlineStyle Default { get; } = new(
            FontWeights.Normal,
            FontStyles.Normal,
            null,
            new MediaFontFamily("Segoe UI Variable Text, Segoe UI"),
            BaseFontSize,
            new SolidColorBrush(MediaColor.FromRgb(17, 24, 39)),
            null,
            false);
    }
}

