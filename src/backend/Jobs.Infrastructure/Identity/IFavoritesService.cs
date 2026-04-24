using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Infrastructure.Identity;

public interface IFavoritesService
{
    Task<IReadOnlyList<JobPostingEntity>> ListFavoritesAsync(Guid userId, CancellationToken cancellationToken);
    Task<AddFavoriteResult> AddFavoriteAsync(Guid userId, Guid jobPostingId, CancellationToken cancellationToken);
    Task<RemoveFavoriteResult> RemoveFavoriteAsync(Guid userId, Guid jobPostingId, CancellationToken cancellationToken);
}

public sealed class FavoritesService : IFavoritesService
{
    private readonly JobsDbContext _db;

    public FavoritesService(JobsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<JobPostingEntity>> ListFavoritesAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _db.FavoriteJobs
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Join(
                _db.JobPostings,
                favorite => favorite.JobPostingId,
                job => job.Id,
                (_, job) => job)
            .ToListAsync(cancellationToken);
    }

    public async Task<AddFavoriteResult> AddFavoriteAsync(Guid userId, Guid jobPostingId, CancellationToken cancellationToken)
    {
        var userExists = await _db.Users.AnyAsync(x => x.Id == userId, cancellationToken);
        if (!userExists)
        {
            return new AddFavoriteResult(AddFavoriteStatus.UserNotFound);
        }

        var jobExists = await _db.JobPostings.AnyAsync(x => x.Id == jobPostingId, cancellationToken);
        if (!jobExists)
        {
            return new AddFavoriteResult(AddFavoriteStatus.JobNotFound);
        }

        var exists = await _db.FavoriteJobs.AnyAsync(
            x => x.UserId == userId && x.JobPostingId == jobPostingId,
            cancellationToken);

        if (exists)
        {
            return new AddFavoriteResult(AddFavoriteStatus.AlreadyExists);
        }

        _db.FavoriteJobs.Add(new FavoriteJobEntity
        {
            UserId = userId,
            JobPostingId = jobPostingId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return new AddFavoriteResult(AddFavoriteStatus.Added);
    }

    public async Task<RemoveFavoriteResult> RemoveFavoriteAsync(Guid userId, Guid jobPostingId, CancellationToken cancellationToken)
    {
        var entity = await _db.FavoriteJobs.FirstOrDefaultAsync(
            x => x.UserId == userId && x.JobPostingId == jobPostingId,
            cancellationToken);

        if (entity is null)
        {
            return new RemoveFavoriteResult(RemoveFavoriteStatus.NotFound);
        }

        _db.FavoriteJobs.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return new RemoveFavoriteResult(RemoveFavoriteStatus.Removed);
    }
}
