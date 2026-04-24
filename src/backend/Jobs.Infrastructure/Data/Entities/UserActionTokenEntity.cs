namespace Jobs.Infrastructure.Data.Entities;

public sealed class UserActionTokenEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
