using System.Text.Json;
using Jobs.Domain.Models;
using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Data.Entities;
using Jobs.Infrastructure.Data.Mapping;
using Jobs.Infrastructure.Ingestion;
using Jobs.Infrastructure.Options;
using Jobs.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobs.Infrastructure.BulkIngestion;

/// <summary>
/// Application service that processes bulk job ingestion requests from external systems.
/// Validates items, applies idempotency rules, persists to Postgres, and indexes in Meilisearch.
/// </summary>
public sealed class BulkJobIngestionService
{
    private const int MaxBatchSize = 100;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly JobsDbContext _db;
    private readonly MeiliClient _meili;
    private readonly Fingerprint _fingerprint;
    private readonly AppOptions _appOptions;
    private readonly ILogger<BulkJobIngestionService> _logger;

    public BulkJobIngestionService(
        JobsDbContext db,
        MeiliClient meili,
        Fingerprint fingerprint,
        IOptions<AppOptions> appOptions,
        ILogger<BulkJobIngestionService> logger)
    {
        _db = db;
        _meili = meili;
        _fingerprint = fingerprint;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Validates the API key from the request header against the configured value.
    /// Returns true if the key is valid or if no key is configured (open dev mode).
    /// </summary>
    public bool IsApiKeyValid(string? providedKey)
    {
        var configuredKey = _appOptions.Ingestion.ApiKey;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return true; // Not configured — open in dev
        }

        return string.Equals(providedKey, configuredKey, StringComparison.Ordinal);
    }

    /// <summary>Processes a bulk ingestion request and returns a summary of results.</summary>
    public async Task<BulkIngestionResponse> ProcessAsync(BulkIngestionRequest request, CancellationToken ct)
    {
        var response = new BulkIngestionResponse { Received = request.Items.Count };

        _logger.LogInformation(
            "Bulk ingestion iniciada: source={Source} type={Type} items={Count}",
            request.SourceName, request.SourceType, request.Items.Count);

        await _meili.EnsureIndexAsync(_appOptions.SearchIndexName, ct);

        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var validationError = ValidateItem(item);

            if (validationError is not null)
            {
                response.Invalid++;
                response.Errors.Add(new BulkIngestionItemError
                {
                    Index = i,
                    SourceJobId = item.SourceJobId,
                    Message = validationError
                });
                _logger.LogWarning("Item inválido no índice {Index} (sourceJobId={SourceJobId}): {Error}",
                    i, item.SourceJobId, validationError);
                continue;
            }

            try
            {
                var result = await ProcessItemAsync(request, item, now, ct);
                response.Processed++;

                switch (result)
                {
                    case ProcessResult.Inserted: response.Inserted++; break;
                    case ProcessResult.Updated: response.Updated++; break;
                    case ProcessResult.Duplicate: response.Duplicates++; break;
                }
            }
            catch (Exception ex)
            {
                response.Invalid++;
                response.Errors.Add(new BulkIngestionItemError
                {
                    Index = i,
                    SourceJobId = item.SourceJobId,
                    Message = ex.Message
                });
                _logger.LogError(ex,
                    "Erro ao processar item {Index} (sourceJobId={SourceJobId})",
                    i, item.SourceJobId);
            }
        }

        _logger.LogInformation(
            "Bulk ingestion finalizada: source={Source} received={Received} processed={Processed} inserted={Inserted} updated={Updated} duplicates={Duplicates} invalid={Invalid}",
            request.SourceName,
            response.Received,
            response.Processed,
            response.Inserted,
            response.Updated,
            response.Duplicates,
            response.Invalid);

        return response;
    }

    private async Task<ProcessResult> ProcessItemAsync(
        BulkIngestionRequest request,
        BulkIngestionItemRequest item,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var sourceName = item.SourceName ?? request.SourceName;
        var sourceTypeStr = item.SourceType ?? request.SourceType;
        var sourceType = ParseSourceType(sourceTypeStr);

        // Resolve effective sourceUrl — fall back to originUrl if sourceUrl is absent
        var effectiveSourceUrl = !string.IsNullOrWhiteSpace(item.SourceUrl)
            ? item.SourceUrl
            : item.OriginUrl!;

        var fp = _fingerprint.Compute(
            item.Company!.Name!,
            item.Title!,
            item.LocationText ?? string.Empty,
            item.WorkMode ?? string.Empty);

        var metadata = BuildMetadata(item);

        var existing = await FindExistingAsync(sourceName, sourceType, item, fp, ct);

        if (existing is null)
        {
            var entity = new JobPostingEntity
            {
                Id = Guid.NewGuid(),
                SourceName = sourceName,
                SourceType = sourceType,
                SourceUrl = effectiveSourceUrl,
                SourceJobId = item.SourceJobId,
                OriginUrl = NullIfEmpty(item.OriginUrl),
                Title = item.Title!,
                CompanyName = item.Company!.Name!,
                CompanyWebsite = NullIfEmpty(item.Company.Website),
                CompanyIndustry = NullIfEmpty(item.Company.Industry),
                LocationText = item.LocationText ?? string.Empty,
                WorkMode = ParseEnum<WorkMode>(item.WorkMode),
                Seniority = ParseEnum<Seniority>(item.Seniority),
                EmploymentType = ParseEnum<EmploymentType>(item.EmploymentType),
                DescriptionText = item.DescriptionText ?? string.Empty,
                Tags = NormalizeTags(item.Tags),
                Languages = item.Languages?.ToArray() ?? Array.Empty<string>(),
                PostedAt = item.PostedAt,
                CapturedAt = now,
                LastSeenAt = now,
                Status = JobStatus.Active,
                Fingerprint = fp,
                MetadataJson = JsonSerializer.Serialize(metadata, JsonOpts)
            };

            _db.JobPostings.Add(entity);
            await _db.SaveChangesAsync(ct);
            await _meili.UpsertAsync(_appOptions.SearchIndexName, entity.ToSearchDocument(), ct);

            return ProcessResult.Inserted;
        }

        // Upsert: enrich existing entity
        var changed = ApplyEnrichmentRules(existing, item, sourceName, sourceType, effectiveSourceUrl, metadata, now);
        existing.LastSeenAt = now;
        existing.Status = JobStatus.Active;

        await _db.SaveChangesAsync(ct);

        if (changed)
        {
            await _meili.UpsertAsync(_appOptions.SearchIndexName, existing.ToSearchDocument(), ct);
            return ProcessResult.Updated;
        }

        return ProcessResult.Duplicate;
    }

    private async Task<JobPostingEntity?> FindExistingAsync(
        string sourceName,
        SourceType sourceType,
        BulkIngestionItemRequest item,
        string fingerprint,
        CancellationToken ct)
    {
        // Priority 1: originUrl
        if (!string.IsNullOrWhiteSpace(item.OriginUrl))
        {
            var byOrigin = await _db.JobPostings
                .FirstOrDefaultAsync(x => x.OriginUrl == item.OriginUrl, ct);
            if (byOrigin is not null)
            {
                return byOrigin;
            }
        }

        // Priority 2: sourceJobId (scoped to source)
        if (!string.IsNullOrWhiteSpace(item.SourceJobId))
        {
            var bySourceJobId = await _db.JobPostings
                .FirstOrDefaultAsync(x =>
                    x.SourceType == sourceType &&
                    x.SourceName == sourceName &&
                    x.SourceJobId == item.SourceJobId, ct);
            if (bySourceJobId is not null)
            {
                return bySourceJobId;
            }
        }

        // Priority 3: sourceUrl
        var effectiveSourceUrl = !string.IsNullOrWhiteSpace(item.SourceUrl)
            ? item.SourceUrl
            : item.OriginUrl;

        if (!string.IsNullOrWhiteSpace(effectiveSourceUrl))
        {
            var bySourceUrl = await _db.JobPostings
                .FirstOrDefaultAsync(x => x.SourceUrl == effectiveSourceUrl, ct);
            if (bySourceUrl is not null)
            {
                return bySourceUrl;
            }
        }

        // Priority 4: fingerprint fallback
        return await _db.JobPostings
            .FirstOrDefaultAsync(x => x.Fingerprint == fingerprint, ct);
    }

    private static bool ApplyEnrichmentRules(
        JobPostingEntity entity,
        BulkIngestionItemRequest item,
        string sourceName,
        SourceType sourceType,
        string effectiveSourceUrl,
        Dictionary<string, object> incomingMetadata,
        DateTimeOffset now)
    {
        var changed = false;

        // Update source tracking fields (always)
        entity.SourceName = sourceName;
        entity.SourceType = sourceType;
        entity.SourceUrl = effectiveSourceUrl;
        entity.SourceJobId ??= item.SourceJobId;

        if (NullIfEmpty(item.OriginUrl) is { } originUrl && entity.OriginUrl is null)
        {
            entity.OriginUrl = originUrl;
            changed = true;
        }

        // Enrich description — don't overwrite a good description with a shorter/empty one
        if (IsBetterText(entity.DescriptionText, item.DescriptionText))
        {
            entity.DescriptionText = item.DescriptionText!;
            changed = true;
        }

        // Tags: merge, never replace existing tags with empty list
        if (item.Tags is { Count: > 0 })
        {
            var merged = MergeTags(entity.Tags, item.Tags);
            if (!entity.Tags.SequenceEqual(merged))
            {
                entity.Tags = merged;
                changed = true;
            }
        }

        // Enrich optional fields only when currently empty
        if (string.IsNullOrWhiteSpace(entity.CompanyWebsite) && !string.IsNullOrWhiteSpace(item.Company?.Website))
        {
            entity.CompanyWebsite = item.Company.Website;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(entity.CompanyIndustry) && !string.IsNullOrWhiteSpace(item.Company?.Industry))
        {
            entity.CompanyIndustry = item.Company.Industry;
            changed = true;
        }

        if (entity.WorkMode == WorkMode.Unknown && !string.IsNullOrWhiteSpace(item.WorkMode))
        {
            entity.WorkMode = ParseEnum<WorkMode>(item.WorkMode);
            changed = true;
        }

        if (entity.Seniority == Seniority.Unknown && !string.IsNullOrWhiteSpace(item.Seniority))
        {
            entity.Seniority = ParseEnum<Seniority>(item.Seniority);
            changed = true;
        }

        if (entity.EmploymentType == EmploymentType.Unknown && !string.IsNullOrWhiteSpace(item.EmploymentType))
        {
            entity.EmploymentType = ParseEnum<EmploymentType>(item.EmploymentType);
            changed = true;
        }

        if (entity.PostedAt is null && item.PostedAt is not null)
        {
            entity.PostedAt = item.PostedAt;
            changed = true;
        }

        // Merge metadata dictionaries
        var existingMeta = DeserializeMetadata(entity.MetadataJson);
        foreach (var (key, value) in incomingMetadata)
        {
            existingMeta[key] = value;
        }

        entity.MetadataJson = JsonSerializer.Serialize(existingMeta, JsonOpts);

        if (!changed)
        {
            // LastSeenAt is always updated — still counts as a save but not a "changed" document
            entity.CapturedAt = now;
        }

        return changed;
    }

    private static string? ValidateItem(BulkIngestionItemRequest item)
    {
        if (string.IsNullOrWhiteSpace(item.Title))
        {
            return "title is required";
        }

        if (item.Company is null || string.IsNullOrWhiteSpace(item.Company.Name))
        {
            return "company.name is required";
        }

        if (string.IsNullOrWhiteSpace(item.SourceUrl) && string.IsNullOrWhiteSpace(item.OriginUrl))
        {
            return "sourceUrl or originUrl is required";
        }

        return null;
    }

    private static Dictionary<string, object> BuildMetadata(BulkIngestionItemRequest item)
    {
        var meta = item.Metadata is not null
            ? new Dictionary<string, object>(item.Metadata)
            : new Dictionary<string, object>();

        // Always store originUrl in metadata for traceability
        if (!string.IsNullOrWhiteSpace(item.OriginUrl))
        {
            meta["originUrl"] = item.OriginUrl;
        }

        return meta;
    }

    private static bool IsBetterText(string? current, string? incoming) =>
        !string.IsNullOrWhiteSpace(incoming) &&
        (string.IsNullOrWhiteSpace(current) || incoming.Length > current.Length + 40);

    private static string[] MergeTags(string[] current, IEnumerable<string> incoming) =>
        current
            .Concat(incoming)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string[] NormalizeTags(IEnumerable<string>? tags) =>
        tags?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static TEnum ParseEnum<TEnum>(string? raw) where TEnum : struct, Enum =>
        !string.IsNullOrWhiteSpace(raw) && Enum.TryParse<TEnum>(raw, true, out var parsed)
            ? parsed
            : default;

    private static SourceType ParseSourceType(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && Enum.TryParse<SourceType>(raw, true, out var parsed)
            ? parsed
            : SourceType.ExternalIngestion;

    private static Dictionary<string, object> DeserializeMetadata(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOpts)
                   ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    private enum ProcessResult { Inserted, Updated, Duplicate }
}
