using Jobs.Domain.Models;

namespace Jobs.Infrastructure.Data.Entities;

public sealed class SourceEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public SourceType Type { get; set; }
    public string? BaseUrl { get; set; }
    public bool Enabled { get; set; } = true;

    public string RateLimitPolicyJson { get; set; } = "{}";
}
