using SheraBoard.Core.Models;

namespace SheraBoard.Core.Persistence;

public sealed record ClipboardSearchSpec(
    IReadOnlyList<string> SearchTerms,
    ClipboardKind? Kind,
    string? SourceApp,
    bool PinnedOnly,
    IReadOnlySet<ClipboardContentFeature> Features)
{
    public string? SearchText => SearchTerms.Count == 0 ? null : string.Join(' ', SearchTerms);
}

public static class ClipboardSearchParser
{
    public static ClipboardSearchSpec Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Empty();
        }

        var terms = new List<string>();
        var features = new HashSet<ClipboardContentFeature>();
        ClipboardKind? kind = null;
        string? sourceApp = null;
        var pinnedOnly = false;

        foreach (var token in Tokenize(input))
        {
            var separator = token.IndexOf(':');
            if (separator <= 0 || separator == token.Length - 1)
            {
                terms.Add(token);
                continue;
            }

            var key = token[..separator].Trim().ToLowerInvariant();
            var value = token[(separator + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (key)
            {
                case "app":
                case "source":
                case "process":
                    sourceApp = value;
                    break;
                case "type":
                case "kind":
                    if (TryParseKind(value, out var parsedKind))
                    {
                        kind = parsedKind;
                    }
                    else
                    {
                        terms.Add(token);
                    }

                    break;
                case "has":
                    if (TryParseFeature(value, out var feature))
                    {
                        features.Add(feature);
                    }
                    else
                    {
                        terms.Add(token);
                    }

                    break;
                case "is":
                    if (value.Equals("pinned", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("pin", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("fixed", StringComparison.OrdinalIgnoreCase))
                    {
                        pinnedOnly = true;
                    }
                    else
                    {
                        terms.Add(token);
                    }

                    break;
                default:
                    terms.Add(token);
                    break;
            }
        }

        return new ClipboardSearchSpec(terms, kind, sourceApp, pinnedOnly, features);
    }

    private static ClipboardSearchSpec Empty()
    {
        return new ClipboardSearchSpec([], null, null, false, new HashSet<ClipboardContentFeature>());
    }

    private static bool TryParseKind(string value, out ClipboardKind kind)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "text":
            case "txt":
            case "plain":
            case "文字":
                kind = ClipboardKind.Text;
                return true;
            case "rich":
            case "rtf":
            case "html":
            case "table":
            case "富文本":
            case "表格":
                kind = ClipboardKind.RichText;
                return true;
            case "image":
            case "img":
            case "png":
            case "pic":
            case "图片":
                kind = ClipboardKind.Image;
                return true;
            case "file":
            case "files":
            case "folder":
            case "path":
            case "文件":
                kind = ClipboardKind.FileList;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool TryParseFeature(string value, out ClipboardContentFeature feature)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "url":
            case "link":
            case "links":
            case "网址":
            case "链接":
                feature = ClipboardContentFeature.Url;
                return true;
            case "code":
            case "source":
            case "snippet":
            case "代码":
                feature = ClipboardContentFeature.Code;
                return true;
            default:
                feature = default;
                return false;
        }
    }

    private static IEnumerable<string> Tokenize(string input)
    {
        var token = new List<char>();
        var inQuotes = false;

        foreach (var character in input.Trim())
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (token.Count > 0)
                {
                    yield return new string(token.ToArray());
                    token.Clear();
                }

                continue;
            }

            token.Add(character);
        }

        if (token.Count > 0)
        {
            yield return new string(token.ToArray());
        }
    }
}
