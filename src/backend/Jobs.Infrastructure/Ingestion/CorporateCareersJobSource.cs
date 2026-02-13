using Jobs.Domain.Models;
using Jobs.Infrastructure.Options;
using Microsoft.Extensions.Logging;

namespace Jobs.Infrastructure.Ingestion;

public sealed class CorporateCareersJobSource : IJobSource
{
    private readonly CorporateCareerSourceOptions _source;
    private readonly Fingerprint _fingerprint;
    private readonly ILogger<CorporateCareersJobSource> _logger;
    private readonly IHttpClientFactory _httpFactory;

    public CorporateCareersJobSource(
        CorporateCareerSourceOptions source,
        Fingerprint fingerprint,
        ILogger<CorporateCareersJobSource> logger,
        IHttpClientFactory httpFactory)
    {
        _source = source;
        _fingerprint = fingerprint;
        _logger = logger;
        _httpFactory = httpFactory;
    }

    public string Name => _source.Name;
    public SourceType Type => SourceTypeResolver.ParseOrDefault(_source.Type, SourceType.CorporateCareers);

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
            _logger.LogWarning("Corporate source sem StartUrl: {SourceName}", Name);
            yield break;
        }

        var sourceHttp = new SourceHttpClient(_httpFactory.CreateClient("Sources"), _logger);
        var listHtml = await sourceHttp.TryGetStringAsync(_source.StartUrl, Name, ct);
        if (string.IsNullOrWhiteSpace(listHtml))
        {
            yield break;
        }

        var maxItems = ResolveMaxItems(options);
        var maxDetailFetch = ResolveMaxDetails(options);

        var parsed = JsonLdHtmlParser.ParseJobPostings(listHtml, _source.StartUrl)
            .Take(maxItems)
            .ToList();

        if (parsed.Count > 0)
        {
            foreach (var item in parsed)
            {
                ct.ThrowIfCancellationRequested();
                var description = item.DescriptionText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(description) && maxDetailFetch > 0)
                {
                    var detailHtml = await sourceHttp.TryGetStringAsync(item.Url, Name, ct);
                    if (!string.IsNullOrWhiteSpace(detailHtml))
                    {
                        var detail = JsonLdHtmlParser.ParseJobPostings(detailHtml, item.Url).FirstOrDefault();
                        description = detail?.DescriptionText ?? InfoJobsHtmlParser.ParseDetailDescription(detailHtml);
                    }

                    maxDetailFetch--;
                }

                yield return BuildJob(item with { DescriptionText = description });
            }

            yield break;
        }

        if (IsTotvs())
        {
            var jobs = TotvsHtmlParser.ParseList(listHtml, _source.StartUrl)
                .Take(maxItems)
                .ToList();

            if (jobs.Count == 0)
            {
                _logger.LogInformation("Fonte {Source} sem vagas parseadas.", Name);
                yield break;
            }

            foreach (var job in jobs)
            {
                ct.ThrowIfCancellationRequested();
                var description = string.Empty;
                if (maxDetailFetch > 0)
                {
                    var detailHtml = await sourceHttp.TryGetStringAsync(job.Url, Name, ct);
                    if (!string.IsNullOrWhiteSpace(detailHtml))
                    {
                        description = InfoJobsHtmlParser.ParseDetailDescription(detailHtml);
                    }

                    maxDetailFetch--;
                }

                yield return BuildJob(job, description);
            }

            yield break;
        }

        if (RequiresJsSource())
        {
            _logger.LogInformation("Fonte {Source} requires JS - skipped", Name);
            yield break;
        }

        _logger.LogInformation("Fonte {Source} sem json-ld (no json-ld).", Name);
    }

    private JobPosting BuildJob(ParsedJsonLdJob item)
    {
        var workMode = InfoJobsHtmlParser.InferWorkMode($"{item.WorkModeHint} {item.LocationText} {item.DescriptionText}");

        return new JobPosting
        {
            Source = new JobSourceRef(Name, Type, item.Url, item.SourceJobId),
            Title = item.Title,
            Company = new CompanyRef(string.IsNullOrWhiteSpace(item.Company) ? Name : item.Company),
            LocationText = item.LocationText,
            WorkMode = workMode,
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

    private JobPosting BuildJob(ParsedSourceJob item, string descriptionText)
    {
        var workMode = InfoJobsHtmlParser.InferWorkMode($"{item.WorkModeText} {item.LocationText} {descriptionText}");
        return new JobPosting
        {
            Source = new JobSourceRef(Name, Type, item.Url, item.SourceJobId),
            Title = item.Title,
            Company = new CompanyRef(string.IsNullOrWhiteSpace(item.Company) ? Name : item.Company),
            LocationText = item.LocationText,
            WorkMode = workMode,
            DescriptionText = descriptionText,
            Tags = SourceTagInferer.Infer(item.Title, descriptionText),
            Languages = InferLanguages(descriptionText),
            PostedAt = item.PostedAt,
            CapturedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            Status = JobStatus.Active,
            Dedupe = new DedupeInfo(_fingerprint.Compute(item.Company, item.Title, item.LocationText, workMode.ToString()), null),
            Metadata = new Dictionary<string, object>
            {
                ["source"] = Name,
                ["startUrl"] = _source.StartUrl,
                ["parser"] = "totvs-html"
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

    private int ResolveMaxDetails(IngestionFetchOptions options)
    {
        if (_source.MaxDetailFetch is > 0)
        {
            return _source.MaxDetailFetch.Value;
        }

        return Math.Max(0, options.MaxDetailFetch);
    }

    private bool IsTotvs()
    {
        return Name.Contains("totvs", StringComparison.OrdinalIgnoreCase) ||
               _source.StartUrl.Contains("totvs", StringComparison.OrdinalIgnoreCase);
    }

    private bool RequiresJsSource()
    {
        return Name.Contains("thoughtworks", StringComparison.OrdinalIgnoreCase) ||
               Name.Contains("redhat", StringComparison.OrdinalIgnoreCase) ||
               Name.Contains("red hat", StringComparison.OrdinalIgnoreCase) ||
               Name.Contains("accenture", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> InferLanguages(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new[] { "pt-BR" };
        }

        return text.Any(c => "ãõçáéíóú".Contains(char.ToLowerInvariant(c)))
            ? new[] { "pt-BR" }
            : new[] { "en" };
    }
}
