using Jobs.Infrastructure.Options;
using Microsoft.Extensions.Configuration;

namespace Jobs.Tests.Configuration;

public sealed class AppOptionsTests
{
    [Fact]
    public void NewAppOptions_ShouldExposeExpectedDefaultsForAuthEmailAndPublicUrls()
    {
        var options = new AppOptions();

        Assert.Equal("dev_internal_key_change_me", options.Auth.BffInternalApiKey);
        Assert.Equal(1440, options.Auth.Tokens.EmailVerificationMinutes);
        Assert.Equal(30, options.Auth.Tokens.PasswordResetMinutes);

        Assert.Equal("Jobs", options.Email.FromName);
        Assert.Equal("no-reply@job-search.local", options.Email.FromAddress);
        Assert.Equal("localhost", options.Email.Smtp.Host);
        Assert.Equal(1025, options.Email.Smtp.Port);
        Assert.False(options.Email.Smtp.UseSsl);

        Assert.Equal("http://localhost:3000", options.PublicUrls.FrontendBaseUrl);
    }

    [Fact]
    public void Bind_ShouldMapNestedAuthEmailAndPublicUrlsSections()
    {
        var data = new Dictionary<string, string?>
        {
            ["App:Auth:BffInternalApiKey"] = "internal-key-test",
            ["App:Auth:Tokens:EmailVerificationMinutes"] = "180",
            ["App:Auth:Tokens:PasswordResetMinutes"] = "45",

            ["App:Email:FromName"] = "Jobs Test",
            ["App:Email:FromAddress"] = "noreply@test.local",
            ["App:Email:Smtp:Host"] = "mailpit",
            ["App:Email:Smtp:Port"] = "2025",
            ["App:Email:Smtp:UseSsl"] = "true",
            ["App:Email:Smtp:Username"] = "smtp-user",
            ["App:Email:Smtp:Password"] = "smtp-pass",

            ["App:PublicUrls:FrontendBaseUrl"] = "https://jobs.example.com"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();

        var options = new AppOptions();
        config.GetSection("App").Bind(options);

        Assert.Equal("internal-key-test", options.Auth.BffInternalApiKey);
        Assert.Equal(180, options.Auth.Tokens.EmailVerificationMinutes);
        Assert.Equal(45, options.Auth.Tokens.PasswordResetMinutes);

        Assert.Equal("Jobs Test", options.Email.FromName);
        Assert.Equal("noreply@test.local", options.Email.FromAddress);
        Assert.Equal("mailpit", options.Email.Smtp.Host);
        Assert.Equal(2025, options.Email.Smtp.Port);
        Assert.True(options.Email.Smtp.UseSsl);
        Assert.Equal("smtp-user", options.Email.Smtp.Username);
        Assert.Equal("smtp-pass", options.Email.Smtp.Password);

        Assert.Equal("https://jobs.example.com", options.PublicUrls.FrontendBaseUrl);
    }
}
