using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Tests.Data;

public sealed class JobsDbContextModelTests
{
    [Fact]
    public void Model_ShouldMapNewAuthAndFavoritesEntitiesToExpectedTables()
    {
        using var db = CreateDb();
        var model = db.Model;

        Assert.Equal("users", model.FindEntityType(typeof(UserEntity))?.GetTableName());
        Assert.Equal("user_credentials", model.FindEntityType(typeof(UserCredentialEntity))?.GetTableName());
        Assert.Equal("user_identities", model.FindEntityType(typeof(UserIdentityEntity))?.GetTableName());
        Assert.Equal("user_action_tokens", model.FindEntityType(typeof(UserActionTokenEntity))?.GetTableName());
        Assert.Equal("favorite_jobs", model.FindEntityType(typeof(FavoriteJobEntity))?.GetTableName());
    }

    [Fact]
    public void Model_ShouldConfigureUserConstraintsAndIndexes()
    {
        using var db = CreateDb();
        var user = db.Model.FindEntityType(typeof(UserEntity));
        Assert.NotNull(user);

        var userPk = user!.FindPrimaryKey();
        Assert.NotNull(userPk);
        Assert.Equal(new[] { nameof(UserEntity.Id) }, userPk!.Properties.Select(p => p.Name));

        Assert.Contains(user.GetIndexes(), idx =>
            idx.IsUnique &&
            idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(UserEntity.NormalizedEmail) }));

        var status = user.FindProperty(nameof(UserEntity.Status));
        Assert.NotNull(status);
        Assert.Equal("Active", status!.GetDefaultValue());

        var createdAt = user.FindProperty(nameof(UserEntity.CreatedAt));
        Assert.NotNull(createdAt);
        Assert.Equal("now()", createdAt!.GetDefaultValueSql());
    }

    [Fact]
    public void Model_ShouldConfigureCredentialIdentityAndTokenIndexes()
    {
        using var db = CreateDb();

        var credentials = db.Model.FindEntityType(typeof(UserCredentialEntity));
        Assert.NotNull(credentials);
        Assert.Equal(new[] { nameof(UserCredentialEntity.UserId) },
            credentials!.FindPrimaryKey()!.Properties.Select(p => p.Name));

        var identities = db.Model.FindEntityType(typeof(UserIdentityEntity));
        Assert.NotNull(identities);
        Assert.Contains(identities!.GetIndexes(), idx =>
            idx.IsUnique && idx.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { nameof(UserIdentityEntity.Provider), nameof(UserIdentityEntity.ProviderUserId) }));
        Assert.Contains(identities.GetIndexes(), idx =>
            idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(UserIdentityEntity.UserId) }));

        var actionTokens = db.Model.FindEntityType(typeof(UserActionTokenEntity));
        Assert.NotNull(actionTokens);
        Assert.Contains(actionTokens!.GetIndexes(), idx =>
            idx.IsUnique && idx.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { nameof(UserActionTokenEntity.Type), nameof(UserActionTokenEntity.TokenHash) }));
        Assert.Contains(actionTokens.GetIndexes(), idx =>
            idx.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { nameof(UserActionTokenEntity.UserId), nameof(UserActionTokenEntity.Type) }));
        Assert.Contains(actionTokens.GetIndexes(), idx =>
            idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(UserActionTokenEntity.ExpiresAt) }));
    }

    [Fact]
    public void Model_ShouldConfigureFavoriteJobCompositeKeyAndCascadeRelationships()
    {
        using var db = CreateDb();
        var favorites = db.Model.FindEntityType(typeof(FavoriteJobEntity));
        Assert.NotNull(favorites);

        var pk = favorites!.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Equal(
            new[] { nameof(FavoriteJobEntity.UserId), nameof(FavoriteJobEntity.JobPostingId) },
            pk!.Properties.Select(p => p.Name));

        Assert.Contains(favorites.GetIndexes(), idx =>
            idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(FavoriteJobEntity.JobPostingId) }));

        var userFk = favorites.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(UserEntity));
        Assert.Equal(DeleteBehavior.Cascade, userFk.DeleteBehavior);
        Assert.Equal(new[] { nameof(FavoriteJobEntity.UserId) }, userFk.Properties.Select(p => p.Name));

        var jobFk = favorites.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(JobPostingEntity));
        Assert.Equal(DeleteBehavior.Cascade, jobFk.DeleteBehavior);
        Assert.Equal(new[] { nameof(FavoriteJobEntity.JobPostingId) }, jobFk.Properties.Select(p => p.Name));
    }

    private static JobsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new JobsDbContext(options);
    }
}
