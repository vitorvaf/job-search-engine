using Jobs.Domain.Models;
using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Data.Entities;
using Jobs.Infrastructure.Data.Mapping;
using Jobs.Infrastructure.Options;
using Jobs.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Jobs.Infrastructure.Ingestion;

public sealed class IngestionPipeline
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly JobsDbContext _db;
    private readonly MeiliClient _meili;
    private readonly AppOptions _appOptions;
    private readonly ILogger<IngestionPipeline> _logger;

    public IngestionPipeline(
        JobsDbContext db,
        MeiliClient meili,
        IOptions<AppOptions> appOptions,
        ILogger<IngestionPipeline> logger)
    {
        _db = db;
        _meili = meili;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    public async Task RunOnceAsync(IJobSource source, IngestionFetchOptions? options, CancellationToken ct)
    {
        var fetchOptions = ResolveFetchOptions(options);
        var now = DateTimeOffset.UtcNow;
        var src = await _db.Sources.FirstOrDefaultAsync(s => s.Name == source.Name && s.Type == source.Type, ct);
        if (src is null)
        {
            src = new SourceEntity { Id = Guid.NewGuid(), Name = source.Name, Type = source.Type, Enabled = true };
            _db.Sources.Add(src);
            await _db.SaveChangesAsync(ct);
        }

        var run = new IngestionRunEntity
        {
            Id = Guid.NewGuid(),
            SourceId = src.Id,
            StartedAt = now,
            Status = "Running"
        };

        _db.IngestionRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        var inserted = 0;
        var updated = 0;
        var duplicates = 0;

        try
        {
            await _meili.EnsureIndexAsync(_appOptions.SearchIndexName, ct);

            var maxItems = Math.Max(1, fetchOptions.MaxItemsPerRun);
            var seen = 0;

            await foreach (var job in source.FetchAsync(fetchOptions, ct))
            {
                if (seen >= maxItems)
                {
                    break;
                }

                seen++;
                run.Fetched++;
                run.Parsed++;
                run.Normalized++;

                var incoming = WithRunTimestamps(job, now);

                var existing = await FindExistingAsync(incoming, ct);
                if (existing is null)
                {
                    var entity = incoming.ToEntity();
                    _db.JobPostings.Add(entity);
                    await _db.SaveChangesAsync(ct);

                    await _meili.UpsertAsync(_appOptions.SearchIndexName, entity.ToSearchDocument(), ct);
                    run.Indexed++;
                    inserted++;
                    continue;
                }

                var changed = ApplyUpsertRules(existing, incoming);
                existing.LastSeenAt = now;
                existing.Status = JobStatus.Active;

                if (changed)
                {
                    await _db.SaveChangesAsync(ct);
                    await _meili.UpsertAsync(_appOptions.SearchIndexName, existing.ToSearchDocument(), ct);
                    run.Indexed++;
                    updated++;
                }
                else
                {
                    await _db.SaveChangesAsync(ct);
                    run.Duplicates++;
                    duplicates++;
                }
            }

            var expiredCount = await ExpireMissingJobsAsync(source, now, ct);
            if (expiredCount > 0)
            {
                _logger.LogInformation("Fonte {Source} expirou {Count} vagas antigas.", source.Name, expiredCount);
            }

            run.Status = "Success";
        }
        catch (Exception ex)
        {
            run.Status = "Failed";
            run.Errors++;
            run.ErrorSample = ex.Message;
            _logger.LogError(ex, "Ingestion falhou para {Source}", source.Name);
        }
        finally
        {
            run.FinishedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Run finalizado: source={Source} status={Status} fetched={Fetched} parsed={Parsed} normalized={Normalized} indexed={Indexed} inserted={Inserted} updated={Updated} duplicates={Duplicates} errors={Errors}",
                source.Name,
                run.Status,
                run.Fetched,
                run.Parsed,
                run.Normalized,
                run.Indexed,
                inserted,
                updated,
                duplicates,
                run.Errors);
        }
    }

    private async Task<JobPostingEntity?> FindExistingAsync(JobPosting incoming, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(incoming.Source.SourceJobId))
        {
            var bySourceJobId = await _db.JobPostings.FirstOrDefaultAsync(x =>
                x.SourceType == incoming.Source.Type &&
                x.SourceName == incoming.Source.Name &&
                x.SourceJobId == incoming.Source.SourceJobId, ct);
            if (bySourceJobId is not null)
            {
                return bySourceJobId;
            }
        }

        return await _db.JobPostings.FirstOrDefaultAsync(x => x.SourceUrl == incoming.Source.Url, ct);
    }

    private IngestionFetchOptions ResolveFetchOptions(IngestionFetchOptions? options)
    {
        if (options is not null)
        {
            return options;
        }

        return new IngestionFetchOptions(
            MaxItemsPerRun: Math.Max(1, _appOptions.Ingestion.MaxItemsPerRunDefault),
            MaxDetailFetch: Math.Max(0, _appOptions.Ingestion.MaxDetailFetchDefault));
    }

    private static JobPosting WithRunTimestamps(JobPosting sourceJob, DateTimeOffset runAt)
    {
        return new JobPosting
        {
            Id = sourceJob.Id,
            Source = sourceJob.Source,
            Title = sourceJob.Title,
            Company = sourceJob.Company,
            LocationText = sourceJob.LocationText,
            Location = sourceJob.Location,
            WorkMode = sourceJob.WorkMode,
            Seniority = sourceJob.Seniority,
            EmploymentType = sourceJob.EmploymentType,
            Salary = sourceJob.Salary,
            DescriptionText = sourceJob.DescriptionText,
            Tags = sourceJob.Tags,
            Languages = sourceJob.Languages,
            PostedAt = sourceJob.PostedAt,
            CapturedAt = runAt,
            LastSeenAt = runAt,
            Status = JobStatus.Active,
            Dedupe = sourceJob.Dedupe,
            Metadata = sourceJob.Metadata
        };
    }

    private static bool ApplyUpsertRules(JobPostingEntity entity, JobPosting incoming)
    {
        var changed = false;

        entity.SourceType = incoming.Source.Type;
        entity.SourceName = incoming.Source.Name;
        entity.SourceUrl = incoming.Source.Url;
        entity.SourceJobId = incoming.Source.SourceJobId;

        if (IsBetterText(entity.DescriptionText, incoming.DescriptionText))
        {
            entity.DescriptionText = incoming.DescriptionText;
            changed = true;
        }

        var mergedTags = MergeTags(entity.Tags, incoming.Tags);
        if (!entity.Tags.SequenceEqual(mergedTags))
        {
            entity.Tags = mergedTags;
            changed = true;
        }

        if (ShouldReplaceSalary(entity, incoming.Salary))
        {
            entity.SalaryMin = incoming.Salary?.Min;
            entity.SalaryMax = incoming.Salary?.Max;
            entity.SalaryCurrency = incoming.Salary?.Currency;
            entity.SalaryPeriod = incoming.Salary?.Period;
            changed = true;
        }

        if (incoming.PostedAt is not null && (entity.PostedAt is null || incoming.PostedAt < entity.PostedAt))
        {
            entity.PostedAt = incoming.PostedAt;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(incoming.Dedupe.Fingerprint) && incoming.Dedupe.Fingerprint != entity.Fingerprint)
        {
            entity.Fingerprint = incoming.Dedupe.Fingerprint;
            changed = true;
        }

        if (entity.CapturedAt != incoming.CapturedAt)
        {
            entity.CapturedAt = incoming.CapturedAt;
            changed = true;
        }

        entity.MetadataJson = JsonSerializer.Serialize(incoming.Metadata, JsonOpts);

        return changed;
    }

    private static bool IsBetterText(string? current, string? incoming)
    {
        return !string.IsNullOrWhiteSpace(incoming) &&
               (string.IsNullOrWhiteSpace(current) || incoming.Length > current.Length + 40);
    }

    private static string[] MergeTags(string[] current, IReadOnlyList<string> incoming)
    {
        return current
            .Concat(incoming)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ShouldReplaceSalary(JobPostingEntity current, SalaryRange? incoming)
    {
        if (incoming is null)
        {
            return false;
        }

        var currentFilled = (current.SalaryMin is not null ? 1 : 0) +
                            (current.SalaryMax is not null ? 1 : 0) +
                            (!string.IsNullOrWhiteSpace(current.SalaryCurrency) ? 1 : 0) +
                            (!string.IsNullOrWhiteSpace(current.SalaryPeriod) ? 1 : 0);

        var incomingFilled = (incoming.Min is not null ? 1 : 0) +
                             (incoming.Max is not null ? 1 : 0) +
                             (!string.IsNullOrWhiteSpace(incoming.Currency) ? 1 : 0) +
                             (!string.IsNullOrWhiteSpace(incoming.Period) ? 1 : 0);

        return incomingFilled > currentFilled;
    }

    private async Task<int> ExpireMissingJobsAsync(IJobSource source, DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-Math.Max(1, _appOptions.Ingestion.ExpireAfterDays));

        var stale = await _db.JobPostings
            .Where(x => x.SourceType == source.Type && x.SourceName == source.Name)
            .Where(x => x.Status != JobStatus.Expired && x.LastSeenAt < cutoff)
            .ToListAsync(ct);

        if (stale.Count == 0)
        {
            return 0;
        }

        foreach (var item in stale)
        {
            item.Status = JobStatus.Expired;
        }

        await _db.SaveChangesAsync(ct);

        foreach (var item in stale)
        {
            await _meili.UpsertAsync(_appOptions.SearchIndexName, item.ToSearchDocument(), ct);
        }

        return stale.Count;
    }
}
