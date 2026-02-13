using Jobs.Infrastructure.Ingestion;
using Jobs.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Worker> _logger;
    private readonly WorkerRunOptions _runOptions;

    public Worker(
        IServiceScopeFactory scopeFactory,
        ILogger<Worker> logger,
        WorkerRunOptions runOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _runOptions = runOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker iniciou.");
        _logger.LogWarning("Indeed não suportado no MVP por risco de anti-bot/challenge. Fonte ignorada.");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<IngestionPipeline>();
            var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;
            var fetchOptions = new IngestionFetchOptions(
                MaxItemsPerRun: _runOptions.MaxItems ?? Math.Max(1, appOptions.Ingestion.MaxItemsPerRunDefault),
                MaxDetailFetch: _runOptions.MaxDetail ?? Math.Max(0, appOptions.Ingestion.MaxDetailFetchDefault));
            var selectedSources = ResolveSources(scope.ServiceProvider);

            foreach (var source in selectedSources)
            {
                _logger.LogInformation("Rodando ingestão: {Source}", source.Name);
                await pipeline.RunOnceAsync(source, fetchOptions, stoppingToken);
            }

            if (_runOptions.RunOnce)
            {
                _logger.LogInformation("Execução única finalizada (--run-once).");
                return;
            }

            // MVP: roda a cada 1h (ajuste depois; pode virar cron/Quartz)
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private IReadOnlyList<IJobSource> ResolveSources(IServiceProvider provider)
    {
        var selectedSources = provider.GetServices<IJobSource>().ToList();
        if (string.IsNullOrWhiteSpace(_runOptions.SourceFilter))
        {
            return selectedSources;
        }

        selectedSources = selectedSources
            .Where(s => string.Equals(s.Name, _runOptions.SourceFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (selectedSources.Count == 0 &&
            string.Equals(_runOptions.SourceFilter, "indeed", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Fonte {Source} não suportada no MVP.", _runOptions.SourceFilter);
            return Array.Empty<IJobSource>();
        }

        if (selectedSources.Count == 0)
        {
            _logger.LogWarning("Nenhuma fonte encontrada para --source={Source}.", _runOptions.SourceFilter);
        }

        return selectedSources;
    }
}
