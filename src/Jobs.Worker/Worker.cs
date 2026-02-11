using Jobs.Infrastructure.Ingestion;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class Worker : BackgroundService
{
    private readonly IEnumerable<IJobSource> _sources;
    private readonly IngestionPipeline _pipeline;
    private readonly ILogger<Worker> _logger;

    public Worker(IEnumerable<IJobSource> sources, IngestionPipeline pipeline, ILogger<Worker> logger)
    {
        _sources = sources;
        _pipeline = pipeline;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker iniciou.");

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var source in _sources)
            {
                _logger.LogInformation("Rodando ingestão: {Source}", source.Name);
                await _pipeline.RunOnceAsync(source, stoppingToken);
            }

            // MVP: roda a cada 1h (ajuste depois; pode virar cron/Quartz)
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
