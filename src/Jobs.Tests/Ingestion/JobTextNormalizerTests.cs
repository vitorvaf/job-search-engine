using Jobs.Infrastructure.Ingestion;

namespace Jobs.Tests.Ingestion;

public sealed class JobTextNormalizerTests
{
    [Theory]
    [InlineData("  Sênior .NET Engineer  ", "senior net engineer")]
    [InlineData("São   Paulo, SP (Híbrido)", "sao paulo sp hibrido")]
    [InlineData("QA/Test-Automation!!!", "qa test automation")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_ShouldApplyCanonicalRules(string input, string expected)
    {
        var normalized = JobTextNormalizer.Normalize(input);

        Assert.Equal(expected, normalized);
    }
}
