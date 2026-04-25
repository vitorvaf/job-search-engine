namespace Jobs.Infrastructure.Data.Entities;

public sealed class UserCredentialEntity
{
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
