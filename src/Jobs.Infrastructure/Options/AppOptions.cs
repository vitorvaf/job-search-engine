namespace Jobs.Infrastructure.Options;

public sealed class AppOptions
{
    public string SamplesPath { get; set; } = "../docs/samples";
    public string SearchIndexName { get; set; } = "jobs";
}
