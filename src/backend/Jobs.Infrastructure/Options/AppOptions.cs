namespace Jobs.Infrastructure.Options;

public sealed class AppOptions
{
    public string SamplesPath { get; set; } = "../docs/samples";
    public string SearchIndexName { get; set; } = "jobs";
    public IngestionOptions Ingestion { get; set; } = new();
    public HttpSourceOptions Http { get; set; } = new();
    public SourcesOptions Sources { get; set; } = new();
}

public sealed class IngestionOptions
{
    public int MaxItemsPerRunDefault { get; set; } = 20;
    public int MaxDetailFetchDefault { get; set; } = 20;
    public int ExpireAfterDays { get; set; } = 14;
}

public sealed class HttpSourceOptions
{
    public string UserAgent { get; set; } = "JobSearchEngineBot/0.1 (contact: unknown)";
    public int TimeoutSeconds { get; set; } = 20;
    public int MaxRetries { get; set; } = 3;
    public int InitialBackoffMs { get; set; } = 500;
}

public sealed class SourcesOptions
{
    public InfoJobsSourceOptions InfoJobs { get; set; } = new();
    public StoneSourceOptions Stone { get; set; } = new();
    public AccentureWorkdaySourceOptions AccentureWorkday { get; set; } = new();
    public List<CorporateCareerSourceOptions> CorporateCareers { get; set; } = new();
    public List<JsonLdSourceOptions> JsonLd { get; set; } = new();
    public List<GupyCompanySourceOptions> GupyCompanies { get; set; } = new();
}

public sealed class InfoJobsSourceOptions
{
    public bool Enabled { get; set; } = true;
    public string SearchUrl { get; set; } = "https://www.infojobs.com.br/vagas.aspx?palabra=TI";
}

public sealed class StoneSourceOptions
{
    public bool Enabled { get; set; }
    public string SearchUrl { get; set; } = "https://trabalheconosco.vagas.com.br/stone";
}

public sealed class AccentureWorkdaySourceOptions
{
    public bool Enabled { get; set; }
    public string BaseHost { get; set; } = "accenture.wd103.myworkdayjobs.com";
    public string SitePath { get; set; } = "/pt-BR/AccentureCareers";
    public string Tenant { get; set; } = "accenture";
    public string SiteName { get; set; } = "AccentureCareers";
    public int PageSize { get; set; } = 50;
    public int MaxPagesPerRun { get; set; } = 3;
    public int? MaxDetailFetch { get; set; } = 10;
    public string UserAgent { get; set; } = "JobSearchEngineBot/0.1 (contact: unknown)";
}

public sealed class CorporateCareerSourceOptions
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public string Type { get; set; } = "CorporateCareers";
    public string StartUrl { get; set; } = "";
    public int? MaxItemsPerRun { get; set; }
    public int? MaxDetailFetch { get; set; }
}

public sealed class JsonLdSourceOptions
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public string StartUrl { get; set; } = "";
    public int? MaxItemsPerRun { get; set; }
    public int? MaxDetailFetch { get; set; }
}

public sealed class GupyCompanySourceOptions
{
    public bool Enabled { get; set; }
    public string Name { get; set; } = "";
    public string CompanyBaseUrl { get; set; } = "";
    public int? MaxItemsPerRun { get; set; }
    public int? MaxDetailFetch { get; set; }
}
