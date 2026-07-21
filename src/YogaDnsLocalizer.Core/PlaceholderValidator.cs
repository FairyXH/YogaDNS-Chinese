using System.Text.RegularExpressions;

namespace YogaDnsLocalizer.Core;

public static class PlaceholderValidator
{
    private static readonly Regex TokenPattern = new(@"%(?:\d+|[A-Za-z]+)", RegexOptions.Compiled);

    public static ValidationResult Validate(string original, string translation)
    {
        var expected = TokenPattern.Matches(original).Select(m => m.Value).ToArray();
        var actual = TokenPattern.Matches(translation).Select(m => m.Value).ToArray();
        if (expected.SequenceEqual(actual))
        {
            return new ValidationResult(true, "占位符完整");
        }

        var missing = expected.Except(actual).Distinct().ToArray();
        var extra = actual.Except(expected).Distinct().ToArray();
        var detail = string.Join("；", new[]
        {
            missing.Length > 0 ? $"缺少 {string.Join(", ", missing)}" : "",
            extra.Length > 0 ? $"多出 {string.Join(", ", extra)}" : ""
        }.Where(x => x.Length > 0));
        return new ValidationResult(false, $"占位符不一致：{detail}");
    }
}

public sealed record ValidationResult(bool IsValid, string Message);
