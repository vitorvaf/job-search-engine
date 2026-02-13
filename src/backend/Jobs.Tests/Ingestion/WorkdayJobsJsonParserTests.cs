using Jobs.Infrastructure.Ingestion;

namespace Jobs.Tests.Ingestion;

public sealed class WorkdayJobsJsonParserTests
{
    [Fact]
    public void ParseListing_ShouldExtractAtLeastTenJobsWithCoreFields()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "accenture_workday_jobs_page1.json");
        var json = File.ReadAllText(fixturePath);

        var jobs = WorkdayJobsJsonParser.ParseListing(
            json,
            "accenture.wd103.myworkdayjobs.com",
            "/pt-BR/AccentureCareers",
            "AccentureCareers");

        Assert.True(jobs.Count >= 10);
        foreach (var job in jobs.Take(10))
        {
            Assert.False(string.IsNullOrWhiteSpace(job.Title));
            Assert.False(string.IsNullOrWhiteSpace(job.LocationText));
            Assert.False(string.IsNullOrWhiteSpace(job.SourceJobId));
        }
    }

    [Fact]
    public void ParseListing_ShouldBuildValidSourceUrls()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "accenture_workday_jobs_page1.json");
        var json = File.ReadAllText(fixturePath);

        var jobs = WorkdayJobsJsonParser.ParseListing(
            json,
            "accenture.wd103.myworkdayjobs.com",
            "/pt-BR/AccentureCareers",
            "AccentureCareers");

        Assert.All(jobs, job =>
        {
            Assert.True(Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var uri));
            Assert.Equal("https", uri!.Scheme);
            Assert.Equal("accenture.wd103.myworkdayjobs.com", uri.Host);
        });
    }

    [Fact]
    public void ParseDetailDescription_AndFingerprint_ShouldRemainStable()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "accenture_workday_job_detail.json");
        var json = File.ReadAllText(fixturePath);

        var description = WorkdayJobsJsonParser.ParseDetailDescription(json);
        Assert.Contains("design and build scalable systems", description);
        Assert.Contains("Build cloud native APIs", description);

        var fingerprint = new Fingerprint();
        var first = fingerprint.Compute("Accenture", "Application Developer", "Sao Paulo, Brazil", "Hybrid");
        var second = fingerprint.Compute("Accenture", "Application Developer", "Sao Paulo, Brazil", "Hybrid");
        Assert.Equal(first, second);
        Assert.False(string.IsNullOrWhiteSpace(first));
    }
}
