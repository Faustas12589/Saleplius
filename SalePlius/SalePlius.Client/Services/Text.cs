using System.Globalization;
using System.Text;

namespace SalePlius.Client.Services;

/// <summary>Accent- and case-insensitive text matching ("Krūtinė" == "krutine").</summary>
public static class Text
{
    public static string Fold(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().ToLowerInvariant().Trim();
    }

    public static bool SameLoose(string? a, string? b) => Fold(a) == Fold(b);

    /// <summary>Lithuanian plural: Plural(1, "pratimas", "pratimai", "pratimų") → "1 pratimas".</summary>
    public static string Plural(int n, string one, string few, string many)
    {
        var mod10 = n % 10;
        var mod100 = n % 100;
        var word = mod10 == 1 && mod100 != 11 ? one
            : mod10 >= 2 && (mod100 < 11 || mod100 > 19) ? few
            : many;
        return $"{n} {word}";
    }

    public static bool ContainsLoose(string? haystack, string? needle) => Fold(haystack).Contains(Fold(needle));
}
