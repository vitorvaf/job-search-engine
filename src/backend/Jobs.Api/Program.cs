using Jobs.Infrastructure;
using Jobs.Domain.Models;
using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Options;
using Jobs.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddJobsInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
    await db.Database.EnsureCreatedAsync();

    var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>();
    var meili = scope.ServiceProvider.GetRequiredService<MeiliClient>();
    await meili.EnsureIndexAsync(appOptions.Value.SearchIndexName, CancellationToken.None);
}

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/sources", async (JobsDbContext db, CancellationToken ct) =>
{
    var sources = await db.Sources
        .OrderBy(s => s.Name)
        .Select(s => new { s.Id, s.Name, Type = s.Type.ToString(), s.BaseUrl, s.Enabled })
        .ToListAsync(ct);

    return Results.Ok(sources);
});

app.MapGet("/api/jobs/{id:guid}", async (Guid id, JobsDbContext db, CancellationToken ct) =>
{
    var job = await db.JobPostings.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (job is null) return Results.NotFound();

    var metadata = TryParseJson(job.MetadataJson);

    return Results.Ok(new
    {
        id = job.Id,
        title = job.Title,
        company = new { name = job.CompanyName, website = job.CompanyWebsite, industry = job.CompanyIndustry },
        locationText = job.LocationText,
        location = new { country = job.Country, state = job.State, city = job.City },
        workMode = job.WorkMode.ToString(),
        seniority = job.Seniority.ToString(),
        employmentType = job.EmploymentType.ToString(),
        salary = job.SalaryMin is null && job.SalaryMax is null
            ? null
            : new { min = job.SalaryMin, max = job.SalaryMax, currency = job.SalaryCurrency, period = job.SalaryPeriod },
        descriptionText = job.DescriptionText,
        tags = job.Tags,
        languages = job.Languages,
        source = new { name = job.SourceName, type = job.SourceType.ToString(), url = job.SourceUrl, sourceJobId = job.SourceJobId },
        postedAt = job.PostedAt,
        capturedAt = job.CapturedAt,
        lastSeenAt = job.LastSeenAt,
        status = job.Status.ToString(),
        dedupe = new { fingerprint = job.Fingerprint, clusterId = job.ClusterId },
        metadata
    });
});

app.MapGet("/api/jobs", async (
    string? q,
    string? tags,
    string? workMode,
    string? seniority,
    string? employmentType,
    string? sourceName,
    string? company,
    string? location,
    DateTime? postedFrom,
    string? sort,
    int page,
    int pageSize,
    MeiliClient meili,
    IOptions<AppOptions> appOptions,
    CancellationToken ct) =>
{
    page = page <= 0 ? 1 : page;
    pageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

    var parsedTags = tags?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(t => t.ToLowerInvariant())
        .Distinct()
        .ToArray() ?? Array.Empty<string>();

    var filters = new List<string>();

    if (TryParseEnum(workMode, out WorkMode workModeFilter) && workModeFilter != WorkMode.Unknown)
    {
        filters.Add($"workMode = \"{workModeFilter}\"");
    }

    if (TryParseEnum(seniority, out Seniority seniorityFilter) && seniorityFilter != Seniority.Unknown)
    {
        filters.Add($"seniority = \"{seniorityFilter}\"");
    }

    if (TryParseEnum(employmentType, out EmploymentType employmentTypeFilter) && employmentTypeFilter != EmploymentType.Unknown)
    {
        filters.Add($"employmentType = \"{employmentTypeFilter}\"");
    }

    if (parsedTags.Length > 0)
    {
        var tagExpr = string.Join(" OR ", parsedTags.Select(t => $"tags = \"{EscapeFilterValue(t)}\""));
        filters.Add($"({tagExpr})");
    }

    if (!string.IsNullOrWhiteSpace(company))
    {
        filters.Add($"company = \"{EscapeFilterValue(company)}\"");
    }

    if (!string.IsNullOrWhiteSpace(location))
    {
        filters.Add($"locationText = \"{EscapeFilterValue(location)}\"");
    }

    if (!string.IsNullOrWhiteSpace(sourceName))
    {
        filters.Add($"sourceName = \"{EscapeFilterValue(sourceName)}\"");
    }

    if (postedFrom is not null)
    {
        var floor = postedFrom.Value.Date.ToString("yyyy-MM-dd");
        filters.Add($"postedAt >= \"{floor}\"");
    }

    await meili.EnsureIndexAsync(appOptions.Value.SearchIndexName, ct);

    var sortOrder = ResolveSort(sort);

    var raw = await meili.SearchAsync(appOptions.Value.SearchIndexName, new
    {
        q = q ?? string.Empty,
        filter = filters.Count > 0 ? string.Join(" AND ", filters) : null,
        sort = sortOrder,
        offset = (page - 1) * pageSize,
        limit = pageSize
    }, ct);

    var hits = raw.GetProperty("hits").EnumerateArray()
        .Where(hit => !hit.TryGetProperty("status", out var statusEl) ||
                      string.Equals(statusEl.GetString(), "Active", StringComparison.OrdinalIgnoreCase))
        .ToList();
    var total = raw.TryGetProperty("estimatedTotalHits", out var estimated)
        ? estimated.GetInt32()
        : hits.Count;

    var paged = hits
        .Select(hit => new
        {
            id = hit.GetProperty("id").GetGuid(),
            title = hit.GetProperty("title").GetString(),
            company = new { name = hit.GetProperty("company").GetString() },
            locationText = hit.TryGetProperty("locationText", out var loc) ? loc.GetString() : null,
            workMode = hit.TryGetProperty("workMode", out var wm) ? wm.GetString() : null,
            seniority = hit.TryGetProperty("seniority", out var sr) ? sr.GetString() : null,
            employmentType = hit.TryGetProperty("employmentType", out var et) ? et.GetString() : null,
            tags = hit.TryGetProperty("tags", out var tg)
                ? tg.EnumerateArray().Select(x => x.GetString()).Where(x => x is not null).Cast<string>().ToArray()
                : Array.Empty<string>(),
            postedAt = hit.TryGetProperty("postedAt", out var pa) ? pa.GetString() : null,
            capturedAt = hit.TryGetProperty("capturedAt", out var ca) ? ca.GetString() : null,
            source = new
            {
                name = hit.TryGetProperty("sourceName", out var sn) ? sn.GetString() : null,
                url = hit.TryGetProperty("sourceUrl", out var su) ? su.GetString() : null
            }
        })
        .ToList();

    return Results.Ok(new
    {
        page,
        pageSize,
        total,
        items = paged
    });
});

app.Run();

static string[] ResolveSort(string? sort)
{
    if (string.IsNullOrWhiteSpace(sort))
    {
        return new[] { "postedAt:desc", "capturedAt:desc" };
    }

    return sort.Trim().ToLowerInvariant() switch
    {
        "postedat:asc" => new[] { "postedAt:asc", "capturedAt:desc" },
        "capturedat:asc" => new[] { "capturedAt:asc" },
        "capturedat:desc" => new[] { "capturedAt:desc" },
        _ => new[] { "postedAt:desc", "capturedAt:desc" }
    };
}

static bool TryParseEnum<TEnum>(string? raw, out TEnum value)
    where TEnum : struct, Enum
{
    if (!string.IsNullOrWhiteSpace(raw) && Enum.TryParse<TEnum>(raw, true, out var parsed))
    {
        value = parsed;
        return true;
    }

    value = default;
    return false;
}

static object TryParseJson(string rawJson)
{
    try
    {
        return JsonSerializer.Deserialize<JsonElement>(rawJson);
    }
    catch
    {
        return new { };
    }
}

static string EscapeFilterValue(string value)
{
    return value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);
}
