using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Ingestion;
using Jobs.Infrastructure.Options;
using Jobs.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http.Headers;

namespace Jobs.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddJobsInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<AppOptions>(config.GetSection("App"));
        services.Configure<MeiliOptions>(config.GetSection("Meilisearch"));
        var appOpts = config.GetSection("App").Get<AppOptions>() ?? new AppOptions();

        services.AddDbContext<JobsDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("JobsDb")));

        services.AddHttpClient<MeiliClient>();
        services.AddHttpClient("Sources")
            .ConfigureHttpClient((sp, client) =>
            {
                var appOpts = sp.GetRequiredService<IOptions<AppOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(Math.Max(5, appOpts.Http.TimeoutSeconds));
                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.UserAgent.ParseAdd(appOpts.Http.UserAgent);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                client.DefaultRequestHeaders.AcceptLanguage.Clear();
                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR,pt;q=0.9,en;q=0.8");
            })
            .AddPolicyHandler((sp, _) =>
            {
                var opts = sp.GetRequiredService<IOptions<AppOptions>>().Value.Http;
                var retries = Math.Max(1, opts.MaxRetries);
                var initialBackoff = Math.Max(100, opts.InitialBackoffMs);

                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .OrResult(r => (int)r.StatusCode == 429)
                    .WaitAndRetryAsync(retries, attempt =>
                    {
                        var expo = initialBackoff * Math.Pow(2, attempt - 1);
                        var jitter = Random.Shared.Next(20, 250);
                        return TimeSpan.FromMilliseconds(expo + jitter);
                    });
            });

        services.AddSingleton<Fingerprint>();
        services.AddScoped<IngestionPipeline>();

        // Fontes MVP
        services.AddScoped<IJobSource, InfoJobsJobSource>();
        services.AddScoped<IJobSource, StoneVagasJobSource>();
        services.AddScoped<IJobSource, JsonFixtureJobSource>();
        services.AddScoped<IJobSource, AccentureWorkdayJobSource>();

        foreach (var source in appOpts.Sources.CorporateCareers.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Name)))
        {
            services.AddScoped<IJobSource>(sp =>
                ActivatorUtilities.CreateInstance<CorporateCareersJobSource>(sp, source));
        }

        foreach (var source in appOpts.Sources.JsonLd.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Name)))
        {
            services.AddScoped<IJobSource>(sp =>
                ActivatorUtilities.CreateInstance<JsonLdJobSource>(sp, source));
        }

        foreach (var source in appOpts.Sources.GupyCompanies.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Name)))
        {
            services.AddScoped<IJobSource>(sp =>
                ActivatorUtilities.CreateInstance<GupyCompanyJobSource>(sp, source));
        }

        return services;
    }
}
