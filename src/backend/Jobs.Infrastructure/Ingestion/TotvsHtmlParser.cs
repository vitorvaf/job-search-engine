using System.Net;
using System.Text.RegularExpressions;

namespace Jobs.Infrastructure.Ingestion;

public static class TotvsHtmlParser
{
    private static readonly Regex JobAnchorRegex = new(
        "<a[^>]*href=\"(?<href>[^\"]+)\"[^>]*>(?<title>.*?)</a>(?<tail>.{0,600})",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static IReadOnlyList<ParsedSourceJob> ParseList(string html, string startUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<ParsedSourceJob>();
        }

        var baseUri = new Uri(startUrl);
        var jobs = new List<ParsedSourceJob>();

        foreach (Match match in JobAnchorRegex.Matches(html))
        {
            var href = match.Groups["href"].Value;
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            if (!LooksLikeJobLink(href))
            {
                continue;
            }

            var title = CleanText(match.Groups["title"].Value);
            if (string.IsNullOrWhiteSpace(title) || title.Length < 4)
            {
                continue;
            }

            var context = CleanText(match.Groups["tail"].Value);
            var location = ExtractLocation(context);
            var url = MakeAbsolute(href, baseUri);

            jobs.Add(new ParsedSourceJob
            {
                Title = title,
                Company = "TOTVS",
                LocationText = location,
                Url = url,
                SourceJobId = InfoJobsHtmlParser.BuildStableSourceJobId(null, url),
                WorkModeText = context
            });
        }

        return jobs
            .GroupBy(j => j.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static bool LooksLikeJobLink(string href)
    {
        var normalized = href.ToLowerInvariant();
        return normalized.Contains("/job/") ||
               normalized.Contains("/jobs/") ||
               normalized.Contains("oportunidade") ||
               normalized.Contains("vaga");
    }

    private static string ExtractLocation(string context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return "";
        }

        var locationMatch = Regex.Match(
            context,
            "(?<loc>(remote|remoto|hybrid|hibrido|híbrido|onsite|presencial|[A-Za-zÀ-ÿ ]{2,40}\\s?,\\s?[A-Z]{2}))",
            RegexOptions.IgnoreCase);

        return locationMatch.Success ? locationMatch.Groups["loc"].Value.Trim() : "";
    }

    private static string MakeAbsolute(string href, Uri baseUri)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (Uri.TryCreate(baseUri, href, out var relative))
        {
            return relative.ToString();
        }

        return href;
    }

    private static string CleanText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        var noTags = Regex.Replace(raw, "<.*?>", " ", RegexOptions.Singleline);
        return Regex.Replace(WebUtility.HtmlDecode(noTags), "\\s+", " ").Trim();
    }
}
