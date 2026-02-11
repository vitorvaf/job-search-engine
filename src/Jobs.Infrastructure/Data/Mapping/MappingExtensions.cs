using System.Text.Json;
using Jobs.Domain.Models;
using Jobs.Infrastructure.Data.Entities;

namespace Jobs.Infrastructure.Data.Mapping;

public static class MappingExtensions
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static JobPostingEntity ToEntity(this JobPosting job)
    {
        return new JobPostingEntity
        {
            Id = job.Id,

            SourceName = job.Source.Name,
            SourceType = job.Source.Type,
            SourceUrl = job.Source.Url,
            SourceJobId = job.Source.SourceJobId,

            Title = job.Title,
            CompanyName = job.Company.Name,
            CompanyWebsite = job.Company.Website,
            CompanyIndustry = job.Company.Industry,

            LocationText = job.LocationText,
            Country = job.Location?.Country,
            State = job.Location?.State,
            City = job.Location?.City,

            WorkMode = job.WorkMode,
            Seniority = job.Seniority,
            EmploymentType = job.EmploymentType,

            SalaryMin = job.Salary?.Min,
            SalaryMax = job.Salary?.Max,
            SalaryCurrency = job.Salary?.Currency,
            SalaryPeriod = job.Salary?.Period,

            DescriptionText = job.DescriptionText,
            Tags = job.Tags.ToArray(),
            Languages = job.Languages.ToArray(),

            PostedAt = job.PostedAt,
            CapturedAt = job.CapturedAt,
            LastSeenAt = job.LastSeenAt,
            Status = job.Status,

            Fingerprint = job.Dedupe.Fingerprint,
            ClusterId = job.Dedupe.ClusterId,

            MetadataJson = JsonSerializer.Serialize(job.Metadata, JsonOpts)
        };
    }

    public static object ToSearchDocument(this JobPostingEntity e)
    {
        return new
        {
            id = e.Id,
            title = e.Title,
            companyName = e.CompanyName,
            locationText = e.LocationText,
            workMode = e.WorkMode.ToString(),
            seniority = e.Seniority.ToString(),
            employmentType = e.EmploymentType.ToString(),
            tags = e.Tags,
            postedAt = e.PostedAt,
            capturedAt = e.CapturedAt,
            sourceName = e.SourceName,
            sourceUrl = e.SourceUrl,
            fingerprint = e.Fingerprint
        };
    }
}
