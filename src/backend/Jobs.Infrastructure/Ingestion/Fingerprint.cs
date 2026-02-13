using System.Security.Cryptography;
using System.Text;

namespace Jobs.Infrastructure.Ingestion;

public sealed class Fingerprint
{
    public string Normalize(string raw)
    {
        return JobTextNormalizer.Normalize(raw);
    }

    public string Compute(string companyName, string title, string locationText, string workMode)
    {
        var normalized =
            $"{Normalize(companyName)}|{Normalize(title)}|{Normalize(locationText)}|{Normalize(workMode)}";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
