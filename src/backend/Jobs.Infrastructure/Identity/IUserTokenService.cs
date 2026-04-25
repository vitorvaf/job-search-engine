using System.Security.Cryptography;
using System.Text;

namespace Jobs.Infrastructure.Identity;

public interface IUserTokenService
{
    string GenerateToken();
    string HashToken(string token);
}

public sealed class UserTokenService : IUserTokenService
{
    private const int TokenBytes = 32;

    public string GenerateToken()
    {
        var raw = RandomNumberGenerator.GetBytes(TokenBytes);
        var token = Convert.ToBase64String(raw)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return token;
    }

    public string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token is required.", nameof(token));
        }

        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
