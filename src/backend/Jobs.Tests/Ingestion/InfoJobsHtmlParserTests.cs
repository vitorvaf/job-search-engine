using Jobs.Infrastructure.Ingestion;

namespace Jobs.Tests.Ingestion;

public sealed class InfoJobsHtmlParserTests
{
    [Fact]
    public void ParseList_ShouldReturnOnlyValidInfoJobsJobUrls_AndExtractRequiredFields()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "infojobs_list.html");
        var html = File.ReadAllText(fixturePath);

        var jobs = InfoJobsHtmlParser.ParseList(html, "https://www.infojobs.com.br/vagas.aspx?palabra=TI");

        Assert.Equal(2, jobs.Count);
        Assert.All(jobs, job =>
        {
            Assert.True(InfoJobsHtmlParser.IsValidInfoJobsJobUrl(job.Url));
            Assert.False(string.IsNullOrWhiteSpace(job.Title));
            Assert.False(string.IsNullOrWhiteSpace(job.Company));
            Assert.False(string.IsNullOrWhiteSpace(job.LocationText));
            Assert.NotNull(InfoJobsHtmlParser.ExtractInfoJobsJobIdFromUrl(job.Url));
        });
    }

    [Fact]
    public void ParseDetailDescription_ShouldExtractDescriptionText()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "infojobs_detail.html");
        var html = File.ReadAllText(fixturePath);

        var description = InfoJobsHtmlParser.ParseDetailDescription(html);

        Assert.Contains("Buscamos pessoa desenvolvedora", description);
        Assert.Contains("Responsabilidades", description);
    }

    [Fact]
    public void BuildStableSourceJobId_ShouldUseIdFromInfoJobsUrl()
    {
        const string url = "https://www.infojobs.com.br/vaga-de-desenvolvedor-dotnet__44556677.aspx";

        var sourceJobId = InfoJobsHtmlParser.BuildStableSourceJobId(null, url);

        Assert.Equal("44556677", sourceJobId);
    }
}
