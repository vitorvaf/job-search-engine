using System.Text.Json;
using Jobs.Domain.Models;
using Jobs.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobs.Infrastructure.Ingestion;

public sealed class JsonFixtureJobSource : IJobSource
{
    public string Name => "Fixtures";
    public SourceType Type => SourceType.Fixture;

    private readonly AppOptions _opts;
    private readonly Fingerprint _fp;
    private readonly ILogger<JsonFixtureJobSource> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public JsonFixtureJobSource(IOptions<AppOptions> opts, Fingerprint fp, ILogger<JsonFixtureJobSource> logger)
    {
        _opts = opts.Value;
        _fp = fp;
        _logger = logger;
    }

    public async IAsyncEnumerable<JobPosting> FetchAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var path = _opts.SamplesPath;
        if (!Directory.Exists(path))
        {
            _logger.LogWarning("SamplesPath não existe: {Path}", path);
            yield break;
        }

        var files = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)
            .Where(f => Path.GetFileName(f).StartsWith("sample_source_", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var json = await File.ReadAllTextAsync(file, ct);
            using var doc = JsonDocument.Parse(json);

            // leitura "tolerante" do sample (campos mínimos)
            var root = doc.RootElement;

            var source = root.GetProperty("source").GetString() ?? "Unknown";
            var jobId = root.TryGetProperty("jobId", out var jobIdEl) ? jobIdEl.GetString() : null;
            var title = root.GetProperty("title").GetString() ?? "Untitled";
            var company = root.GetProperty("company").GetString() ?? "Unknown";
            var location = root.TryGetProperty("location", out var locEl) ? locEl.GetString() ?? "" : "";
            var url = root.GetProperty("url").GetString() ?? "";
            var desc = root.TryGetProperty("description", out var dEl) ? dEl.GetString() ?? "" : "";
            var postedAt = ParsePostedAt(root);

            var workMode = WorkMode.Unknown;
            if (location.Contains("remote", StringComparison.OrdinalIgnoreCase) || location.Contains("remoto", StringComparison.OrdinalIgnoreCase))
                workMode = WorkMode.Remote;
            else if (location.Contains("híbrido", StringComparison.OrdinalIgnoreCase) || location.Contains("hibrido", StringComparison.OrdinalIgnoreCase))
                workMode = WorkMode.Hybrid;

            var fingerprint = _fp.Compute(company, title, location, workMode.ToString());
            var sourceType = ParseSourceType(source);

            yield return new JobPosting
            {
                Source = new JobSourceRef(source, sourceType, url, jobId),
                Title = title,
                Company = new CompanyRef(company),
                LocationText = location,
                Location = InferLocation(location),
                WorkMode = workMode,
                DescriptionText = desc,
                Tags = InferTags(desc, title),
                Languages = InferLanguages(desc),
                PostedAt = postedAt,
                CapturedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
                Status = JobStatus.Active,
                Dedupe = new DedupeInfo(fingerprint, null),
                Metadata = new Dictionary<string, object>
                {
                    ["fixtureFile"] = Path.GetFileName(file),
                    ["raw"] = JsonSerializer.Deserialize<object>(json, JsonOpts) ?? new { }
                }
            };
        }
    }

    private static IReadOnlyList<string> InferTags(string desc, string title)
    {
        var text = (title + " " + desc).ToLowerInvariant();
        var tags = new List<string>();

        void Add(string t) { if (!tags.Contains(t)) tags.Add(t); }

        if (text.Contains(".net")) Add("dotnet");
        if (text.Contains("c#")) Add("csharp");
        if (text.Contains("react")) Add("react");
        if (text.Contains("typescript")) Add("typescript");
        if (text.Contains("postgres")) Add("postgresql");
        if (text.Contains("kafka")) Add("kafka");
        if (text.Contains("azure")) Add("azure");
        if (text.Contains("aws")) Add("aws");
        if (text.Contains("docker")) Add("docker");

        return tags;
    }

    private static IReadOnlyList<string> InferLanguages(string desc)
    {
        // heurística bem simples
        if (desc.Any(c => "ãõçáéíóú".Contains(char.ToLowerInvariant(c)))) return new[] { "pt-BR" };
        return new[] { "en" };
    }

    private static DateTimeOffset? ParsePostedAt(JsonElement root)
    {
        if (!root.TryGetProperty("postedAt", out var postedAtElement))
        {
            return null;
        }

        return postedAtElement.ValueKind == JsonValueKind.String &&
               DateTimeOffset.TryParse(postedAtElement.GetString(), out var parsed)
            ? parsed
            : null;
    }

    private static SourceType ParseSourceType(string source)
    {
        return source.Trim().ToLowerInvariant() switch
        {
            "linkedin" => SourceType.LinkedIn,
            "greenhouse" => SourceType.Greenhouse,
            "lever" => SourceType.Lever,
            "indeed" => SourceType.Indeed,
            "careerspage" => SourceType.CareersPage,
            _ => SourceType.Fixture
        };
    }

    private static LocationRef? InferLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var normalizedLocation = JobTextNormalizer.Normalize(location);
        var country = normalizedLocation.Contains("brazil", StringComparison.Ordinal) ||
                      normalizedLocation.Contains("brasil", StringComparison.Ordinal) ||
                      normalizedLocation.Contains("sp", StringComparison.Ordinal) ||
                      normalizedLocation.Contains("pr", StringComparison.Ordinal)
            ? "BR"
            : null;

        return new LocationRef(country, null, null);
    }
}
