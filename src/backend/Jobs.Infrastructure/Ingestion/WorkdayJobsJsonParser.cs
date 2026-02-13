using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Jobs.Infrastructure.Ingestion;

public sealed record WorkdayJobListItem(
    string Title,
    string SourceJobId,
    string SourceUrl,
    string? ExternalPath,
    string LocationText,
    string? EmploymentTypeText,
    DateTimeOffset? PostedAt,
    string? DescriptionText);

public static class WorkdayJobsJsonParser
{
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);

    public static IReadOnlyList<WorkdayJobListItem> ParseListing(
        string json,
        string baseHost,
        string sitePath,
        string fallbackSiteName)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var postings = FindPropertyRecursive(root, "jobPostings");
        if (postings is null || postings.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WorkdayJobListItem>();
        }

        var result = new List<WorkdayJobListItem>();
        foreach (var item in postings.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var title = GetString(item, "title", "jobTitle");
            var externalPath = GetString(item, "externalPath");
            var locationText = GetLocationText(item);
            var employmentTypeText = GetString(item, "timeType", "employmentType", "workerSubType");
            var postedAt = ParseDate(GetString(item, "postedOn", "postedOnDate", "postedDate", "postedDateTime"));

            var sourceUrl = BuildSourceUrl(baseHost, sitePath, fallbackSiteName, externalPath, GetString(item, "id", "jobReqId", "requisitionId"));
            var sourceJobId = ResolveSourceJobId(item, sourceUrl, externalPath);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(sourceJobId))
            {
                continue;
            }

            result.Add(new WorkdayJobListItem(
                Title: title.Trim(),
                SourceJobId: sourceJobId,
                SourceUrl: sourceUrl,
                ExternalPath: externalPath,
                LocationText: string.IsNullOrWhiteSpace(locationText) ? "Unknown" : locationText.Trim(),
                EmploymentTypeText: employmentTypeText?.Trim(),
                PostedAt: postedAt,
                DescriptionText: null));
        }

        return result;
    }

    public static string ParseDetailDescription(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var candidates = new[]
        {
            "jobDescription",
            "description",
            "jobDescriptionHtml"
        };

        string? best = null;
        foreach (var obj in EnumerateObjects(root))
        {
            foreach (var candidate in candidates)
            {
                if (!obj.TryGetProperty(candidate, out var prop) || prop.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var value = NormalizeDescription(prop.GetString());
                if (!string.IsNullOrWhiteSpace(value) && (best is null || value.Length > best.Length))
                {
                    best = value;
                }
            }
        }

        return best ?? string.Empty;
    }

    public static string BuildDetailEndpointPath(string tenant, string siteName, string externalPath, string? sourceJobId)
    {
        if (!string.IsNullOrWhiteSpace(externalPath))
        {
            if (externalPath.StartsWith("/wday/cxs/", StringComparison.OrdinalIgnoreCase))
            {
                return externalPath;
            }

            if (externalPath.StartsWith("/job/", StringComparison.OrdinalIgnoreCase))
            {
                return $"/wday/cxs/{tenant}/{siteName}{externalPath}";
            }
        }

        var jobId = string.IsNullOrWhiteSpace(sourceJobId) ? string.Empty : sourceJobId.Trim();
        return $"/wday/cxs/{tenant}/{siteName}/job/{Uri.EscapeDataString(jobId)}";
    }

    private static string ResolveSourceJobId(JsonElement item, string sourceUrl, string? externalPath)
    {
        var id = GetString(item, "id", "jobReqId", "jobRequisitionId", "requisitionId");
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id.Trim();
        }

        var fromPath = ExtractLastPathSegment(externalPath);
        if (!string.IsNullOrWhiteSpace(fromPath))
        {
            return fromPath!;
        }

        var fromUrl = ExtractLastPathSegment(sourceUrl);
        return string.IsNullOrWhiteSpace(fromUrl) ? sourceUrl : fromUrl!;
    }

    private static string BuildSourceUrl(
        string baseHost,
        string sitePath,
        string fallbackSiteName,
        string? externalPath,
        string? sourceJobId)
    {
        if (!string.IsNullOrWhiteSpace(externalPath))
        {
            if (externalPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                externalPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return externalPath;
            }

            if (externalPath.StartsWith("/", StringComparison.Ordinal))
            {
                return $"https://{baseHost}{externalPath}";
            }
        }

        var normalizedSitePath = string.IsNullOrWhiteSpace(sitePath) ? $"/en-US/{fallbackSiteName}" : sitePath.TrimEnd('/');
        var jobId = string.IsNullOrWhiteSpace(sourceJobId) ? "unknown" : sourceJobId.Trim();
        return $"https://{baseHost}{normalizedSitePath}/job/{Uri.EscapeDataString(jobId)}";
    }

    private static string GetLocationText(JsonElement item)
    {
        var locationsText = GetString(item, "locationsText", "location");
        if (!string.IsNullOrWhiteSpace(locationsText))
        {
            return locationsText.Trim();
        }

        if (item.TryGetProperty("locations", out var locations) && locations.ValueKind == JsonValueKind.Array)
        {
            var values = locations
                .EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : GetString(x, "name", "location"))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .ToList();
            if (values.Count > 0)
            {
                return string.Join(" | ", values);
            }
        }

        if (item.TryGetProperty("bulletFields", out var bulletFields) && bulletFields.ValueKind == JsonValueKind.Array)
        {
            var candidate = bulletFields
                .EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && (x!.Contains(',') || x.Contains('-')));
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate!.Trim();
            }
        }

        return "Unknown";
    }

    private static DateTimeOffset? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
        {
            return dto.ToUniversalTime();
        }

        return null;
    }

    private static string? ExtractLastPathSegment(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var clean = input.Split('?', 2)[0].TrimEnd('/');
        var idx = clean.LastIndexOf('/');
        return idx >= 0 ? clean[(idx + 1)..] : clean;
    }

    private static JsonElement? FindPropertyRecursive(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty(propertyName, out var direct))
            {
                return direct;
            }

            foreach (var prop in root.EnumerateObject())
            {
                var child = FindPropertyRecursive(prop.Value, propertyName);
                if (child is not null)
                {
                    return child;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                var child = FindPropertyRecursive(item, propertyName);
                if (child is not null)
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in EnumerateObjects(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in EnumerateObjects(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static string NormalizeDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var noTags = TagRegex.Replace(value, " ");
        return Regex.Replace(System.Net.WebUtility.HtmlDecode(noTags), @"\s+", " ").Trim();
    }

    private static string? GetString(JsonElement obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!obj.TryGetProperty(key, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }

            if (prop.ValueKind == JsonValueKind.Object)
            {
                var nested = GetString(prop, "name", "value", "label", "text");
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }
}
