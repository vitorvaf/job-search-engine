using System.Globalization;
using System.Text;

namespace Jobs.Infrastructure.Ingestion;

public static class JobTextNormalizer
{
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var lower = input.Trim().ToLowerInvariant();
        var formD = lower.Normalize(NormalizationForm.FormD);
        var noDiacritics = new StringBuilder(formD.Length);

        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                noDiacritics.Append(ch);
            }
        }

        var cleaned = new StringBuilder(noDiacritics.Length);
        foreach (var ch in noDiacritics.ToString().Normalize(NormalizationForm.FormC))
        {
            cleaned.Append(char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ');
        }

        return string.Join(' ', cleaned.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
