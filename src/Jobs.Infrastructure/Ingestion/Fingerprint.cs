using System.Security.Cryptography;
using System.Text;

namespace Jobs.Infrastructure.Ingestion;

public sealed class Fingerprint
{
    public string Compute(string companyName, string title, string locationText, string workMode)
    {
        var normalized =
            $"{JobTextNormalizer.Normalize(companyName)}|{JobTextNormalizer.Normalize(title)}|{JobTextNormalizer.Normalize(locationText)}|{JobTextNormalizer.Normalize(workMode)}";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
