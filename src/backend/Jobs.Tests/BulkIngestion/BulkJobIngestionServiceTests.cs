using System.Net;
using System.Text;
using Jobs.Domain.Models;
using Jobs.Infrastructure.BulkIngestion;
using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Ingestion;
using Jobs.Infrastructure.Options;
using Jobs.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jobs.Tests.BulkIngestion;

public sealed class BulkJobIngestionServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static JobsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobsDbContext(options);
    }

    private static MeiliClient CreateMeiliClient()
    {
        var handler = new FakeMeiliHttpMessageHandler();
        var http = new HttpClient(handler);

        var opts = Options.Create(new MeiliOptions
        {
            BaseUrl = "http://localhost:7700",
            MasterKey = "test-key"
        });

        return new MeiliClient(http, opts);
    }

    private static BulkJobIngestionService CreateService(JobsDbContext db, MeiliClient? meili = null)
    {
        meili ??= CreateMeiliClient();
        var fingerprint = new Fingerprint();
        var appOptions = Options.Create(new AppOptions
        {
            SearchIndexName = "jobs",
            Ingestion = new IngestionOptions { ApiKey = null }
        });
        var logger = NullLogger<BulkJobIngestionService>.Instance;

        return new BulkJobIngestionService(db, meili, fingerprint, appOptions, logger);
    }

    private static BulkIngestionItemRequest ValidItem(
        string title = "Backend Developer",
        string company = "Empresa X",
        string? sourceUrl = "https://example.com/job/1",
        string? originUrl = null) => new()
        {
            Title = title,
            Company = new BulkIngestionCompanyRequest { Name = company },
            SourceUrl = sourceUrl,
            OriginUrl = originUrl,
            SourceJobId = null, // Not set by default; tests that need idempotency by sourceJobId set it explicitly
            LocationText = "São Paulo, SP",
            WorkMode = "Remote",
            Seniority = "Mid",
            EmploymentType = "CLT",
            DescriptionText = "Descrição completa da vaga com mais de 200 caracteres para garantir que o texto não seja descartado por regras de qualidade de conteúdo.",
            Tags = new List<string> { "dotnet", "azure" }
        };

    private static BulkIngestionRequest MakeRequest(params BulkIngestionItemRequest[] items) => new()
    {
        SourceName = "Firecrawl",
        SourceType = "ExternalIngestion",
        Items = items.ToList()
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_ValidBatch_InsertsAllItems()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var response = await svc.ProcessAsync(MakeRequest(
            ValidItem(sourceUrl: "https://example.com/job/1"),
            ValidItem(title: "Frontend Developer", sourceUrl: "https://example.com/job/2", company: "Empresa Y")),
            CancellationToken.None);

        Assert.Equal(2, response.Received);
        Assert.Equal(2, response.Processed);
        Assert.Equal(2, response.Inserted);
        Assert.Equal(0, response.Updated);
        Assert.Equal(0, response.Invalid);
        Assert.Equal(2, await db.JobPostings.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_ExistingItemByOriginUrl_UpdatesAndDoesNotDuplicate()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        // First insert
        var firstResponse = await svc.ProcessAsync(MakeRequest(
            ValidItem(originUrl: "https://company.com/careers/123")),
            CancellationToken.None);

        Assert.Equal(1, firstResponse.Inserted);

        // Second call with same originUrl, different sourceUrl
        var secondResponse = await svc.ProcessAsync(MakeRequest(
            ValidItem(originUrl: "https://company.com/careers/123",
                      sourceUrl: "https://other-source.com/job/123")),
            CancellationToken.None);

        Assert.Equal(0, secondResponse.Inserted);
        Assert.Equal(1, await db.JobPostings.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_InvalidItemMissingTitle_DoesNotBreakBatch()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var invalidItem = new BulkIngestionItemRequest
        {
            Title = "",
            Company = new BulkIngestionCompanyRequest { Name = "Empresa X" },
            SourceUrl = "https://example.com/job/99"
        };

        var response = await svc.ProcessAsync(MakeRequest(
            ValidItem(sourceUrl: "https://example.com/job/1"),
            invalidItem,
            ValidItem(title: "DevOps", sourceUrl: "https://example.com/job/2")),
            CancellationToken.None);

        Assert.Equal(3, response.Received);
        Assert.Equal(2, response.Processed);
        Assert.Equal(2, response.Inserted);
        Assert.Equal(1, response.Invalid);
        Assert.Single(response.Errors);
        Assert.Equal(1, response.Errors[0].Index);
        Assert.Contains("title", response.Errors[0].Message);
        Assert.Equal(2, await db.JobPostings.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_InvalidItemMissingCompanyName_ReportedAsInvalid()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var item = new BulkIngestionItemRequest
        {
            Title = "Engineer",
            Company = new BulkIngestionCompanyRequest { Name = "" },
            SourceUrl = "https://example.com/job/1"
        };

        var response = await svc.ProcessAsync(MakeRequest(item), CancellationToken.None);

        Assert.Equal(1, response.Invalid);
        Assert.Contains("company.name", response.Errors[0].Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidItemMissingBothUrls_ReportedAsInvalid()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var item = new BulkIngestionItemRequest
        {
            Title = "Engineer",
            Company = new BulkIngestionCompanyRequest { Name = "Empresa X" },
            SourceUrl = null,
            OriginUrl = null
        };

        var response = await svc.ProcessAsync(MakeRequest(item), CancellationToken.None);

        Assert.Equal(1, response.Invalid);
        Assert.Contains("sourceUrl or originUrl", response.Errors[0].Message);
    }

    [Fact]
    public async Task ProcessAsync_IdempotencyByOriginUrl_FindsExisting()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        await svc.ProcessAsync(MakeRequest(
            ValidItem(originUrl: "https://company.com/careers/job-a", sourceUrl: "https://crawler.io/1")),
            CancellationToken.None);

        // Same originUrl, different sourceUrl — must NOT insert a duplicate
        var response = await svc.ProcessAsync(MakeRequest(
            ValidItem(originUrl: "https://company.com/careers/job-a", sourceUrl: "https://crawler.io/2")),
            CancellationToken.None);

        Assert.Equal(0, response.Inserted);
        Assert.Equal(1, await db.JobPostings.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_IdempotencyFallbackToSourceJobId_FindsExisting()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var item1 = ValidItem(sourceUrl: "https://source.com/job/42");
        item1.SourceJobId = "job-42";
        item1.OriginUrl = null;

        await svc.ProcessAsync(MakeRequest(item1), CancellationToken.None);

        // Same sourceJobId, different sourceUrl, no originUrl
        var item2 = ValidItem(sourceUrl: "https://source2.com/job/42");
        item2.SourceJobId = "job-42";
        item2.OriginUrl = null;

        var response = await svc.ProcessAsync(MakeRequest(item2), CancellationToken.None);

        Assert.Equal(0, response.Inserted);
        Assert.Equal(1, await db.JobPostings.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_IdempotencyFallbackToSourceUrl_FindsExisting()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var item1 = new BulkIngestionItemRequest
        {
            Title = "Engineer",
            Company = new BulkIngestionCompanyRequest { Name = "Empresa X" },
            SourceUrl = "https://source.com/job/unique",
            SourceJobId = null,
            OriginUrl = null
        };

        await svc.ProcessAsync(MakeRequest(item1), CancellationToken.None);

        var item2 = new BulkIngestionItemRequest
        {
            Title = "Engineer",
            Company = new BulkIngestionCompanyRequest { Name = "Empresa X" },
            SourceUrl = "https://source.com/job/unique",
            SourceJobId = null,
            OriginUrl = null
        };

        var response = await svc.ProcessAsync(MakeRequest(item2), CancellationToken.None);

        Assert.Equal(0, response.Inserted);
        Assert.Equal(1, await db.JobPostings.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_FingerprintFallback_FindsExistingWhenUrlsDiffer()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var item1 = new BulkIngestionItemRequest
        {
            Title = "Software Engineer",
            Company = new BulkIngestionCompanyRequest { Name = "Empresa X" },
            SourceUrl = "https://source-a.com/job/1",
            LocationText = "Remote",
            WorkMode = "Remote",
            SourceJobId = null,
            OriginUrl = null
        };

        await svc.ProcessAsync(MakeRequest(item1), CancellationToken.None);

        // Same logical job (same company+title+location+workMode), different source URL, no sourceJobId
        var item2 = new BulkIngestionItemRequest
        {
            Title = "Software Engineer",
            Company = new BulkIngestionCompanyRequest { Name = "Empresa X" },
            SourceUrl = "https://source-b.com/job/99",
            LocationText = "Remote",
            WorkMode = "Remote",
            SourceJobId = null,
            OriginUrl = null
        };

        var response = await svc.ProcessAsync(MakeRequest(item2), CancellationToken.None);

        Assert.Equal(0, response.Inserted);
        Assert.Equal(1, await db.JobPostings.CountAsync());
    }

    [Fact]
    public void IsApiKeyValid_WhenKeyConfiguredAndMatches_ReturnsTrue()
    {
        using var db = CreateDb();
        var fingerprint = new Fingerprint();
        var appOptions = Options.Create(new AppOptions
        {
            SearchIndexName = "jobs",
            Ingestion = new IngestionOptions { ApiKey = "secret-key-123" }
        });
        var svc = new BulkJobIngestionService(db, CreateMeiliClient(), fingerprint, appOptions,
            NullLogger<BulkJobIngestionService>.Instance);

        Assert.True(svc.IsApiKeyValid("secret-key-123"));
        Assert.False(svc.IsApiKeyValid("wrong-key"));
        Assert.False(svc.IsApiKeyValid(null));
    }

    [Fact]
    public void IsApiKeyValid_WhenKeyNotConfigured_AlwaysReturnsTrue()
    {
        using var db = CreateDb();
        var fingerprint = new Fingerprint();
        var appOptions = Options.Create(new AppOptions
        {
            SearchIndexName = "jobs",
            Ingestion = new IngestionOptions { ApiKey = null }
        });
        var svc = new BulkJobIngestionService(db, CreateMeiliClient(), fingerprint, appOptions,
            NullLogger<BulkJobIngestionService>.Instance);

        Assert.True(svc.IsApiKeyValid(null));
        Assert.True(svc.IsApiKeyValid("any-value"));
    }

    [Fact]
    public async Task ProcessAsync_ExistingItemEnrichesDescription_WhenIncomingIsLonger()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var shortDesc = new BulkIngestionItemRequest
        {
            Title = "Backend Dev",
            Company = new BulkIngestionCompanyRequest { Name = "Empresa" },
            SourceUrl = "https://example.com/job/1",
            OriginUrl = "https://company.com/job/1",
            DescriptionText = "Short description."
        };

        await svc.ProcessAsync(MakeRequest(shortDesc), CancellationToken.None);

        var longDesc = new BulkIngestionItemRequest
        {
            Title = "Backend Dev",
            Company = new BulkIngestionCompanyRequest { Name = "Empresa" },
            SourceUrl = "https://example.com/job/1",
            OriginUrl = "https://company.com/job/1",
            DescriptionText = new string('x', 200) // 200 chars, much longer
        };

        var response = await svc.ProcessAsync(MakeRequest(longDesc), CancellationToken.None);

        Assert.Equal(1, response.Updated);
        var stored = await db.JobPostings.FirstAsync();
        Assert.Equal(200, stored.DescriptionText.Length);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotOverwriteTagsWithEmptyList()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var withTags = ValidItem(originUrl: "https://company.com/job/1");
        withTags.Tags = new List<string> { "dotnet", "azure" };

        await svc.ProcessAsync(MakeRequest(withTags), CancellationToken.None);

        var noTags = ValidItem(originUrl: "https://company.com/job/1");
        noTags.Tags = null;

        await svc.ProcessAsync(MakeRequest(noTags), CancellationToken.None);

        var stored = await db.JobPostings.FirstAsync();
        Assert.Contains("dotnet", stored.Tags);
        Assert.Contains("azure", stored.Tags);
    }

    // ── Fake HTTP Handler for MeiliClient ──────────────────────────────────

    private sealed class FakeMeiliHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"taskUid\":1,\"indexUid\":\"jobs\",\"status\":\"enqueued\"}",
                    Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
