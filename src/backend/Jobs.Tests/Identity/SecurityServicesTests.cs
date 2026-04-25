using Jobs.Infrastructure.Identity;

namespace Jobs.Tests.Identity;

public sealed class SecurityServicesTests
{
    [Fact]
    public void PasswordHasher_ShouldHashAndVerify()
    {
        var hasher = new UserPasswordHasher();

        var hash = hasher.HashPassword("StrongPassword123!");

        Assert.StartsWith("v1.", hash);
        Assert.True(hasher.VerifyHashedPassword(hash, "StrongPassword123!"));
        Assert.False(hasher.VerifyHashedPassword(hash, "wrong-password"));
    }

    [Fact]
    public void TokenService_ShouldGenerateAndHashDeterministically()
    {
        var tokens = new UserTokenService();

        var tokenA = tokens.GenerateToken();
        var tokenB = tokens.GenerateToken();

        Assert.NotEqual(tokenA, tokenB);
        Assert.NotEmpty(tokenA);

        var hash1 = tokens.HashToken(tokenA);
        var hash2 = tokens.HashToken(tokenA);
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }
}
