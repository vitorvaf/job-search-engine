using Jobs.Infrastructure.Ingestion;

namespace Jobs.Tests.Ingestion;

public sealed class GupyJobsJsonParserTests
{
    [Fact]
    public void Parse_ShouldReadFixtureJobs()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "gupy_company_jobs.json");
        var json = File.ReadAllText(fixturePath);

        var jobs = GupyJobsJsonParser.Parse(json, "https://example.gupy.io/jobs");

        Assert.Equal(2, jobs.Count);
        Assert.Contains(jobs, j => j.SourceJobId == "acme-1001");
    }
}
