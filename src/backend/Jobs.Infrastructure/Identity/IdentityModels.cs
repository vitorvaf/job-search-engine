namespace Jobs.Infrastructure.Identity;

public static class UserActionTokenTypes
{
    public const string EmailVerification = "EmailVerification";
    public const string PasswordReset = "PasswordReset";
}

public enum RegisterLocalAccountStatus
{
    Created = 1,
    EmailAlreadyInUse = 2
}

public sealed record RegisterLocalAccountResult(RegisterLocalAccountStatus Status, Guid? UserId)
{
    public bool IsSuccess => Status == RegisterLocalAccountStatus.Created && UserId is not null;
}

public enum EmailVerificationStatus
{
    Verified = 1,
    InvalidOrExpiredToken = 2
}

public sealed record EmailVerificationResult(EmailVerificationStatus Status, Guid? UserId)
{
    public bool IsSuccess => Status == EmailVerificationStatus.Verified && UserId is not null;
}

public enum ResendEmailVerificationStatus
{
    Sent = 1,
    AccountNotFound = 2,
    AlreadyVerified = 3
}

public sealed record ResendEmailVerificationResult(ResendEmailVerificationStatus Status, Guid? UserId)
{
    public bool IsSuccess => Status == ResendEmailVerificationStatus.Sent && UserId is not null;
}

public enum PasswordAuthenticationStatus
{
    Success = 1,
    InvalidCredentials = 2,
    EmailNotVerified = 3
}

public sealed record PasswordAuthenticationResult(PasswordAuthenticationStatus Status, Guid? UserId)
{
    public bool IsSuccess => Status == PasswordAuthenticationStatus.Success && UserId is not null;
}

public enum PasswordResetStatus
{
    Reset = 1,
    InvalidOrExpiredToken = 2,
    AccountWithoutPassword = 3
}

public sealed record PasswordResetResult(PasswordResetStatus Status, Guid? UserId)
{
    public bool IsSuccess => Status == PasswordResetStatus.Reset && UserId is not null;
}

public enum OAuthSignInStatus
{
    Success = 1,
    MissingProviderSubject = 2,
    MissingEmail = 3,
    EmailNotVerified = 4
}

public sealed record OAuthSignInResult(OAuthSignInStatus Status, Guid? UserId)
{
    public bool IsSuccess => Status == OAuthSignInStatus.Success && UserId is not null;
}

public enum AddFavoriteStatus
{
    Added = 1,
    AlreadyExists = 2,
    UserNotFound = 3,
    JobNotFound = 4
}

public sealed record AddFavoriteResult(AddFavoriteStatus Status)
{
    public bool IsSuccess => Status == AddFavoriteStatus.Added || Status == AddFavoriteStatus.AlreadyExists;
}

public enum RemoveFavoriteStatus
{
    Removed = 1,
    NotFound = 2
}

public sealed record RemoveFavoriteResult(RemoveFavoriteStatus Status)
{
    public bool IsSuccess => Status == RemoveFavoriteStatus.Removed || Status == RemoveFavoriteStatus.NotFound;
}
