using System.Text.RegularExpressions;
using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Data.Entities;
using Jobs.Infrastructure.Identity;
using Jobs.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jobs.Tests.Identity;

public sealed class AccountServiceTests
{
    [Fact]
    public async Task RegisterLocalAsync_ShouldCreateAccountAndSendVerificationEmail()
    {
        using var db = CreateDb();
        var emails = new FakeEmailSender();
        var tokens = new UserTokenService();
        var service = CreateService(db, emails, tokens);

        var result = await service.RegisterLocalAsync(
            email: "User@Test.com",
            displayName: "User Test",
            password: "Password@123",
            cancellationToken: CancellationToken.None);

        Assert.Equal(RegisterLocalAccountStatus.Created, result.Status);
        Assert.NotNull(result.UserId);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.UserCredentials.CountAsync());
        Assert.Equal(1, await db.UserActionTokens.CountAsync(x => x.Type == UserActionTokenTypes.EmailVerification));

        var sent = Assert.Single(emails.Messages);
        Assert.Equal("Confirme seu email", sent.Subject);
        Assert.Contains("/verificar-email?token=", sent.TextBody);

        var rawToken = ExtractToken(sent.TextBody);
        var storedHash = await db.UserActionTokens.Select(x => x.TokenHash).SingleAsync();
        Assert.Equal(tokens.HashToken(rawToken), storedHash);
    }

    [Fact]
    public async Task RegisterLocalAsync_WhenEmailAlreadyExists_ShouldReturnConflictWithoutSendingEmail()
    {
        using var db = CreateDb();
        db.Users.Add(new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "existing@test.com",
            NormalizedEmail = "existing@test.com",
            DisplayName = "Existing",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var emails = new FakeEmailSender();
        var service = CreateService(db, emails, new UserTokenService());

        var result = await service.RegisterLocalAsync(
            email: "Existing@Test.com",
            displayName: "Another",
            password: "Password@123",
            cancellationToken: CancellationToken.None);

        Assert.Equal(RegisterLocalAccountStatus.EmailAlreadyInUse, result.Status);
        Assert.Empty(emails.Messages);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(0, await db.UserCredentials.CountAsync());
    }

    [Fact]
    public async Task AuthenticateWithPasswordAsync_ShouldRequireEmailVerification()
    {
        using var db = CreateDb();
        var emails = new FakeEmailSender();
        var service = CreateService(db, emails, new UserTokenService());

        await service.RegisterLocalAsync("person@test.com", "Person", "Password@123", CancellationToken.None);

        var auth = await service.AuthenticateWithPasswordAsync("person@test.com", "Password@123", CancellationToken.None);

        Assert.Equal(PasswordAuthenticationStatus.EmailNotVerified, auth.Status);
    }

    [Fact]
    public async Task VerifyEmailAsync_WithValidToken_ShouldMarkAccountAsVerified()
    {
        using var db = CreateDb();
        var emails = new FakeEmailSender();
        var service = CreateService(db, emails, new UserTokenService());

        await service.RegisterLocalAsync("person@test.com", "Person", "Password@123", CancellationToken.None);
        var verificationToken = ExtractToken(Assert.Single(emails.Messages).TextBody);

        var verify = await service.VerifyEmailAsync(verificationToken, CancellationToken.None);
        var auth = await service.AuthenticateWithPasswordAsync("person@test.com", "Password@123", CancellationToken.None);

        Assert.Equal(EmailVerificationStatus.Verified, verify.Status);
        Assert.Equal(PasswordAuthenticationStatus.Success, auth.Status);

        var user = await db.Users.SingleAsync();
        Assert.NotNull(user.EmailVerifiedAt);
    }

    [Fact]
    public async Task PasswordResetFlow_ShouldIssueTokenResetPasswordAndAllowNewLogin()
    {
        using var db = CreateDb();
        var emails = new FakeEmailSender();
        var service = CreateService(db, emails, new UserTokenService());

        await service.RegisterLocalAsync("person@test.com", "Person", "OldPassword@123", CancellationToken.None);
        var verificationToken = ExtractToken(Assert.Single(emails.Messages).TextBody);
        await service.VerifyEmailAsync(verificationToken, CancellationToken.None);

        emails.Messages.Clear();
        await service.RequestPasswordResetAsync("person@test.com", CancellationToken.None);
        var resetEmail = Assert.Single(emails.Messages);
        var resetToken = ExtractToken(resetEmail.TextBody);

        var reset = await service.ResetPasswordAsync(resetToken, "NewPassword@123", CancellationToken.None);
        var oldAuth = await service.AuthenticateWithPasswordAsync("person@test.com", "OldPassword@123", CancellationToken.None);
        var newAuth = await service.AuthenticateWithPasswordAsync("person@test.com", "NewPassword@123", CancellationToken.None);

        Assert.Equal(PasswordResetStatus.Reset, reset.Status);
        Assert.Equal(PasswordAuthenticationStatus.InvalidCredentials, oldAuth.Status);
        Assert.Equal(PasswordAuthenticationStatus.Success, newAuth.Status);
    }

    [Fact]
    public async Task ResolveOAuthSignInAsync_WithVerifiedEmail_ShouldCreateUserAndIdentity()
    {
        using var db = CreateDb();
        var emails = new FakeEmailSender();
        var service = CreateService(db, emails, new UserTokenService());

        var result = await service.ResolveOAuthSignInAsync(
            provider: "google",
            providerUserId: "google-sub-123",
            email: "oauth@test.com",
            isEmailVerified: true,
            displayName: "OAuth User",
            avatarUrl: "https://cdn.example/avatar.png",
            cancellationToken: CancellationToken.None);

        Assert.Equal(OAuthSignInStatus.Success, result.Status);
        Assert.NotNull(result.UserId);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.UserIdentities.CountAsync());

        var user = await db.Users.SingleAsync();
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.Equal("oauth@test.com", user.NormalizedEmail);
    }

    [Fact]
    public async Task ResolveOAuthSignInAsync_WhenEmailNotVerified_ShouldFail()
    {
        using var db = CreateDb();
        var emails = new FakeEmailSender();
        var service = CreateService(db, emails, new UserTokenService());

        var result = await service.ResolveOAuthSignInAsync(
            provider: "github",
            providerUserId: "gh-abc",
            email: "dev@test.com",
            isEmailVerified: false,
            displayName: "Dev",
            avatarUrl: null,
            cancellationToken: CancellationToken.None);

        Assert.Equal(OAuthSignInStatus.EmailNotVerified, result.Status);
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.UserIdentities.CountAsync());
    }

    private static JobsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new JobsDbContext(options);
    }

    private static AccountService CreateService(JobsDbContext db, FakeEmailSender emailSender, IUserTokenService tokenService)
    {
        var appOptions = Options.Create(new AppOptions
        {
            PublicUrls = new PublicUrlsOptions { FrontendBaseUrl = "http://localhost:3000" },
            Auth = new AuthOptions
            {
                Tokens = new TokenTtlOptions
                {
                    EmailVerificationMinutes = 60,
                    PasswordResetMinutes = 30
                }
            }
        });

        return new AccountService(
            db,
            new UserPasswordHasher(),
            tokenService,
            emailSender,
            appOptions,
            NullLogger<AccountService>.Instance);
    }

    private static string ExtractToken(string body)
    {
        var match = Regex.Match(body, @"token=([^\s&]+)", RegexOptions.IgnoreCase);
        Assert.True(match.Success);
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = new();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
