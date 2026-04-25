namespace Jobs.Infrastructure.Data.Entities;

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string NormalizedEmail { get; set; } = default!;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
