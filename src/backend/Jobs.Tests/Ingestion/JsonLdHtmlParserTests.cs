using Jobs.Infrastructure.Ingestion;

namespace Jobs.Tests.Ingestion;

public sealed class JsonLdHtmlParserTests
{
    [Fact]
    public void ParseJobPostings_ShouldExtractJobsFromLdJsonFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "jsonld_jobs.html");
        var html = File.ReadAllText(fixturePath);

        var jobs = JsonLdHtmlParser.ParseJobPostings(html, "https://careers.example.com/open-positions");

        Assert.True(jobs.Count >= 2);
        Assert.Contains(jobs, x => x.SourceJobId == "REQ-123" && x.Company == "Example Corp");
        Assert.Contains(jobs, x => x.SourceJobId == "REQ-124" && x.Url == "https://careers.example.com/jobs/124");
    }
}
