using Jobs.Domain.Models;
using Jobs.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobs.Infrastructure.Ingestion;

public sealed class InfoJobsJobSource : IJobSource
{
    public string Name => "InfoJobs";
    public SourceType Type => SourceType.InfoJobs;

    private readonly AppOptions _opts;
    private readonly Fingerprint _fingerprint;
    private readonly ILogger<InfoJobsJobSource> _logger;
    private readonly IHttpClientFactory _httpFactory;

    public InfoJobsJobSource(
        IOptions<AppOptions> opts,
        Fingerprint fingerprint,
        ILogger<InfoJobsJobSource> logger,
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
        if (!_opts.Sources.InfoJobs.Enabled)
        {
            _logger.LogInformation("Fonte InfoJobs desabilitada por configuração.");
            yield break;
        }

        var searchUrl = _opts.Sources.InfoJobs.SearchUrl;
        if (string.IsNullOrWhiteSpace(searchUrl))
        {
            _logger.LogWarning("Sources:InfoJobs:SearchUrl não configurada.");
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

        if (parsed.Count == 0)
        {
            _logger.LogWarning("InfoJobs retornou 0 vagas parseadas para {Url}", searchUrl);
            yield break;
        }

        var detailBudget = Math.Max(0, options.MaxDetailFetch);
        var skippedInvalid = 0;
        foreach (var item in parsed)
        {
            ct.ThrowIfCancellationRequested();

            if (!PassesQualityGate(item))
            {
                skippedInvalid++;
                _logger.LogWarning(
                    "InfoJobs descartou vaga inválida: title='{Title}' company='{Company}' location='{Location}' url='{Url}'",
                    item.Title,
                    item.Company,
                    item.LocationText,
                    item.Url);
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.DescriptionText) &&
                detailBudget > 0 &&
                !string.IsNullOrWhiteSpace(item.Url))
            {
                var detailHtml = await sourceHttp.TryGetStringAsync(item.Url, Name, ct);
                if (!string.IsNullOrWhiteSpace(detailHtml))
                {
                    item.DescriptionText = InfoJobsHtmlParser.ParseDetailDescription(detailHtml);
                }

                detailBudget--;
            }

            var sourceJobId = InfoJobsHtmlParser.ExtractInfoJobsJobIdFromUrl(item.Url)
                              ?? InfoJobsHtmlParser.BuildStableSourceJobId(item.SourceJobId, item.Url);
            var workMode = InfoJobsHtmlParser.InferWorkMode($"{item.WorkModeText} {item.LocationText} {item.DescriptionText}");
            var salary = InfoJobsHtmlParser.ParseSalary(item.SalaryText);

            yield return new JobPosting
            {
                Source = new JobSourceRef(Name, Type, item.Url, sourceJobId),
                Title = item.Title,
                Company = new CompanyRef(item.Company),
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

        if (skippedInvalid > 0)
        {
            _logger.LogInformation("InfoJobs quality gate: SkippedInvalid={SkippedInvalid}", skippedInvalid);
        }
    }

    private static bool PassesQualityGate(ParsedSourceJob item)
    {
        if (item.Title.Trim().Length < 6)
        {
            return false;
        }

        if (string.Equals(item.Company?.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(item.LocationText);
    }
}
