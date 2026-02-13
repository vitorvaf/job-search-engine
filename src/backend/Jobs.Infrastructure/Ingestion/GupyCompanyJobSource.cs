using Jobs.Domain.Models;
using Jobs.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Jobs.Infrastructure.Ingestion;

public sealed class GupyCompanyJobSource : IJobSource
{
    private static readonly Regex NextDataRegex = new(
        "<script[^>]*id=[\"']__NEXT_DATA__[\"'][^>]*>(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex AppJsonRegex = new(
        "<script[^>]*type=[\"']application/(ld\\+)?json[\"'][^>]*>(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly GupyCompanySourceOptions _source;
    private readonly Fingerprint _fingerprint;
    private readonly ILogger<GupyCompanyJobSource> _logger;
    private readonly IHttpClientFactory _httpFactory;

    public GupyCompanyJobSource(
        GupyCompanySourceOptions source,
        Fingerprint fingerprint,
        ILogger<GupyCompanyJobSource> logger,
        IHttpClientFactory httpFactory)
    {
        _source = source;
        _fingerprint = fingerprint;
        _logger = logger;
        _httpFactory = httpFactory;
    }

    public string Name => _source.Name;
    public SourceType Type => SourceType.Gupy;

    public async IAsyncEnumerable<JobPosting> FetchAsync(
        IngestionFetchOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (!_source.Enabled)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(_source.CompanyBaseUrl))
        {
            _logger.LogWarning("Gupy source sem CompanyBaseUrl: {SourceName}", Name);
            yield break;
        }

        var sourceHttp = new SourceHttpClient(_httpFactory.CreateClient("Sources"), _logger);
        var endpointCandidates = BuildEndpointCandidates(_source.CompanyBaseUrl);
        var jobs = Array.Empty<ParsedGupyJob>();
        string? selectedEndpoint = null;

        foreach (var endpoint in endpointCandidates)
        {
            var payload = await sourceHttp.TryGetStringAsync(endpoint, Name, ct);
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            jobs = ParsePayload(payload, endpoint);
            if (jobs.Length > 0)
            {
                selectedEndpoint = endpoint;
                break;
            }
        }

        if (jobs.Length == 0)
        {
            _logger.LogInformation(
                "Fonte {Source} não retornou vagas em nenhum endpoint candidato. CompanyBaseUrl={CompanyBaseUrl}",
                Name,
                _source.CompanyBaseUrl);
            yield break;
        }

        if (!string.Equals(selectedEndpoint, _source.CompanyBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Fonte {Source} resolveu endpoint automaticamente. base={BaseUrl} selected={SelectedEndpoint}",
                Name,
                _source.CompanyBaseUrl,
                selectedEndpoint);
        }

        foreach (var item in jobs.Take(ResolveMaxItems(options)))
        {
            ct.ThrowIfCancellationRequested();
            var workMode = InfoJobsHtmlParser.InferWorkMode($"{item.LocationText} {item.DescriptionText}");
            yield return new JobPosting
            {
                Source = new JobSourceRef(Name, Type, item.Url, item.SourceJobId),
                Title = item.Title,
                Company = new CompanyRef(Name),
                LocationText = item.LocationText,
                WorkMode = workMode,
                DescriptionText = item.DescriptionText ?? string.Empty,
                Tags = SourceTagInferer.Infer(item.Title, item.DescriptionText),
                Languages = new[] { "pt-BR" },
                PostedAt = item.PostedAt,
                CapturedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
                Status = JobStatus.Active,
                Dedupe = new DedupeInfo(_fingerprint.Compute(Name, item.Title, item.LocationText, workMode.ToString()), null),
                Metadata = new Dictionary<string, object>
                {
                    ["source"] = Name,
                    ["companyBaseUrl"] = _source.CompanyBaseUrl,
                    ["resolvedEndpoint"] = selectedEndpoint ?? _source.CompanyBaseUrl,
                    ["parser"] = "gupy-json"
                }
            };
        }
    }

    private int ResolveMaxItems(IngestionFetchOptions options)
    {
        if (_source.MaxItemsPerRun is > 0)
        {
            return _source.MaxItemsPerRun.Value;
        }

        return Math.Max(1, options.MaxItemsPerRun);
    }

    private static bool LooksLikeJson(string payload)
    {
        foreach (var ch in payload)
        {
            if (!char.IsWhiteSpace(ch))
            {
                return ch == '{' || ch == '[';
            }
        }

        return false;
    }

    private static string[] BuildEndpointCandidates(string companyBaseUrl)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return;
            }

            var normalized = uri.ToString();
            if (seen.Add(normalized))
            {
                candidates.Add(normalized);
            }
        }

        Add(companyBaseUrl);

        if (!Uri.TryCreate(companyBaseUrl, UriKind.Absolute, out var baseUri))
        {
            return candidates.ToArray();
        }

        if (!baseUri.Host.Contains("gupy.io", StringComparison.OrdinalIgnoreCase))
        {
            return candidates.ToArray();
        }

        Add(ReplacePath(baseUri, "/jobs.json"));
        Add(ReplacePath(baseUri, "/jobs"));
        Add(ReplacePath(baseUri, "/"));

        var path = baseUri.AbsolutePath.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(path))
        {
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                Add(ReplacePath(baseUri, path + ".json"));
            }

            if (!path.EndsWith("/jobs", StringComparison.OrdinalIgnoreCase))
            {
                Add(ReplacePath(baseUri, path + "/jobs"));
                Add(ReplacePath(baseUri, path + "/jobs.json"));
            }
            else
            {
                Add(ReplacePath(baseUri, path + ".json"));
            }
        }

        return candidates.ToArray();
    }

    private static ParsedGupyJob[] ParsePayload(string payload, string baseUrl)
    {
        if (LooksLikeJson(payload))
        {
            return TryParseJson(payload, baseUrl);
        }

        foreach (var embeddedJson in ExtractEmbeddedJson(payload))
        {
            var parsed = TryParseJson(embeddedJson, baseUrl);
            if (parsed.Length > 0)
            {
                return parsed;
            }
        }

        return Array.Empty<ParsedGupyJob>();
    }

    private static ParsedGupyJob[] TryParseJson(string json, string baseUrl)
    {
        try
        {
            return GupyJobsJsonParser.Parse(json, baseUrl).ToArray();
        }
        catch
        {
            return Array.Empty<ParsedGupyJob>();
        }
    }

    private static IEnumerable<string> ExtractEmbeddedJson(string html)
    {
        foreach (Match match in NextDataRegex.Matches(html))
        {
            var json = match.Groups["json"].Value.Trim();
            if (LooksLikeJson(json))
            {
                yield return json;
            }
        }

        foreach (Match match in AppJsonRegex.Matches(html))
        {
            var json = match.Groups["json"].Value.Trim();
            if (LooksLikeJson(json))
            {
                yield return json;
            }
        }
    }

    private static string ReplacePath(Uri baseUri, string path)
    {
        var builder = new UriBuilder(baseUri)
        {
            Path = path,
            Query = string.Empty
        };
        return builder.Uri.ToString();
    }
}
