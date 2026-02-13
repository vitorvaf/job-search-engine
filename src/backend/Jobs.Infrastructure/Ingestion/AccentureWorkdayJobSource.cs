using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Jobs.Domain.Models;
using Jobs.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobs.Infrastructure.Ingestion;

public sealed class AccentureWorkdayJobSource : IJobSource
{
    private sealed class HostThrottle
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public DateTimeOffset NextAllowedAt { get; set; } = DateTimeOffset.MinValue;
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, HostThrottle> Throttles = new(StringComparer.OrdinalIgnoreCase);

    private readonly AccentureWorkdaySourceOptions _source;
    private readonly Fingerprint _fingerprint;
    private readonly ILogger<AccentureWorkdayJobSource> _logger;
    private readonly IHttpClientFactory _httpFactory;

    public AccentureWorkdayJobSource(
        IOptions<AppOptions> opts,
        Fingerprint fingerprint,
        ILogger<AccentureWorkdayJobSource> logger,
        IHttpClientFactory httpFactory)
    {
        _source = opts.Value.Sources.AccentureWorkday;
        _fingerprint = fingerprint;
        _logger = logger;
        _httpFactory = httpFactory;
    }

    public string Name => "AccentureWorkday";
    public SourceType Type => SourceType.Workday;

    public async IAsyncEnumerable<JobPosting> FetchAsync(
        IngestionFetchOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (!_source.Enabled)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(_source.BaseHost) ||
            string.IsNullOrWhiteSpace(_source.Tenant) ||
            string.IsNullOrWhiteSpace(_source.SiteName))
        {
            _logger.LogWarning("Fonte {Source} sem BaseHost/Tenant/SiteName configurados.", Name);
            yield break;
        }

        var client = _httpFactory.CreateClient("Sources");
        var pageSize = Math.Max(1, _source.PageSize);
        var maxPages = Math.Max(1, _source.MaxPagesPerRun);
        var maxItems = Math.Max(1, options.MaxItemsPerRun);
        var detailBudget = ResolveDetailBudget(options);
        var baseHost = _source.BaseHost.Trim();
        var listPath = $"/wday/cxs/{_source.Tenant.Trim()}/{_source.SiteName.Trim()}/jobs";
        var listUri = BuildAbsoluteUri(baseHost, listPath);

        var counters = new
        {
            fetched = 0,
            parsed = 0,
            detailFetched = 0,
            errors = 0
        };
        var fetched = counters.fetched;
        var parsed = counters.parsed;
        var detailFetched = counters.detailFetched;
        var errors = counters.errors;

        for (var page = 1; page <= maxPages; page++)
        {
            if (parsed >= maxItems)
            {
                break;
            }

            var offset = (page - 1) * pageSize;
            var requestBody = JsonSerializer.Serialize(new
            {
                appliedFacets = new Dictionary<string, string[]>(),
                limit = pageSize,
                offset,
                searchText = ""
            }, JsonOpts);

            var listResponse = await TrySendJsonAsync(client, HttpMethod.Post, listUri, requestBody, ct);
            if (listResponse.BlockedOrUnauthorized)
            {
                _logger.LogWarning(
                    "Fonte {Source} blocked/unauthorized no endpoint Workday {Endpoint}. status={Status}",
                    Name,
                    listPath,
                    listResponse.StatusCode is null ? "n/a" : ((int)listResponse.StatusCode.Value).ToString());
                break;
            }

            if (string.IsNullOrWhiteSpace(listResponse.Body))
            {
                errors++;
                continue;
            }

            IReadOnlyList<WorkdayJobListItem> jobs;
            try
            {
                jobs = WorkdayJobsJsonParser.ParseListing(
                    listResponse.Body,
                    baseHost,
                    _source.SitePath,
                    _source.SiteName);
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogWarning(ex, "Erro parseando listagem Workday em {Source} page={Page}", Name, page);
                continue;
            }

            if (jobs.Count == 0)
            {
                break;
            }

            fetched += jobs.Count;

            foreach (var item in jobs)
            {
                ct.ThrowIfCancellationRequested();
                if (parsed >= maxItems)
                {
                    break;
                }

                var description = item.DescriptionText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(description) && detailBudget > 0)
                {
                    var detailPath = WorkdayJobsJsonParser.BuildDetailEndpointPath(
                        _source.Tenant.Trim(),
                        _source.SiteName.Trim(),
                        item.ExternalPath ?? string.Empty,
                        item.SourceJobId);
                    var detailUri = BuildAbsoluteUri(baseHost, detailPath);
                    var detailResponse = await TrySendJsonAsync(client, HttpMethod.Get, detailUri, null, ct);
                    if (detailResponse.BlockedOrUnauthorized)
                    {
                        _logger.LogWarning(
                            "Fonte {Source} blocked/unauthorized no detalhe Workday. status={Status}",
                            Name,
                            detailResponse.StatusCode is null ? "n/a" : ((int)detailResponse.StatusCode.Value).ToString());
                    }
                    else if (!string.IsNullOrWhiteSpace(detailResponse.Body))
                    {
                        description = WorkdayJobsJsonParser.ParseDetailDescription(detailResponse.Body);
                        detailFetched++;
                    }
                    else
                    {
                        errors++;
                    }

                    detailBudget--;
                }

                var workMode = InfoJobsHtmlParser.InferWorkMode($"{item.LocationText} {description}");
                var employmentType = ParseEmploymentType(item.EmploymentTypeText);
                parsed++;

                yield return new JobPosting
                {
                    Source = new JobSourceRef(Name, Type, item.SourceUrl, item.SourceJobId),
                    Title = item.Title,
                    Company = new CompanyRef("Accenture"),
                    LocationText = item.LocationText,
                    WorkMode = workMode,
                    EmploymentType = employmentType,
                    DescriptionText = description,
                    Tags = SourceTagInferer.Infer(item.Title, description),
                    Languages = new[] { "pt-BR", "en-US" },
                    PostedAt = item.PostedAt,
                    CapturedAt = DateTimeOffset.UtcNow,
                    LastSeenAt = DateTimeOffset.UtcNow,
                    Status = JobStatus.Active,
                    Dedupe = new DedupeInfo(
                        _fingerprint.Compute("Accenture", item.Title, item.LocationText, workMode.ToString()),
                        null),
                    Metadata = new Dictionary<string, object>
                    {
                        ["source"] = Name,
                        ["sourceType"] = "Workday",
                        ["tenant"] = _source.Tenant,
                        ["siteName"] = _source.SiteName,
                        ["sitePath"] = _source.SitePath,
                        ["listEndpointPath"] = listPath
                    }
                };
            }
        }

        _logger.LogInformation(
            "{Source} counters: fetched={Fetched} parsed={Parsed} detailFetched={DetailFetched} errors={Errors}",
            Name,
            fetched,
            parsed,
            detailFetched,
            errors);
    }

    private int ResolveDetailBudget(IngestionFetchOptions options)
    {
        var runBudget = Math.Max(0, options.MaxDetailFetch);
        if (_source.MaxDetailFetch is null)
        {
            return runBudget;
        }

        return Math.Min(runBudget, Math.Max(0, _source.MaxDetailFetch.Value));
    }

    private Uri BuildAbsoluteUri(string baseHost, string path)
    {
        var normalizedPath = path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
        return new Uri($"https://{baseHost}{normalizedPath}");
    }

    private async Task<(string? Body, HttpStatusCode? StatusCode, bool BlockedOrUnauthorized)> TrySendJsonAsync(
        HttpClient client,
        HttpMethod method,
        Uri uri,
        string? jsonBody,
        CancellationToken ct)
    {
        await EnforceRateLimitAsync(uri.Host, ct);
        using var req = new HttpRequestMessage(method, uri);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.UserAgent.Clear();
        req.Headers.UserAgent.ParseAdd(string.IsNullOrWhiteSpace(_source.UserAgent) ? "JobSearchEngineBot/0.1 (contact: unknown)" : _source.UserAgent);

        if (jsonBody is not null)
        {
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        try
        {
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            var blockedOrUnauthorized = resp.StatusCode == HttpStatusCode.Unauthorized ||
                                        resp.StatusCode == HttpStatusCode.Forbidden ||
                                        resp.StatusCode == (HttpStatusCode)429;

            if (blockedOrUnauthorized)
            {
                return (null, resp.StatusCode, true);
            }

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Falha HTTP na fonte {Source}. method={Method} url={Url} status={Status}",
                    Name,
                    method.Method,
                    uri,
                    (int)resp.StatusCode);
                return (null, resp.StatusCode, false);
            }

            return (body, resp.StatusCode, false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro HTTP na fonte {Source}. method={Method} url={Url}", Name, method.Method, uri);
            return (null, null, false);
        }
    }

    private static async Task EnforceRateLimitAsync(string host, CancellationToken ct)
    {
        var throttle = Throttles.GetOrAdd(host, _ => new HostThrottle());
        await throttle.Lock.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var wait = throttle.NextAllowedAt - now;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, ct);
            }

            throttle.NextAllowedAt = DateTimeOffset.UtcNow.AddSeconds(1);
        }
        finally
        {
            throttle.Lock.Release();
        }
    }

    private static EmploymentType ParseEmploymentType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return EmploymentType.Unknown;
        }

        var normalized = JobTextNormalizer.Normalize(raw);
        if (normalized.Contains("intern", StringComparison.Ordinal))
        {
            return EmploymentType.Internship;
        }

        if (normalized.Contains("temporary", StringComparison.Ordinal) ||
            normalized.Contains("temp", StringComparison.Ordinal))
        {
            return EmploymentType.Temporary;
        }

        if (normalized.Contains("contract", StringComparison.Ordinal))
        {
            return EmploymentType.Contractor;
        }

        return EmploymentType.Unknown;
    }
}
