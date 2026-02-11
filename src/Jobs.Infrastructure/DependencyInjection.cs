using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Ingestion;
using Jobs.Infrastructure.Options;
using Jobs.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobs.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddJobsInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<AppOptions>(config.GetSection("App"));
        services.Configure<MeiliOptions>(config.GetSection("Meilisearch"));

        services.AddDbContext<JobsDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("JobsDb")));

        services.AddHttpClient<MeiliClient>();

        services.AddSingleton<Fingerprint>();
        services.AddScoped<IngestionPipeline>();

        // MVP: fonte via fixtures JSON
        services.AddScoped<IJobSource, JsonFixtureJobSource>();

        return services;
    }
}
