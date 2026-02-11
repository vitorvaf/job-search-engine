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

    public async Task RunOnceAsync(IJobSource source, CancellationToken ct)
    {
        // garante Source cadastrado
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
            StartedAt = DateTimeOffset.UtcNow,
            Status = "Running"
        };

        _db.IngestionRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _meili.EnsureIndexAsync(_appOptions.SearchIndexName, ct);

            await foreach (var job in source.FetchAsync(ct))
            {
                run.Fetched++;

                // MVP: já vem normalizado
                run.Parsed++;
                run.Normalized++;

                // Idempotência simples:
                // - se já existe por (SourceName+SourceJobId) quando SourceJobId não é nulo
                // - senão, tenta por fingerprint + sourceUrl
                JobPostingEntity? existing = null;

                if (!string.IsNullOrWhiteSpace(job.Source.SourceJobId))
                {
                    existing = await _db.JobPostings.FirstOrDefaultAsync(
                        x => x.SourceName == job.Source.Name && x.SourceJobId == job.Source.SourceJobId, ct);
                }

                existing ??= await _db.JobPostings.FirstOrDefaultAsync(x => x.SourceUrl == job.Source.Url, ct);
                existing ??= await _db.JobPostings.FirstOrDefaultAsync(x => x.Fingerprint == job.Dedupe.Fingerprint, ct);

                if (existing is null)
                {
                    var entity = job.ToEntity();
                    _db.JobPostings.Add(entity);
                    await _db.SaveChangesAsync(ct);

                    await _meili.UpsertAsync(_appOptions.SearchIndexName, entity.ToSearchDocument(), ct);
                    run.Indexed++;
                }
                else
                {
                    // idempotência: mantém o documento canônico atualizado e renova visibilidade
                    ApplyMutableFields(existing, job);

                    await _db.SaveChangesAsync(ct);
                    run.Duplicates++;
                }
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
                "Run finalizado: source={Source} status={Status} fetched={Fetched} parsed={Parsed} normalized={Normalized} indexed={Indexed} duplicates={Duplicates} errors={Errors}",
                source.Name,
                run.Status,
                run.Fetched,
                run.Parsed,
                run.Normalized,
                run.Indexed,
                run.Duplicates,
                run.Errors);
        }
    }

    private static void ApplyMutableFields(JobPostingEntity entity, Domain.Models.JobPosting incoming)
    {
        entity.SourceType = incoming.Source.Type;
        entity.SourceUrl = incoming.Source.Url;
        entity.SourceJobId = incoming.Source.SourceJobId;
        entity.Title = incoming.Title;
        entity.CompanyName = incoming.Company.Name;
        entity.CompanyWebsite = incoming.Company.Website;
        entity.CompanyIndustry = incoming.Company.Industry;
        entity.LocationText = incoming.LocationText;
        entity.Country = incoming.Location?.Country;
        entity.State = incoming.Location?.State;
        entity.City = incoming.Location?.City;
        entity.WorkMode = incoming.WorkMode;
        entity.Seniority = incoming.Seniority;
        entity.EmploymentType = incoming.EmploymentType;
        entity.SalaryMin = incoming.Salary?.Min;
        entity.SalaryMax = incoming.Salary?.Max;
        entity.SalaryCurrency = incoming.Salary?.Currency;
        entity.SalaryPeriod = incoming.Salary?.Period;
        entity.DescriptionText = incoming.DescriptionText;
        entity.Tags = incoming.Tags.ToArray();
        entity.Languages = incoming.Languages.ToArray();
        entity.PostedAt = incoming.PostedAt;
        entity.CapturedAt = incoming.CapturedAt;
        entity.LastSeenAt = incoming.LastSeenAt;
        entity.Status = incoming.Status;
        entity.Fingerprint = incoming.Dedupe.Fingerprint;
        entity.ClusterId = incoming.Dedupe.ClusterId;
        entity.MetadataJson = JsonSerializer.Serialize(incoming.Metadata, JsonOpts);
    }
}
