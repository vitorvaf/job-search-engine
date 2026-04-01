using System.Globalization;
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
            var id = FirstString(obj, "id", "jobId", "code", "slug")
                  ?? FirstNumericIdAsString(obj, "id", "jobId");
            var title = FirstString(obj, "name", "title", "jobTitle");
            var url = FirstString(obj, "jobUrl", "url", "absoluteUrl")
                   ?? (id != null ? SynthesizeJobUrl(baseUri, id) : null);

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
                LocationText: FirstString(obj, "location", "locationText", "city")
                           ?? ExtractWorkplaceLocation(obj)
                           ?? "",
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

    private static string? FirstNumericIdAsString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var child))
            {
                continue;
            }

            if (child.ValueKind == JsonValueKind.Number && child.TryGetInt64(out var num))
            {
                return num.ToString(CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static string? ExtractWorkplaceLocation(JsonElement element)
    {
        if (!element.TryGetProperty("workplace", out var workplace) ||
            workplace.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (workplace.TryGetProperty("address", out var address) &&
            address.ValueKind == JsonValueKind.Object)
        {
            var city = FirstString(address, "city");
            var state = FirstString(address, "stateShortName", "state");
            var country = FirstString(address, "country");

            var parts = new[] { city, state, country }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            if (parts.Length > 0)
            {
                return string.Join(", ", parts);
            }
        }

        return FirstString(workplace, "workplaceType");
    }

    private static string SynthesizeJobUrl(Uri baseUri, string jobId)
    {
        var builder = new UriBuilder(baseUri)
        {
            Path = "/job/" + jobId,
            Query = string.Empty
        };
        return builder.Uri.ToString();
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
