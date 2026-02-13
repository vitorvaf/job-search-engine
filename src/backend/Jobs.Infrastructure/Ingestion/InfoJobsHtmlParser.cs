using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jobs.Domain.Models;

namespace Jobs.Infrastructure.Ingestion;

public static class InfoJobsHtmlParser
{
    private static readonly Regex InfoJobsJobUrlRegex = new(
        "^https:\\/\\/www\\.infojobs\\.com\\.br\\/vaga-de-.*__\\d+\\.aspx$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NextDataRegex = new(
        "<script[^>]*id=\"__NEXT_DATA__\"[^>]*>(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex JsonLdRegex = new(
        "<script[^>]*type=\"application/ld\\+json\"[^>]*>(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex AnchorRegex = new(
        "<a[^>]*href=\"(?<href>[^\"]+)\"[^>]*>(?<text>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static IReadOnlyList<ParsedSourceJob> ParseList(string html, string searchUrl)
    {
        var jobs = new List<ParsedSourceJob>();
        var baseUri = new Uri(searchUrl);

        jobs.AddRange(ParseFromNextData(html, baseUri));
        jobs.AddRange(ParseFromJsonLd(html, baseUri));
        jobs.AddRange(ParseFromAnchors(html, baseUri));

        return jobs
            .Where(j => !string.IsNullOrWhiteSpace(j.Title) && !string.IsNullOrWhiteSpace(j.Url))
            .Where(j => IsValidInfoJobsJobUrl(j.Url))
            .GroupBy(j => j.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    public static bool IsValidInfoJobsJobUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) && InfoJobsJobUrlRegex.IsMatch(url);
    }

    public static string? ExtractInfoJobsJobIdFromUrl(string? url)
    {
        if (!IsValidInfoJobsJobUrl(url))
        {
            return null;
        }

        var match = Regex.Match(url!, "__(?<id>\\d+)\\.aspx$", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["id"].Value : null;
    }

    public static string ParseDetailDescription(string html)
    {
        var noScript = Regex.Replace(html, "<script.*?</script>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var candidates = new[]
        {
            "job description",
            "descricao da vaga",
            "descricao da vaga",
            "responsabilidades",
            "requirements",
            "requisitos"
        };

        var normalized = JobTextNormalizer.Normalize(noScript);
        if (!candidates.Any(c => normalized.Contains(c, StringComparison.Ordinal)))
        {
            return CleanHtmlText(noScript);
        }

        var sectionMatch = Regex.Match(noScript,
            "<(section|div)[^>]*(description|descricao|requirements|responsabilidades)[^>]*>(?<body>.*?)</(section|div)>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (sectionMatch.Success)
        {
            return CleanHtmlText(sectionMatch.Groups["body"].Value);
        }

        return CleanHtmlText(noScript);
    }

    public static string BuildStableSourceJobId(string? sourceJobId, string url)
    {
        var byInfoJobsSlug = ExtractInfoJobsJobIdFromUrl(url);
        if (!string.IsNullOrWhiteSpace(byInfoJobsSlug))
        {
            return byInfoJobsSlug;
        }

        if (!string.IsNullOrWhiteSpace(sourceJobId))
        {
            return sourceJobId;
        }

        var byQuery = Regex.Match(url, "[?&](id|iv|jobid)=(?<id>\\d+)", RegexOptions.IgnoreCase);
        if (byQuery.Success)
        {
            return byQuery.Groups["id"].Value;
        }

        var byPath = Regex.Match(url, "(?<id>\\d{5,})", RegexOptions.IgnoreCase);
        if (byPath.Success)
        {
            return byPath.Groups["id"].Value;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(url.Trim().ToLowerInvariant()));
        return "url:" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    public static WorkMode InferWorkMode(string? raw)
    {
        var text = JobTextNormalizer.Normalize(raw);
        if (text.Contains("remote", StringComparison.Ordinal) || text.Contains("remoto", StringComparison.Ordinal))
        {
            return WorkMode.Remote;
        }

        if (text.Contains("hybrid", StringComparison.Ordinal) || text.Contains("hibrido", StringComparison.Ordinal))
        {
            return WorkMode.Hybrid;
        }

        if (text.Contains("onsite", StringComparison.Ordinal) || text.Contains("presencial", StringComparison.Ordinal))
        {
            return WorkMode.Onsite;
        }

        return WorkMode.Unknown;
    }

    public static (decimal? Min, decimal? Max, string? Currency, string? Period) ParseSalary(string? salaryText)
    {
        if (string.IsNullOrWhiteSpace(salaryText))
        {
            return (null, null, null, null);
        }

        var numbers = Regex.Matches(salaryText, "\\d+[\\d\\.,]*")
            .Select(x => ParseDecimal(x.Value))
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .ToArray();

        var currency = salaryText.Contains("R$", StringComparison.OrdinalIgnoreCase) ? "BRL" : null;
        var period = JobTextNormalizer.Normalize(salaryText).Contains("mes", StringComparison.Ordinal) ? "month" : null;

        return numbers.Length switch
        {
            0 => (null, null, currency, period),
            1 => (numbers[0], numbers[0], currency, period),
            _ => (numbers[0], numbers[1], currency, period)
        };
    }

    private static decimal? ParseDecimal(string raw)
    {
        var trimmed = raw.Trim();
        if (decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out var ptValue))
        {
            return ptValue;
        }

        if (decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var invValue))
        {
            return invValue;
        }

        return null;
    }

    private static IEnumerable<ParsedSourceJob> ParseFromNextData(string html, Uri baseUri)
    {
        var match = NextDataRegex.Match(html);
        if (!match.Success)
        {
            yield break;
        }

        using var doc = JsonDocument.Parse(match.Groups["json"].Value);
        foreach (var job in EnumerateObjects(doc.RootElement))
        {
            var title = FirstString(job, "title", "jobTitle", "titulo");
            var url = FirstString(job, "url", "jobUrl", "link");
            var company = FirstString(job, "company", "companyName", "employer", "nomeEmpresa");
            var location = FirstString(job, "location", "locationText", "cidade", "localizacao");

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var absoluteUrl = MakeAbsolute(url, baseUri);
            if (!IsValidInfoJobsJobUrl(absoluteUrl))
            {
                continue;
            }

            yield return new ParsedSourceJob
            {
                Title = CleanHtmlText(title),
                Company = CleanHtmlText(company) is { Length: > 0 } parsedCompany ? parsedCompany : "Unknown",
                LocationText = CleanHtmlText(location),
                Url = absoluteUrl,
                SalaryText = FirstString(job, "salary", "salaryText", "remuneration"),
                WorkModeText = FirstString(job, "workMode", "modality", "modeloTrabalho"),
                SourceJobId = FirstString(job, "jobId", "id", "sourceJobId"),
                PostedAt = FirstDate(job, "postedAt", "publishedAt", "publicationDate")
            };
        }
    }

    private static IEnumerable<ParsedSourceJob> ParseFromJsonLd(string html, Uri baseUri)
    {
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
                foreach (var obj in EnumerateObjects(doc.RootElement))
                {
                    var type = FirstString(obj, "@type", "type");
                    if (!string.Equals(type, "JobPosting", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var title = FirstString(obj, "title");
                    var url = FirstString(obj, "url");
                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
                    {
                        continue;
                    }

                    var absoluteUrl = MakeAbsolute(url, baseUri);
                    if (!IsValidInfoJobsJobUrl(absoluteUrl))
                    {
                        continue;
                    }

                    yield return new ParsedSourceJob
                    {
                        Title = CleanHtmlText(title),
                        Company = ExtractJsonLdCompany(obj),
                        LocationText = FirstString(obj, "jobLocation", "address", "location") ?? string.Empty,
                        Url = absoluteUrl,
                        SalaryText = FirstString(obj, "baseSalary"),
                        PostedAt = FirstDate(obj, "datePosted")
                    };
                }
            }
        }
    }

    private static string ExtractJsonLdCompany(JsonElement obj)
    {
        if (obj.TryGetProperty("hiringOrganization", out var hiringOrg) &&
            hiringOrg.ValueKind == JsonValueKind.Object &&
            hiringOrg.TryGetProperty("name", out var name))
        {
            return name.GetString() ?? "Unknown";
        }

        return "Unknown";
    }

    private static IEnumerable<ParsedSourceJob> ParseFromAnchors(string html, Uri baseUri)
    {
        foreach (Match match in AnchorRegex.Matches(html))
        {
            var href = match.Groups["href"].Value;
            var title = CleanHtmlText(match.Groups["text"].Value);
            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var absoluteUrl = MakeAbsolute(href, baseUri);
            if (!IsValidInfoJobsJobUrl(absoluteUrl))
            {
                continue;
            }

            var window = ExtractWindow(html, match.Index, 1200);

            yield return new ParsedSourceJob
            {
                Title = title,
                Company = ExtractByRegex(window, "(empresa|company)[^<:\\n]{0,40}[:\\-]?\\s*(?<v>[A-Za-z0-9 .&\\-]{2,80})") ?? "Unknown",
                LocationText = ExtractByRegex(window, "(local|location|cidade)[^<:\\n]{0,40}[:\\-]?\\s*(?<v>[A-Za-z0-9 .,/\\-]{2,120})") ?? string.Empty,
                SalaryText = ExtractByRegex(window, "(salario|salary)[^<:\\n]{0,40}[:\\-]?\\s*(?<v>[^<\\n]{2,80})"),
                Url = absoluteUrl,
                SourceJobId = ExtractInfoJobsJobIdFromUrl(absoluteUrl)
            };
        }
    }

    private static string? ExtractByRegex(string input, string pattern)
    {
        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? CleanHtmlText(match.Groups["v"].Value) : null;
    }

    private static string ExtractWindow(string html, int idx, int size)
    {
        var start = Math.Max(0, idx - size / 2);
        var len = Math.Min(size, html.Length - start);
        return html.Substring(start, len);
    }

    private static DateTimeOffset? FirstDate(JsonElement obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!obj.TryGetProperty(key, out var element) || element.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (DateTimeOffset.TryParse(element.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? FirstString(JsonElement obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!obj.TryGetProperty(key, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                var nestedName = FirstString(element, "name", "value", "text");
                if (!string.IsNullOrWhiteSpace(nestedName))
                {
                    return nestedName;
                }
            }
        }

        return null;
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement root)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Object:
                yield return root;
                foreach (var property in root.EnumerateObject())
                {
                    foreach (var child in EnumerateObjects(property.Value))
                    {
                        yield return child;
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in root.EnumerateArray())
                {
                    foreach (var child in EnumerateObjects(item))
                    {
                        yield return child;
                    }
                }
                break;
        }
    }

    private static string MakeAbsolute(string rawUrl, Uri baseUri)
    {
        if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (Uri.TryCreate(baseUri, rawUrl, out var relative))
        {
            return relative.ToString();
        }

        return rawUrl;
    }

    private static string CleanHtmlText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var withoutTags = Regex.Replace(raw, "<[^>]+>", " ");
        var decoded = System.Net.WebUtility.HtmlDecode(withoutTags);
        return string.Join(' ', decoded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
