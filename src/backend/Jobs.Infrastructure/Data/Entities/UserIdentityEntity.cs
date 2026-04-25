namespace Jobs.Infrastructure.Data.Entities;

public sealed class UserIdentityEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = default!;
    public string ProviderUserId { get; set; } = default!;
    public string? ProviderEmail { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
