namespace Jobs.Infrastructure.Data.Entities;

public sealed class FavoriteJobEntity
{
    public Guid UserId { get; set; }
    public Guid JobPostingId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
