using Jobs.Domain.Models;

namespace Jobs.Infrastructure.Ingestion;

public static class SourceTypeResolver
{
    public static SourceType ParseOrDefault(string? raw, SourceType fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return Enum.TryParse<SourceType>(raw, true, out var parsed)
            ? parsed
            : fallback;
    }
}
