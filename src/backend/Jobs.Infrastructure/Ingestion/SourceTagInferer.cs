namespace Jobs.Infrastructure.Ingestion;

internal static class SourceTagInferer
{
    private static readonly (string Needle, string Tag)[] Keywords =
    {
        (".net", "dotnet"),
        ("asp.net", "dotnet"),
        ("c#", "csharp"),
        ("react", "react"),
        ("typescript", "typescript"),
        ("javascript", "javascript"),
        ("azure", "azure"),
        ("aws", "aws"),
        ("postgres", "postgres"),
        ("postgresql", "postgres"),
        ("kafka", "kafka"),
        ("docker", "docker"),
        ("kubernetes", "kubernetes"),
        ("golang", "golang"),
        ("java", "java"),
        ("python", "python")
    };

    public static IReadOnlyList<string> Infer(string? title, string? description)
    {
        var rawText = $"{title} {description}".ToLowerInvariant();
        var text = JobTextNormalizer.Normalize($"{title} {description}");
        var tags = new List<string>();

        foreach (var (needle, tag) in Keywords)
        {
            var normalizedNeedle = JobTextNormalizer.Normalize(needle);
            if ((rawText.Contains(needle, StringComparison.Ordinal) || text.Contains(normalizedNeedle, StringComparison.Ordinal)) &&
                !tags.Contains(tag))
            {
                tags.Add(tag);
            }
        }

        return tags;
    }
}
