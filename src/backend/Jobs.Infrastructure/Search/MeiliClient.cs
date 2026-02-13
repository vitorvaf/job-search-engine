using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Jobs.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Jobs.Infrastructure.Search;

public sealed class MeiliClient
{
    private static readonly ConcurrentDictionary<string, byte> EnsuredIndexes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _http;
    private readonly MeiliOptions _opts;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public MeiliClient(HttpClient http, IOptions<MeiliOptions> opts)
    {
        _http = http;
        _opts = opts.Value;

        _http.BaseAddress = new Uri(_opts.BaseUrl);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opts.MasterKey);
    }

    public async Task EnsureIndexAsync(string indexName, CancellationToken ct)
    {
        if (EnsuredIndexes.ContainsKey(indexName))
        {
            return;
        }

        var indexPayload = JsonSerializer.Serialize(new { uid = indexName, primaryKey = "id" }, JsonOpts);
        using var createContent = new StringContent(indexPayload, Encoding.UTF8, "application/json");

        var createResponse = await _http.PostAsync("/indexes", createContent, ct);
        if (!createResponse.IsSuccessStatusCode && createResponse.StatusCode != System.Net.HttpStatusCode.Conflict)
        {
            createResponse.EnsureSuccessStatusCode();
        }

        var settingsPayload = JsonSerializer.Serialize(new
        {
            filterableAttributes = new[]
            {
                "workMode", "seniority", "employmentType", "tags", "company", "locationText", "sourceName", "postedAt"
            },
            sortableAttributes = new[] { "postedAt", "capturedAt" }
        }, JsonOpts);

        using var settingsContent = new StringContent(settingsPayload, Encoding.UTF8, "application/json");
        var settingsResponse = await _http.PatchAsync($"/indexes/{indexName}/settings", settingsContent, ct);
        settingsResponse.EnsureSuccessStatusCode();
        EnsuredIndexes[indexName] = 1;
    }

    public async Task UpsertAsync(string indexName, object document, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new[] { document }, JsonOpts);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var resp = await _http.PostAsync($"/indexes/{indexName}/documents", content, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<JsonElement> SearchAsync(string indexName, object query, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(query, JsonOpts);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var resp = await _http.PostAsync($"/indexes/{indexName}/search", content, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
