using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Data.Entities;
using Jobs.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobs.Infrastructure.Identity;

public interface IAccountService
{
    Task<RegisterLocalAccountResult> RegisterLocalAsync(string email, string displayName, string password, CancellationToken cancellationToken);
    Task<EmailVerificationResult> VerifyEmailAsync(string token, CancellationToken cancellationToken);
    Task<ResendEmailVerificationResult> ResendEmailVerificationAsync(string email, CancellationToken cancellationToken);
    Task<PasswordAuthenticationResult> AuthenticateWithPasswordAsync(string email, string password, CancellationToken cancellationToken);
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken);
    Task<PasswordResetResult> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken);
    Task<OAuthSignInResult> ResolveOAuthSignInAsync(
        string provider,
        string providerUserId,
        string email,
        bool isEmailVerified,
        string? displayName,
        string? avatarUrl,
        CancellationToken cancellationToken);
}

public sealed class AccountService : IAccountService
{
    private readonly JobsDbContext _db;
    private readonly IUserPasswordHasher _passwordHasher;
    private readonly IUserTokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IOptions<AppOptions> _appOptions;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        JobsDbContext db,
        IUserPasswordHasher passwordHasher,
        IUserTokenService tokenService,
        IEmailSender emailSender,
        IOptions<AppOptions> appOptions,
        ILogger<AccountService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _appOptions = appOptions;
        _logger = logger;
    }

    public async Task<RegisterLocalAccountResult> RegisterLocalAsync(string email, string displayName, string password, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var safeDisplayName = (displayName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var existingUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            return new RegisterLocalAccountResult(RegisterLocalAccountStatus.EmailAlreadyInUse, null);
        }

        var now = DateTimeOffset.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = safeDisplayName,
            Status = "Active",
            CreatedAt = now
        };

        var credential = new UserCredentialEntity
        {
            UserId = user.Id,
            PasswordHash = _passwordHasher.HashPassword(password),
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        _db.UserCredentials.Add(credential);

        var token = await CreateActionTokenAsync(
            user.Id,
            UserActionTokenTypes.EmailVerification,
            _appOptions.Value.Auth.Tokens.EmailVerificationMinutes,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await SendEmailVerificationAsync(user, token, cancellationToken);

        _logger.LogInformation("Local account registered: userId={UserId}", user.Id);
        return new RegisterLocalAccountResult(RegisterLocalAccountStatus.Created, user.Id);
    }

    public async Task<EmailVerificationResult> VerifyEmailAsync(string token, CancellationToken cancellationToken)
    {
        var userId = await ConsumeActionTokenAsync(token, UserActionTokenTypes.EmailVerification, cancellationToken);
        if (userId is null)
        {
            return new EmailVerificationResult(EmailVerificationStatus.InvalidOrExpiredToken, null);
        }

        var user = await _db.Users.FirstAsync(x => x.Id == userId.Value, cancellationToken);
        user.EmailVerifiedAt ??= DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return new EmailVerificationResult(EmailVerificationStatus.Verified, user.Id);
    }

    public async Task<ResendEmailVerificationResult> ResendEmailVerificationAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new ResendEmailVerificationResult(ResendEmailVerificationStatus.AccountNotFound, null);
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null)
        {
            return new ResendEmailVerificationResult(ResendEmailVerificationStatus.AccountNotFound, null);
        }

        if (user.EmailVerifiedAt is not null)
        {
            return new ResendEmailVerificationResult(ResendEmailVerificationStatus.AlreadyVerified, user.Id);
        }

        var token = await CreateActionTokenAsync(
            user.Id,
            UserActionTokenTypes.EmailVerification,
            _appOptions.Value.Auth.Tokens.EmailVerificationMinutes,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await SendEmailVerificationAsync(user, token, cancellationToken);

        return new ResendEmailVerificationResult(ResendEmailVerificationStatus.Sent, user.Id);
    }

    public async Task<PasswordAuthenticationResult> AuthenticateWithPasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new PasswordAuthenticationResult(PasswordAuthenticationStatus.InvalidCredentials, null);
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null)
        {
            return new PasswordAuthenticationResult(PasswordAuthenticationStatus.InvalidCredentials, null);
        }

        var credential = await _db.UserCredentials.FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        if (credential is null || !_passwordHasher.VerifyHashedPassword(credential.PasswordHash, password))
        {
            return new PasswordAuthenticationResult(PasswordAuthenticationStatus.InvalidCredentials, null);
        }

        if (user.EmailVerifiedAt is null)
        {
            return new PasswordAuthenticationResult(PasswordAuthenticationStatus.EmailNotVerified, null);
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new PasswordAuthenticationResult(PasswordAuthenticationStatus.Success, user.Id);
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return;
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null)
        {
            return;
        }

        var hasCredentials = await _db.UserCredentials.AnyAsync(x => x.UserId == user.Id, cancellationToken);
        if (!hasCredentials)
        {
            return;
        }

        var token = await CreateActionTokenAsync(
            user.Id,
            UserActionTokenTypes.PasswordReset,
            _appOptions.Value.Auth.Tokens.PasswordResetMinutes,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await SendPasswordResetAsync(user, token, cancellationToken);
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        var userId = await ConsumeActionTokenAsync(token, UserActionTokenTypes.PasswordReset, cancellationToken);
        if (userId is null)
        {
            return new PasswordResetResult(PasswordResetStatus.InvalidOrExpiredToken, null);
        }

        var credential = await _db.UserCredentials.FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);
        if (credential is null)
        {
            return new PasswordResetResult(PasswordResetStatus.AccountWithoutPassword, userId.Value);
        }

        credential.PasswordHash = _passwordHasher.HashPassword(newPassword);
        credential.UpdatedAt = DateTimeOffset.UtcNow;
        await InvalidateActiveTokensAsync(userId.Value, UserActionTokenTypes.PasswordReset, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new PasswordResetResult(PasswordResetStatus.Reset, userId.Value);
    }

    public async Task<OAuthSignInResult> ResolveOAuthSignInAsync(
        string provider,
        string providerUserId,
        string email,
        bool isEmailVerified,
        string? displayName,
        string? avatarUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerUserId))
        {
            return new OAuthSignInResult(OAuthSignInStatus.MissingProviderSubject, null);
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return new OAuthSignInResult(OAuthSignInStatus.MissingEmail, null);
        }

        if (!isEmailVerified)
        {
            return new OAuthSignInResult(OAuthSignInStatus.EmailNotVerified, null);
        }

        var safeProvider = provider.Trim();
        var normalizedEmail = NormalizeEmail(email);
        var now = DateTimeOffset.UtcNow;

        var identity = await _db.UserIdentities.FirstOrDefaultAsync(
            x => x.Provider == safeProvider && x.ProviderUserId == providerUserId,
            cancellationToken);

        UserEntity user;
        if (identity is not null)
        {
            user = await _db.Users.FirstAsync(x => x.Id == identity.UserId, cancellationToken);
            identity.ProviderEmail = email.Trim();
        }
        else
        {
            user = await _db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken)
                ?? new UserEntity
                {
                    Id = Guid.NewGuid(),
                    Email = email.Trim(),
                    NormalizedEmail = normalizedEmail,
                    DisplayName = string.Empty,
                    Status = "Active",
                    CreatedAt = now
                };

            if (_db.Entry(user).State == EntityState.Detached)
            {
                _db.Users.Add(user);
            }

            identity = new UserIdentityEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = safeProvider,
                ProviderUserId = providerUserId.Trim(),
                ProviderEmail = email.Trim(),
                CreatedAt = now
            };

            _db.UserIdentities.Add(identity);
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            user.AvatarUrl = avatarUrl.Trim();
        }

        user.EmailVerifiedAt ??= now;
        user.LastLoginAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return new OAuthSignInResult(OAuthSignInStatus.Success, user.Id);
    }

    private async Task<string> CreateActionTokenAsync(Guid userId, string tokenType, int ttlMinutes, CancellationToken cancellationToken)
    {
        await InvalidateActiveTokensAsync(userId, tokenType, cancellationToken);

        var token = _tokenService.GenerateToken();
        var hash = _tokenService.HashToken(token);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, ttlMinutes));

        _db.UserActionTokens.Add(new UserActionTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = tokenType,
            TokenHash = hash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        });

        return token;
    }

    private async Task<Guid?> ConsumeActionTokenAsync(string token, string tokenType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = _tokenService.HashToken(token);
        var now = DateTimeOffset.UtcNow;

        var entity = await _db.UserActionTokens.FirstOrDefaultAsync(
            x => x.Type == tokenType && x.TokenHash == tokenHash && x.ConsumedAt == null,
            cancellationToken);

        if (entity is null || entity.ExpiresAt <= now)
        {
            return null;
        }

        entity.ConsumedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        return entity.UserId;
    }

    private async Task InvalidateActiveTokensAsync(Guid userId, string tokenType, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var activeTokens = await _db.UserActionTokens
            .Where(x => x.UserId == userId && x.Type == tokenType && x.ConsumedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var activeToken in activeTokens)
        {
            activeToken.ConsumedAt = now;
        }
    }

    private async Task SendEmailVerificationAsync(UserEntity user, string token, CancellationToken cancellationToken)
    {
        var frontend = _appOptions.Value.PublicUrls.FrontendBaseUrl.TrimEnd('/');
        var link = $"{frontend}/verificar-email?token={Uri.EscapeDataString(token)}";
        var body = $"Confirme seu email acessando: {link}";

        await _emailSender.SendAsync(new EmailMessage(
            user.Email,
            "Confirme seu email",
            body,
            $"<p>Confirme seu email acessando: <a href=\"{link}\">{link}</a></p>",
            user.DisplayName), cancellationToken);
    }

    private async Task SendPasswordResetAsync(UserEntity user, string token, CancellationToken cancellationToken)
    {
        var frontend = _appOptions.Value.PublicUrls.FrontendBaseUrl.TrimEnd('/');
        var link = $"{frontend}/redefinir-senha?token={Uri.EscapeDataString(token)}";
        var body = $"Voce solicitou redefinicao de senha. Use este link: {link}";

        await _emailSender.SendAsync(new EmailMessage(
            user.Email,
            "Redefina sua senha",
            body,
            $"<p>Voce solicitou redefinicao de senha. Use este link: <a href=\"{link}\">{link}</a></p>",
            user.DisplayName), cancellationToken);
    }

    private static string NormalizeEmail(string? email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }
}
