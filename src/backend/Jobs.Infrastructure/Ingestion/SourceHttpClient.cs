using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Jobs.Infrastructure.Ingestion;

internal sealed class SourceHttpClient
{
    private sealed class HostThrottle
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public DateTimeOffset NextAllowedAt { get; set; } = DateTimeOffset.MinValue;
    }

    private static readonly ConcurrentDictionary<string, HostThrottle> Throttles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _http;
    private readonly ILogger _logger;

    public SourceHttpClient(HttpClient http, ILogger logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string?> TryGetStringAsync(string url, string sourceName, CancellationToken ct)
    {
        Uri uri;
        try
        {
            uri = new Uri(url);
        }
        catch (Exception)
        {
            _logger.LogWarning("URL inválida para fonte {Source}: {Url}", sourceName, url);
            return null;
        }

        await EnforceRateLimitAsync(uri.Host, ct);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (IsBlocked(resp.StatusCode, body))
            {
                _logger.LogWarning(
                    "Fonte {Source} bloqueada ou desafiada em {Url}. status={Status}. Pulando sem bypass.",
                    sourceName,
                    url,
                    (int)resp.StatusCode);
                return null;
            }

            if (resp.IsSuccessStatusCode)
            {
                return body;
            }

            _logger.LogWarning(
                "Falha HTTP em {Source} url={Url} status={Status}",
                sourceName,
                url,
                (int)resp.StatusCode);
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro HTTP em {Source} url={Url}", sourceName, url);
            return null;
        }
    }

    private static async Task EnforceRateLimitAsync(string host, CancellationToken ct)
    {
        var throttle = Throttles.GetOrAdd(host, _ => new HostThrottle());
        await throttle.Lock.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var wait = throttle.NextAllowedAt - now;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, ct);
            }

            throttle.NextAllowedAt = DateTimeOffset.UtcNow.AddSeconds(1);
        }
        finally
        {
            throttle.Lock.Release();
        }
    }

    private static bool IsBlocked(HttpStatusCode statusCode, string body)
    {
        if (statusCode == HttpStatusCode.Forbidden || statusCode == (HttpStatusCode)429)
        {
            return true;
        }

        var normalized = JobTextNormalizer.Normalize(body);
        return normalized.Contains("verify you are human", StringComparison.Ordinal) ||
               normalized.Contains("cloudflare", StringComparison.Ordinal) ||
               normalized.Contains("turnstile", StringComparison.Ordinal) ||
               normalized.Contains("captcha", StringComparison.Ordinal);
    }
}
