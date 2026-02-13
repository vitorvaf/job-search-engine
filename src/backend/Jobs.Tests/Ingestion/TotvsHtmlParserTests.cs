using Jobs.Infrastructure.Ingestion;

namespace Jobs.Tests.Ingestion;

public sealed class TotvsHtmlParserTests
{
    [Fact]
    public void ParseList_ShouldExtractAtLeastFiveJobsFromFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "totvs_list.html");
        var html = File.ReadAllText(fixturePath);

        var jobs = TotvsHtmlParser.ParseList(html, "https://atracaodetalentos.totvs.app/vempratotvs/extended");

        Assert.True(jobs.Count >= 5);
        Assert.All(jobs, job =>
        {
            Assert.False(string.IsNullOrWhiteSpace(job.Title));
            Assert.False(string.IsNullOrWhiteSpace(job.Url));
            Assert.Equal("TOTVS", job.Company);
        });
    }
}
