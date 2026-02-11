using Jobs.Infrastructure;
using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Options;
using Jobs.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddJobsInfrastructure(builder.Configuration);
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
