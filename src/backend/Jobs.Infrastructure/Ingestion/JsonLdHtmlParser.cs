using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Jobs.Infrastructure.Ingestion;

public sealed record ParsedJsonLdJob(
    string Title,
    string Url,
    string Company,
    string LocationText,
    string SourceJobId,
    string? DescriptionText,
    DateTimeOffset? PostedAt,
    string? EmploymentType,
    string? WorkModeHint);

public static class JsonLdHtmlParser
{
    private static readonly Regex JsonLdRegex = new(
        "<script[^>]*type=\"application/ld\\+json\"[^>]*>(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static IReadOnlyList<ParsedJsonLdJob> ParseJobPostings(string html, string startUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<ParsedJsonLdJob>();
        }

        var baseUri = new Uri(startUrl);
        var jobs = new List<ParsedJsonLdJob>();

        foreach (Match match in JsonLdRegex.Matches(html))
        {
            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(match.Groups["json"].Value);
            }
            catch
            {
                doc?.Dispose();
                continue;
            }

            using (doc)
            {
                foreach (var node in EnumerateObjects(doc.RootElement))
                {
                    if (!IsJobPosting(node))
                    {
                        continue;
                    }

                    var title = FirstString(node, "title");
                    var url = FirstString(node, "url") ?? startUrl;
                    var absoluteUrl = MakeAbsolute(url, baseUri);

                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(absoluteUrl))
                    {
                        continue;
                    }

                    var sourceJobId = ResolveSourceJobId(node, absoluteUrl);
                    jobs.Add(new ParsedJsonLdJob(
                        Title: CleanText(title),
                        Url: absoluteUrl,
                        Company: ExtractCompany(node),
                        LocationText: ExtractLocation(node),
                        SourceJobId: sourceJobId,
                        DescriptionText: FirstString(node, "description"),
                        PostedAt: FirstDate(node, "datePosted", "validFrom"),
                        EmploymentType: FirstString(node, "employmentType"),
                        WorkModeHint: FirstString(node, "jobLocationType")));
                }
            }
        }

        return jobs
            .Where(j => !string.IsNullOrWhiteSpace(j.Title) && !string.IsNullOrWhiteSpace(j.Url))
            .GroupBy(j => j.SourceJobId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static bool IsJobPosting(JsonElement node)
    {
        if (!node.TryGetProperty("@type", out var typeElement))
        {
            return false;
        }

        return typeElement.ValueKind switch
        {
            JsonValueKind.String => string.Equals(typeElement.GetString(), "JobPosting", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => typeElement.EnumerateArray().Any(x =>
                x.ValueKind == JsonValueKind.String &&
                string.Equals(x.GetString(), "JobPosting", StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private static string ResolveSourceJobId(JsonElement node, string url)
    {
        if (node.TryGetProperty("identifier", out var identifier))
        {
            if (identifier.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(identifier.GetString()))
            {
                return identifier.GetString()!;
            }

            if (identifier.ValueKind == JsonValueKind.Object)
            {
                var idValue = FirstString(identifier, "value", "name");
                if (!string.IsNullOrWhiteSpace(idValue))
                {
                    return idValue;
                }
            }
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(url.Trim().ToLowerInvariant()));
        return "url:" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string ExtractCompany(JsonElement node)
    {
        if (node.TryGetProperty("hiringOrganization", out var hiring))
        {
            var name = FirstString(hiring, "name", "legalName");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return CleanText(name);
            }
        }

        return "Unknown";
    }

    private static string ExtractLocation(JsonElement node)
    {
        if (node.TryGetProperty("jobLocation", out var location))
        {
            if (location.ValueKind == JsonValueKind.Array)
            {
                var values = location.EnumerateArray().Select(ExtractLocation).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                return string.Join(" | ", values);
            }

            if (location.ValueKind == JsonValueKind.Object)
            {
                if (location.TryGetProperty("address", out var address))
                {
                    var parts = new[]
                    {
                        FirstString(address, "addressLocality"),
                        FirstString(address, "addressRegion"),
                        FirstString(address, "addressCountry")
                    }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

                    if (parts.Length > 0)
                    {
                        return string.Join(", ", parts.Select(x => CleanText(x!)));
                    }
                }

                var name = FirstString(location, "name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return CleanText(name);
                }
            }
        }

        if (node.TryGetProperty("applicantLocationRequirements", out var req) &&
            req.ValueKind == JsonValueKind.Object)
        {
            var name = FirstString(req, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return CleanText(name);
            }
        }

        return "";
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

            if (child.ValueKind == JsonValueKind.Object)
            {
                var nested = FirstString(child, "name", "value", "@id");
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static DateTimeOffset? FirstDate(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (DateTimeOffset.TryParse(value.GetString(), out var parsed))
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

    private static string CleanText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var noTags = Regex.Replace(raw, "<.*?>", " ", RegexOptions.Singleline);
        return Regex.Replace(WebUtility.HtmlDecode(noTags), "\\s+", " ").Trim();
    }
}
