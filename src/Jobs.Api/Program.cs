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
    string? company,
    string? location,
    DateTime? postedFrom,
    int page,
    int pageSize,
    MeiliClient meili,
    IOptions<AppOptions> appOptions,
    JobsDbContext db,
    CancellationToken ct) =>
{
    page = page <= 0 ? 1 : page;
    pageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

    var query = db.JobPostings.AsNoTracking();

    if (TryParseEnum(workMode, out WorkMode workModeFilter))
    {
        query = query.Where(x => x.WorkMode == workModeFilter);
    }

    if (TryParseEnum(seniority, out Seniority seniorityFilter))
    {
        query = query.Where(x => x.Seniority == seniorityFilter);
    }

    if (!string.IsNullOrWhiteSpace(company))
    {
        query = query.Where(x => EF.Functions.ILike(x.CompanyName, $"%{company.Trim()}%"));
    }

    if (!string.IsNullOrWhiteSpace(location))
    {
        query = query.Where(x => EF.Functions.ILike(x.LocationText, $"%{location.Trim()}%"));
    }

    if (postedFrom is not null)
    {
        var floor = new DateTimeOffset(postedFrom.Value.Date, TimeSpan.Zero);
        query = query.Where(x => x.PostedAt != null && x.PostedAt >= floor);
    }

    var parsedTags = tags?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(t => t.ToLowerInvariant())
        .Distinct()
        .ToArray() ?? Array.Empty<string>();

    if (parsedTags.Length > 0)
    {
        query = query.Where(x => x.Tags.Any(tag => parsedTags.Contains(tag)));
    }

    List<Guid>? rankedIds = null;
    if (!string.IsNullOrWhiteSpace(q))
    {
        await meili.EnsureIndexAsync(appOptions.Value.SearchIndexName, ct);

        var searchResponse = await meili.SearchAsync(appOptions.Value.SearchIndexName, new
        {
            q,
            limit = 1000,
            offset = 0
        }, ct);

        rankedIds = searchResponse.GetProperty("hits")
            .EnumerateArray()
            .Select(hit => hit.GetProperty("id").GetGuid())
            .ToList();

        if (rankedIds.Count == 0)
        {
            return Results.Ok(new { page, pageSize, total = 0, items = Array.Empty<object>() });
        }

        var rankedIdsSet = rankedIds.ToHashSet();
        query = query.Where(x => rankedIdsSet.Contains(x.Id));
    }

    var total = await query.CountAsync(ct);

    var materializedItems = await query
        .Select(x => new
        {
            id = x.Id,
            title = x.Title,
            company = new { name = x.CompanyName },
            locationText = x.LocationText,
            workMode = x.WorkMode.ToString(),
            seniority = x.Seniority.ToString(),
            employmentType = x.EmploymentType.ToString(),
            tags = x.Tags,
            postedAt = x.PostedAt,
            capturedAt = x.CapturedAt,
            source = new { name = x.SourceName, url = x.SourceUrl }
        })
        .ToListAsync(ct);

    IReadOnlyList<object> pageItems;
    if (rankedIds is not null)
    {
        var rankMap = rankedIds
            .Select((id, idx) => new { id, idx })
            .ToDictionary(x => x.id, x => x.idx);

        pageItems = materializedItems
            .OrderBy(x => rankMap.GetValueOrDefault(x.id, int.MaxValue))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Cast<object>()
            .ToList();
    }
    else
    {
        pageItems = materializedItems
            .OrderByDescending(x => x.postedAt ?? x.capturedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Cast<object>()
            .ToList();
    }

    return Results.Ok(new
    {
        page,
        pageSize,
        total,
        items = pageItems
    });
});

app.Run();

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
