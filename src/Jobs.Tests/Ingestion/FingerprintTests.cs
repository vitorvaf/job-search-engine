using Jobs.Infrastructure.Ingestion;

namespace Jobs.Tests.Ingestion;

public sealed class FingerprintTests
{
    private readonly Fingerprint _fingerprint = new();

    [Fact]
    public void Compute_ShouldBeDeterministic()
    {
        var first = _fingerprint.Compute("Empresa X", "Software Engineer .NET", "São Paulo, SP", "Hybrid");
        var second = _fingerprint.Compute("Empresa X", "Software Engineer .NET", "São Paulo, SP", "Hybrid");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_ShouldIgnoreCaseAccentPunctuationAndWhitespace()
    {
        var first = _fingerprint.Compute("Empresa X", "Software Engineer - .NET", "São   Paulo, SP", "Hybrid");
        var second = _fingerprint.Compute("  empresa x ", "software engineer net", "Sao Paulo SP", " hybrid ");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_ShouldChangeWhenCoreFieldsChange()
    {
        var first = _fingerprint.Compute("Empresa X", "Software Engineer .NET", "Remote - Brazil", "Remote");
        var second = _fingerprint.Compute("Empresa X", "Software Engineer .NET", "Sao Paulo, SP", "Hybrid");

        Assert.NotEqual(first, second);
    }
}
