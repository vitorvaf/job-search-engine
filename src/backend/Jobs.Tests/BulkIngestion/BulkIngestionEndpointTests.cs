using System.Net;
using System.Net.Http.Json;
using System.Text;
using Jobs.Infrastructure.BulkIngestion;
using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Options;
using Jobs.Infrastructure.Search;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jobs.Tests.BulkIngestion;

public sealed class BulkIngestionEndpointTests
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    private static WebApplicationFactory<Program> CreateFactory(string? configuredApiKey = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Replace DbContext with InMemory to avoid requiring a real Postgres connection
                var dbDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<JobsDbContext>));
                if (dbDescriptor != null) services.Remove(dbDescriptor);
                services.AddDbContext<JobsDbContext>(opt =>
                    opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));

                // Replace MeiliClient with a fake backed by a stub HTTP handler
                var meiliDescriptors = services
                    .Where(d => d.ServiceType == typeof(MeiliClient))
                    .ToList();
                foreach (var d in meiliDescriptors) services.Remove(d);
                services.AddSingleton<MeiliClient>(_ =>
                {
                    var http = new HttpClient(new FakeMeiliHttpMessageHandler());
                    var opts = Options.Create(new MeiliOptions
                    {
                        BaseUrl = "http://fake-meili",
                        MasterKey = "test-key"
                    });
                    return new MeiliClient(http, opts);
                });

                // Override the API key and search index name for this test run
                services.PostConfigure<AppOptions>(opts =>
                {
                    opts.Ingestion.ApiKey = configuredApiKey;
                    opts.SearchIndexName = "test-jobs";
                });
            });
        });
    }

    private static BulkIngestionRequest ValidRequest(int itemCount = 1) => new()
    {
        SourceName = "TestSource",
        SourceType = "ExternalIngestion",
        Items = Enumerable.Range(1, itemCount).Select(i => new BulkIngestionItemRequest
        {
            Title = $"Job {i}",
            Company = new BulkIngestionCompanyRequest { Name = "Empresa Test" },
            SourceUrl = $"https://example.com/job/{i}",
            WorkMode = "Remote"
        }).ToList()
    };

    // ── Authentication tests ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_BulkIngestion_WhenApiKeyConfigured_AndHeaderMissing_Returns401()
    {
        using var factory = CreateFactory(configuredApiKey: "secret-key");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ingestion/jobs/bulk", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_BulkIngestion_WhenApiKeyConfigured_AndHeaderWrong_Returns401()
    {
        using var factory = CreateFactory(configuredApiKey: "secret-key");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Ingestion-Key", "wrong-key");

        var response = await client.PostAsJsonAsync("/api/ingestion/jobs/bulk", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_BulkIngestion_WhenApiKeyConfigured_AndHeaderCorrect_Returns200()
    {
        using var factory = CreateFactory(configuredApiKey: "secret-key");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Ingestion-Key", "secret-key");

        var response = await client.PostAsJsonAsync("/api/ingestion/jobs/bulk", ValidRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_BulkIngestion_WhenNoApiKeyConfigured_Returns200WithoutHeader()
    {
        using var factory = CreateFactory(configuredApiKey: null);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ingestion/jobs/bulk", ValidRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Batch size validation tests ───────────────────────────────────────────

    [Fact]
    public async Task Post_BulkIngestion_WhenItemsIsEmpty_Returns400()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var request = new BulkIngestionRequest
        {
            SourceName = "TestSource",
            Items = []
        };

        var response = await client.PostAsJsonAsync("/api/ingestion/jobs/bulk", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_BulkIngestion_WhenItemsExceed100_Returns400()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ingestion/jobs/bulk", ValidRequest(itemCount: 101));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_BulkIngestion_WhenItemsExceed100_ResponseBodyContainsReceivedCount()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ingestion/jobs/bulk", ValidRequest(itemCount: 101));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("101", body);
    }

    // ── Success path tests ────────────────────────────────────────────────────

    [Fact]
    public async Task Post_BulkIngestion_WithValidBatch_ReturnsOkWithCorrectCounts()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ingestion/jobs/bulk", ValidRequest(itemCount: 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BulkIngestionResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Received);
        Assert.Equal(2, result.Inserted);
        Assert.Equal(0, result.Invalid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Post_BulkIngestion_WithExactly100Items_Returns200()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ingestion/jobs/bulk", ValidRequest(itemCount: 100));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BulkIngestionResponse>();
        Assert.NotNull(result);
        Assert.Equal(100, result.Received);
    }

    // ── Stub HTTP handler shared by factory helpers ───────────────────────────

    private sealed class FakeMeiliHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"taskUid\":1,\"indexUid\":\"test-jobs\",\"status\":\"enqueued\"}",
                    Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
