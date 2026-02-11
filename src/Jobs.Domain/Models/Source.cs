namespace Jobs.Domain.Models;

public sealed class Source
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = default!;
    public SourceType Type { get; init; } = SourceType.Unknown;
    public string? BaseUrl { get; init; }
    public bool Enabled { get; init; } = true;

    // Ex.: {"requestsPerMinute": 30}
    public Dictionary<string, object> RateLimitPolicy { get; init; } = new();
}
