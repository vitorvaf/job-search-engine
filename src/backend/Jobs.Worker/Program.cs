using Jobs.Infrastructure;
using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Options;
using Jobs.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
var runOptions = ParseRunOptions(args);

builder.Services.AddJobsInfrastructure(builder.Configuration);
builder.Services.AddSingleton(runOptions);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
    await db.Database.EnsureCreatedAsync();

    var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>();
    var meili = scope.ServiceProvider.GetRequiredService<MeiliClient>();
    await meili.EnsureIndexAsync(appOptions.Value.SearchIndexName, CancellationToken.None);
}

host.Run();

static WorkerRunOptions ParseRunOptions(string[] args)
{
    var runOnce = args.Any(a => string.Equals(a, "--run-once", StringComparison.OrdinalIgnoreCase));
    var sourceArg = args.FirstOrDefault(a => a.StartsWith("--source=", StringComparison.OrdinalIgnoreCase));
    var maxItemsArg = args.FirstOrDefault(a => a.StartsWith("--max-items=", StringComparison.OrdinalIgnoreCase));
    var maxDetailArg = args.FirstOrDefault(a => a.StartsWith("--max-detail=", StringComparison.OrdinalIgnoreCase));
    var sourceFilter = sourceArg?.Split('=', 2, StringSplitOptions.TrimEntries)[1];
    var maxItems = TryParsePositiveInt(maxItemsArg);
    var maxDetail = TryParseNonNegativeInt(maxDetailArg);

    return new WorkerRunOptions
    {
        RunOnce = runOnce,
        SourceFilter = string.IsNullOrWhiteSpace(sourceFilter) ? null : sourceFilter,
        MaxItems = maxItems,
        MaxDetail = maxDetail
    };
}

static int? TryParsePositiveInt(string? arg)
{
    var raw = arg?.Split('=', 2, StringSplitOptions.TrimEntries)[1];
    if (int.TryParse(raw, out var value) && value > 0)
    {
        return value;
    }

    return null;
}

static int? TryParseNonNegativeInt(string? arg)
{
    var raw = arg?.Split('=', 2, StringSplitOptions.TrimEntries)[1];
    if (int.TryParse(raw, out var value) && value >= 0)
    {
        return value;
    }

    return null;
}
