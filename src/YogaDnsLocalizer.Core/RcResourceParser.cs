using System.Text.RegularExpressions;

namespace YogaDnsLocalizer.Core;

public static class RcResourceParser
{
    private static readonly Regex QuotedText = new("\\\"((?:\\\\.|[^\\\"\\\\])*)\\\"", RegexOptions.Compiled);
    private static readonly Regex IdPattern = new("(?:,|\\s)(-?\\d+)\\s*(?:,|$)", RegexOptions.Compiled);

    public static IReadOnlyList<ResourceEntry> Parse(ResourceType type, int resourceId, string rcText)
    {
        var result = new List<ResourceEntry>();
        foreach (var line in rcText.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var match = QuotedText.Match(line);
            if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                continue;
            }

            var original = Unescape(match.Groups[1].Value);
            var id = 0;
            var idMatches = IdPattern.Matches(line);
            if (idMatches.Count > 0 && int.TryParse(idMatches[^1].Groups[1].Value, out var parsed))
            {
                id = parsed;
            }
            result.Add(new ResourceEntry(type, resourceId, original, line, id, line.TrimStart().Split(' ', '\t')[0]));
        }
        return result;
    }

    public static string ApplyTranslations(string rcText, IEnumerable<ResourceEntry> entries, TranslationCatalog catalog)
    {
        var translations = entries
            .Select(entry => (entry, item: catalog.Find(entry)))
            .Where(pair => pair.item is not null && !string.IsNullOrWhiteSpace(pair.item.Translation))
            .GroupBy(pair => pair.entry.Original, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().item!.Translation, StringComparer.Ordinal);

        return QuotedText.Replace(rcText, match =>
        {
            var original = Unescape(match.Groups[1].Value);
            if (!translations.TryGetValue(original, out var translation))
            {
                return match.Value;
            }
            var validation = PlaceholderValidator.Validate(original, translation);
            if (!validation.IsValid)
            {
                throw new InvalidDataException($"{original}：{validation.Message}");
            }
            return $"\"{Escape(translation)}\"";
        });
    }

    private static string Unescape(string value) => value.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\"", "\\\"");
}
