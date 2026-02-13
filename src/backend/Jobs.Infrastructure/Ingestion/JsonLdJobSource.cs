using Jobs.Domain.Models;
using Jobs.Infrastructure.Options;
using Microsoft.Extensions.Logging;

namespace Jobs.Infrastructure.Ingestion;

public sealed class JsonLdJobSource : IJobSource
{
    private readonly JsonLdSourceOptions _source;
    private readonly Fingerprint _fingerprint;
    private readonly ILogger<JsonLdJobSource> _logger;
    private readonly IHttpClientFactory _httpFactory;

    public JsonLdJobSource(
        JsonLdSourceOptions source,
        Fingerprint fingerprint,
        ILogger<JsonLdJobSource> logger,
        IHttpClientFactory httpFactory)
    {
        _source = source;
        _fingerprint = fingerprint;
        _logger = logger;
        _httpFactory = httpFactory;
    }

    public string Name => _source.Name;
    public SourceType Type => SourceType.JsonLd;

    public async IAsyncEnumerable<JobPosting> FetchAsync(
        IngestionFetchOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (!_source.Enabled)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(_source.StartUrl))
        {
            _logger.LogWarning("JsonLd source sem StartUrl: {SourceName}", Name);
            yield break;
        }

        var sourceHttp = new SourceHttpClient(_httpFactory.CreateClient("Sources"), _logger);
        var html = await sourceHttp.TryGetStringAsync(_source.StartUrl, Name, ct);
        if (string.IsNullOrWhiteSpace(html))
        {
            yield break;
        }

        var items = JsonLdHtmlParser.ParseJobPostings(html, _source.StartUrl)
            .Take(ResolveMaxItems(options))
            .ToList();

        if (items.Count == 0)
        {
            _logger.LogInformation("Fonte {Source} sem json-ld de JobPosting (no json-ld).", Name);
            yield break;
        }

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            yield return ToJobPosting(item);
        }
    }

    private JobPosting ToJobPosting(ParsedJsonLdJob item)
    {
        var workMode = InfoJobsHtmlParser.InferWorkMode($"{item.WorkModeHint} {item.LocationText} {item.DescriptionText}");
        var employmentType = InferEmploymentType(item.EmploymentType, item.DescriptionText);

        return new JobPosting
        {
            Source = new JobSourceRef(Name, Type, item.Url, item.SourceJobId),
            Title = item.Title,
            Company = new CompanyRef(item.Company),
            LocationText = item.LocationText,
            WorkMode = workMode,
            EmploymentType = employmentType,
            DescriptionText = item.DescriptionText ?? string.Empty,
            Tags = SourceTagInferer.Infer(item.Title, item.DescriptionText),
            Languages = InferLanguages(item.DescriptionText),
            PostedAt = item.PostedAt,
            CapturedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            Status = JobStatus.Active,
            Dedupe = new DedupeInfo(_fingerprint.Compute(item.Company, item.Title, item.LocationText, workMode.ToString()), null),
            Metadata = new Dictionary<string, object>
            {
                ["source"] = Name,
                ["startUrl"] = _source.StartUrl,
                ["parser"] = "json-ld"
            }
        };
    }

    private int ResolveMaxItems(IngestionFetchOptions options)
    {
        if (_source.MaxItemsPerRun is > 0)
        {
            return _source.MaxItemsPerRun.Value;
        }

        return Math.Max(1, options.MaxItemsPerRun);
    }

    private static EmploymentType InferEmploymentType(string? raw, string? description)
    {
        var text = JobTextNormalizer.Normalize($"{raw} {description}");
        if (text.Contains("intern", StringComparison.Ordinal) || text.Contains("estagio", StringComparison.Ordinal))
        {
            return EmploymentType.Internship;
        }

        if (text.Contains("contractor", StringComparison.Ordinal) || text.Contains("pj", StringComparison.Ordinal))
        {
            return EmploymentType.Contractor;
        }

        if (text.Contains("temporary", StringComparison.Ordinal) || text.Contains("temporario", StringComparison.Ordinal))
        {
            return EmploymentType.Temporary;
        }

        if (text.Contains("clt", StringComparison.Ordinal))
        {
            return EmploymentType.CLT;
        }

        return EmploymentType.Unknown;
    }

    private static IReadOnlyList<string> InferLanguages(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return text.Any(c => "ãõçáéíóú".Contains(char.ToLowerInvariant(c)))
            ? new[] { "pt-BR" }
            : new[] { "en" };
    }
}
