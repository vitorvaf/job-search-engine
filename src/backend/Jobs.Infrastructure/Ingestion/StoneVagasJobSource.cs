using Jobs.Domain.Models;
using Jobs.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobs.Infrastructure.Ingestion;

public sealed class StoneVagasJobSource : IJobSource
{
    public string Name => "Stone";
    public SourceType Type => SourceType.Vagas;

    private readonly AppOptions _opts;
    private readonly Fingerprint _fingerprint;
    private readonly ILogger<StoneVagasJobSource> _logger;
    private readonly IHttpClientFactory _httpFactory;

    public StoneVagasJobSource(
        IOptions<AppOptions> opts,
        Fingerprint fingerprint,
        ILogger<StoneVagasJobSource> logger,
        IHttpClientFactory httpFactory)
    {
        _opts = opts.Value;
        _fingerprint = fingerprint;
        _logger = logger;
        _httpFactory = httpFactory;
    }

    public async IAsyncEnumerable<JobPosting> FetchAsync(
        IngestionFetchOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (!_opts.Sources.Stone.Enabled)
        {
            yield break;
        }

        var searchUrl = _opts.Sources.Stone.SearchUrl;
        if (string.IsNullOrWhiteSpace(searchUrl))
        {
            _logger.LogWarning("Sources:Stone:SearchUrl não configurada.");
            yield break;
        }

        var sourceHttp = new SourceHttpClient(_httpFactory.CreateClient("Sources"), _logger);

        var listHtml = await sourceHttp.TryGetStringAsync(searchUrl, Name, ct);
        if (string.IsNullOrWhiteSpace(listHtml))
        {
            yield break;
        }

        var parsed = InfoJobsHtmlParser.ParseList(listHtml, searchUrl)
            .Take(Math.Max(1, options.MaxItemsPerRun))
            .ToList();

        var detailBudget = Math.Max(0, options.MaxDetailFetch);

        foreach (var item in parsed)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(item.DescriptionText) && detailBudget > 0)
            {
                var detailHtml = await sourceHttp.TryGetStringAsync(item.Url, Name, ct);
                if (!string.IsNullOrWhiteSpace(detailHtml))
                {
                    item.DescriptionText = InfoJobsHtmlParser.ParseDetailDescription(detailHtml);
                }

                detailBudget--;
            }

            var sourceJobId = InfoJobsHtmlParser.BuildStableSourceJobId(item.SourceJobId, item.Url);
            var workMode = InfoJobsHtmlParser.InferWorkMode($"{item.WorkModeText} {item.LocationText} {item.DescriptionText}");
            var salary = InfoJobsHtmlParser.ParseSalary(item.SalaryText);

            yield return new JobPosting
            {
                Source = new JobSourceRef(Name, Type, item.Url, sourceJobId),
                Title = item.Title,
                Company = new CompanyRef(string.IsNullOrWhiteSpace(item.Company) ? "Stone" : item.Company),
                LocationText = item.LocationText,
                WorkMode = workMode,
                Salary = new SalaryRange(salary.Min, salary.Max, salary.Currency, salary.Period),
                DescriptionText = item.DescriptionText ?? string.Empty,
                Tags = SourceTagInferer.Infer(item.Title, item.DescriptionText),
                Languages = new[] { "pt-BR" },
                PostedAt = item.PostedAt,
                CapturedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
                Status = JobStatus.Active,
                Dedupe = new DedupeInfo(
                    _fingerprint.Compute(item.Company, item.Title, item.LocationText, workMode.ToString()),
                    null),
                Metadata = new Dictionary<string, object>
                {
                    ["source"] = Name,
                    ["salaryText"] = item.SalaryText ?? string.Empty,
                    ["searchUrl"] = searchUrl
                }
            };
        }
    }
}
