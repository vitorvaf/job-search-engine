namespace Jobs.Infrastructure.Options;

public sealed class MeiliOptions
{
    public string BaseUrl { get; set; } = "http://localhost:7700";
    public string MasterKey { get; set; } = "dev_master_key_change_me";
}
