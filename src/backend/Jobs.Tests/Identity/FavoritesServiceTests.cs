using Jobs.Domain.Models;
using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Data.Entities;
using Jobs.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Tests.Identity;

public sealed class FavoritesServiceTests
{
    [Fact]
    public async Task AddFavoriteAsync_ShouldAddAndListFavorite()
    {
        using var db = CreateDb();
        var user = SeedUser(db);
        var job = SeedJob(db);
        await db.SaveChangesAsync();

        var service = new FavoritesService(db);

        var add = await service.AddFavoriteAsync(user.Id, job.Id, CancellationToken.None);
        var listed = await service.ListFavoritesAsync(user.Id, CancellationToken.None);

        Assert.Equal(AddFavoriteStatus.Added, add.Status);
        Assert.Single(listed);
        Assert.Equal(job.Id, listed[0].Id);
    }

    [Fact]
    public async Task AddFavoriteAsync_WhenFavoriteAlreadyExists_ShouldBeIdempotent()
    {
        using var db = CreateDb();
        var user = SeedUser(db);
        var job = SeedJob(db);
        db.FavoriteJobs.Add(new FavoriteJobEntity
        {
            UserId = user.Id,
            JobPostingId = job.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new FavoritesService(db);
        var result = await service.AddFavoriteAsync(user.Id, job.Id, CancellationToken.None);

        Assert.Equal(AddFavoriteStatus.AlreadyExists, result.Status);
        Assert.Equal(1, await db.FavoriteJobs.CountAsync());
    }

    [Fact]
    public async Task AddFavoriteAsync_ShouldValidateUserAndJob()
    {
        using var db = CreateDb();
        var existingUser = SeedUser(db);
        await db.SaveChangesAsync();

        var service = new FavoritesService(db);

        var missingUser = await service.AddFavoriteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        var missingJob = await service.AddFavoriteAsync(existingUser.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(AddFavoriteStatus.UserNotFound, missingUser.Status);
        Assert.Equal(AddFavoriteStatus.JobNotFound, missingJob.Status);
    }

    [Fact]
    public async Task RemoveFavoriteAsync_ShouldRemoveOrReturnNotFound()
    {
        using var db = CreateDb();
        var user = SeedUser(db);
        var job = SeedJob(db);
        db.FavoriteJobs.Add(new FavoriteJobEntity
        {
            UserId = user.Id,
            JobPostingId = job.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new FavoritesService(db);

        var removed = await service.RemoveFavoriteAsync(user.Id, job.Id, CancellationToken.None);
        var missing = await service.RemoveFavoriteAsync(user.Id, job.Id, CancellationToken.None);

        Assert.Equal(RemoveFavoriteStatus.Removed, removed.Status);
        Assert.Equal(RemoveFavoriteStatus.NotFound, missing.Status);
    }

    private static JobsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new JobsDbContext(options);
    }

    private static UserEntity SeedUser(JobsDbContext db)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            NormalizedEmail = "user@test.com",
            DisplayName = "User",
            Status = "Active",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(user);
        return user;
    }

    private static JobPostingEntity SeedJob(JobsDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var job = new JobPostingEntity
        {
            Id = Guid.NewGuid(),
            SourceName = "Fixture",
            SourceType = SourceType.Fixture,
            SourceUrl = $"https://example.com/job/{Guid.NewGuid():N}",
            Title = "Backend Developer",
            CompanyName = "Acme",
            LocationText = "Remote",
            WorkMode = WorkMode.Remote,
            Seniority = Seniority.Mid,
            EmploymentType = EmploymentType.CLT,
            DescriptionText = "A valid job description",
            CapturedAt = now,
            LastSeenAt = now,
            Status = JobStatus.Active,
            Fingerprint = Guid.NewGuid().ToString("N"),
            MetadataJson = "{}"
        };

        db.JobPostings.Add(job);
        return job;
    }
}
