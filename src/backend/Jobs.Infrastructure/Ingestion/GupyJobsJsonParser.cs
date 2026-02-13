using System.Text.Json;

namespace Jobs.Infrastructure.Ingestion;

public sealed record ParsedGupyJob(
    string SourceJobId,
    string Url,
    string Title,
    string LocationText,
    string? DescriptionText,
    DateTimeOffset? PostedAt);

public static class GupyJobsJsonParser
{
    public static IReadOnlyList<ParsedGupyJob> Parse(string json, string companyBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<ParsedGupyJob>();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var jobs = new List<ParsedGupyJob>();
        var baseUri = new Uri(companyBaseUrl);

        foreach (var obj in EnumerateObjects(root))
        {
            var id = FirstString(obj, "id", "jobId", "code", "slug");
            var title = FirstString(obj, "name", "title", "jobTitle");
            var url = FirstString(obj, "jobUrl", "url", "absoluteUrl");

            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var absoluteUrl = MakeAbsolute(url, baseUri);
            jobs.Add(new ParsedGupyJob(
                SourceJobId: id,
                Url: absoluteUrl,
                Title: title.Trim(),
                LocationText: FirstString(obj, "location", "locationText", "city", "workplace") ?? "",
                DescriptionText: FirstString(obj, "description", "jobDescription"),
                PostedAt: FirstDate(obj, "publishedAt", "createdAt", "datePublished")));
        }

        return jobs
            .GroupBy(x => x.SourceJobId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                yield return element;
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var child in EnumerateObjects(property.Value))
                    {
                        yield return child;
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var child in EnumerateObjects(item))
                    {
                        yield return child;
                    }
                }
                break;
        }
    }

    private static string? FirstString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var child))
            {
                continue;
            }

            if (child.ValueKind == JsonValueKind.String)
            {
                return child.GetString();
            }
        }

        return null;
    }

    private static DateTimeOffset? FirstDate(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = FirstString(element, key);
            if (value is not null && DateTimeOffset.TryParse(value, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string MakeAbsolute(string url, Uri baseUri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (Uri.TryCreate(baseUri, url, out var relative))
        {
            return relative.ToString();
        }

        return url;
    }
}
