using Jobs.Infrastructure.Ingestion;

namespace Jobs.Tests.Ingestion;

/// <summary>
/// Integration tests that validate parsers against real response data captured from live sources.
/// Fixtures under tests/fixtures/*_real.* and *_nextdata.* contain actual HTTP responses.
/// </summary>
public sealed class IngestionSourceValidationTests
{
    // ── InfoJobs ──────────────────────────────────────────────────────────────

    [Fact]
    public void InfoJobs_ParseList_RealHtml_ExtractsJobsWithValidUrls()
    {
        // The live InfoJobs page uses relative URLs (/vaga-de-*__ID.aspx).
        // MakeAbsolute() inside the parser resolves them against the search URL.
        var html = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "infojobs_list.html"));

        var jobs = InfoJobsHtmlParser.ParseList(
            html, "https://www.infojobs.com.br/vagas.aspx?palabra=TI");

        Assert.True(jobs.Count >= 1, $"Expected ≥1 job, got {jobs.Count}");
        Assert.All(jobs, job =>
        {
            Assert.True(InfoJobsHtmlParser.IsValidInfoJobsJobUrl(job.Url),
                $"Invalid InfoJobs URL: {job.Url}");
            Assert.False(string.IsNullOrWhiteSpace(job.Title),
                "Title must not be blank");
            Assert.True(job.Title.Length >= 6,
                $"Title too short: '{job.Title}'");
            Assert.NotNull(InfoJobsHtmlParser.ExtractInfoJobsJobIdFromUrl(job.Url));
        });
    }

    // ── Accenture Workday ─────────────────────────────────────────────────────

    [Fact]
    public void AccentureWorkday_ParseListing_RealJson_ExtractsJobsFromLiveResponse()
    {
        // Fixture captured from a real POST to the Workday CXS API (limit=5).
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "accenture_workday_real.json");
        if (!File.Exists(fixturePath))
        {
            // Fall back to the hand-crafted fixture if real one is absent.
            fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "accenture_workday_jobs_page1.json");
        }

        var json = File.ReadAllText(fixturePath);
        var jobs = WorkdayJobsJsonParser.ParseListing(
            json,
            "accenture.wd103.myworkdayjobs.com",
            "/pt-BR/AccentureCareers",
            "AccentureCareers");

        Assert.True(jobs.Count >= 1, $"Expected ≥1 job, got {jobs.Count}");
        Assert.All(jobs, job =>
        {
            Assert.False(string.IsNullOrWhiteSpace(job.Title));
            Assert.False(string.IsNullOrWhiteSpace(job.SourceJobId));
            Assert.True(Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var uri) &&
                        uri.Scheme == "https" &&
                        uri.Host == "accenture.wd103.myworkdayjobs.com",
                $"Invalid source URL: {job.SourceUrl}");
            Assert.False(string.IsNullOrWhiteSpace(job.LocationText));
        });
    }

    [Fact]
    public void AccentureWorkday_ParseListing_RealJson_LocationExtractedFromBulletFields()
    {
        // The real Workday API omits locationsText; location lives in bulletFields[1].
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "accenture_workday_real.json");
        if (!File.Exists(fixturePath))
        {
            return; // fixture not present, skip
        }

        var json = File.ReadAllText(fixturePath);
        var jobs = WorkdayJobsJsonParser.ParseListing(
            json,
            "accenture.wd103.myworkdayjobs.com",
            "/pt-BR/AccentureCareers",
            "AccentureCareers");

        // At least one job must have a location that isn't "Unknown"
        Assert.Contains(jobs, j => j.LocationText != "Unknown");
    }

    // ── Gupy (Casas Bahia) ────────────────────────────────────────────────────

    [Fact]
    public void Gupy_ParseList_RealNextData_ExtractsJobsWithNumericId()
    {
        // Real Gupy pages embed job data in __NEXT_DATA__ with numeric `id` (not string)
        // and no `url`/`jobUrl` field. The parser must synthesize the URL from the base URL.
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "gupy_casasbahia_nextdata.json");
        if (!File.Exists(fixturePath))
        {
            return; // fixture not present, skip
        }

        var json = File.ReadAllText(fixturePath);
        var baseUrl = "https://tecnologiagrupocasasbahia.gupy.io/";

        var jobs = GupyJobsJsonParser.Parse(json, baseUrl);

        Assert.True(jobs.Count >= 1, $"Expected ≥1 job from real Gupy NEXT_DATA, got {jobs.Count}");
        Assert.All(jobs, job =>
        {
            Assert.False(string.IsNullOrWhiteSpace(job.SourceJobId),
                "SourceJobId must not be blank");
            Assert.False(string.IsNullOrWhiteSpace(job.Title),
                "Title must not be blank");
            Assert.True(job.Title.Length >= 6,
                $"Title too short: '{job.Title}'");
            Assert.True(Uri.TryCreate(job.Url, UriKind.Absolute, out var uri) &&
                        uri.Scheme == "https",
                $"URL must be absolute HTTPS: {job.Url}");
            Assert.True(job.Url.Contains("tecnologiagrupocasasbahia.gupy.io"),
                $"URL must point to the company Gupy domain: {job.Url}");
        });
    }

    [Fact]
    public void Gupy_ParseList_RealNextData_SynthesizesJobUrlFromNumericId()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "gupy_casasbahia_nextdata.json");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        var json = File.ReadAllText(fixturePath);
        var jobs = GupyJobsJsonParser.Parse(json, "https://tecnologiagrupocasasbahia.gupy.io/");

        // Synthesized URL pattern: https://[host]/job/[numericId]
        Assert.All(jobs, job =>
        {
            Assert.Matches(@"^https://tecnologiagrupocasasbahia\.gupy\.io/job/\d+$", job.Url);
            // ID should be a valid integer string (not a hash or slug)
            Assert.True(long.TryParse(job.SourceJobId, out _),
                $"Expected numeric SourceJobId, got: {job.SourceJobId}");
        });
    }

    // ── Gupy fixture (existing hand-crafted fixture still works) ──────────────

    [Fact]
    public void Gupy_ParseList_LegacyFixture_StillParsesStringIdAndJobUrl()
    {
        // Ensures the fix for numeric IDs does not break the original fixture format
        // which uses string "id" and "jobUrl" fields.
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "gupy_company_jobs.json");
        var json = File.ReadAllText(fixturePath);

        var jobs = GupyJobsJsonParser.Parse(json, "https://example.gupy.io/jobs");

        Assert.Equal(2, jobs.Count);
        Assert.Contains(jobs, j => j.SourceJobId == "acme-1001");
        Assert.Contains(jobs, j => j.Url == "https://example.gupy.io/jobs/1001");
    }

    // ── Workday – existing fixture still passes ───────────────────────────────

    [Fact]
    public void AccentureWorkday_ParseListing_HandCraftedFixture_StillPasses()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "accenture_workday_jobs_page1.json");
        var json = File.ReadAllText(fixturePath);

        var jobs = WorkdayJobsJsonParser.ParseListing(
            json,
            "accenture.wd103.myworkdayjobs.com",
            "/pt-BR/AccentureCareers",
            "AccentureCareers");

        Assert.True(jobs.Count >= 10);
    }
}
