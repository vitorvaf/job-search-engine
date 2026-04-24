using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Jobs.Domain.Models;
using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Data.Entities;
using Jobs.Infrastructure.Identity;
using Jobs.Infrastructure.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Jobs.Tests.Identity;

public sealed class AccountEndpointsTests
{
    private const string InternalApiKey = "test-internal-api-key";

    [Fact]
    public async Task Post_Register_WithoutInternalApiKey_Returns401()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/account/register", new
        {
            email = "person@test.com",
            displayName = "Person",
            password = "Password@123"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_Register_Verify_ThenCredentialsAuth_Returns200()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", InternalApiKey);

        var registerResponse = await client.PostAsJsonAsync("/api/account/register", new
        {
            email = "person@test.com",
            displayName = "Person",
            password = "Password@123"
        });

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var emailSender = factory.Services.GetRequiredService<FakeEmailSender>();
        var sent = Assert.Single(emailSender.Messages);
        var token = ExtractToken(sent.TextBody);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
            var storedHash = await db.UserActionTokens
                .Where(x => x.Type == UserActionTokenTypes.EmailVerification)
                .Select(x => x.TokenHash)
                .SingleAsync();

            var computedHash = new UserTokenService().HashToken(token);
            Assert.Equal(storedHash, computedHash);
        }

        var verifyResponse = await client.PostAsJsonAsync("/api/account/verify-email", new { token });
        if (verifyResponse.StatusCode != HttpStatusCode.OK)
        {
            var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
            Assert.Fail($"Expected 200 from verify-email but got {(int)verifyResponse.StatusCode}: {verifyBody}");
        }

        var authResponse = await client.PostAsJsonAsync("/api/account/auth/credentials", new
        {
            email = "person@test.com",
            password = "Password@123"
        });

        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);

        var payload = await authResponse.Content.ReadFromJsonAsync<CredentialsAuthPayload>();
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload.UserId);
        Assert.Equal("person@test.com", payload.Email);
    }

    [Fact]
    public async Task FavoritesEndpoints_WithInternalKeyAndUserHeader_ReturnsExpectedFlow()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        Guid seededUserId;
        Guid seededJobId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
            var user = new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = "person@test.com",
                NormalizedEmail = "person@test.com",
                DisplayName = "Person",
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                Status = "Active",
                CreatedAt = DateTimeOffset.UtcNow
            };

            var now = DateTimeOffset.UtcNow;
            var job = new JobPostingEntity
            {
                Id = Guid.NewGuid(),
                SourceName = "Fixture",
                SourceType = SourceType.Fixture,
                SourceUrl = "https://example.com/jobs/1",
                Title = "Backend Engineer",
                CompanyName = "Acme",
                LocationText = "Remote",
                WorkMode = WorkMode.Remote,
                Seniority = Seniority.Mid,
                EmploymentType = EmploymentType.CLT,
                DescriptionText = "Valid description",
                CapturedAt = now,
                LastSeenAt = now,
                Status = JobStatus.Active,
                Fingerprint = Guid.NewGuid().ToString("N"),
                MetadataJson = "{}"
            };

            db.Users.Add(user);
            db.JobPostings.Add(job);
            await db.SaveChangesAsync();

            seededUserId = user.Id;
            seededJobId = job.Id;
        }

        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", InternalApiKey);
        client.DefaultRequestHeaders.Add("X-User-Id", seededUserId.ToString());

        var addResponse = await client.PutAsync($"/api/me/favorites/{seededJobId}", null);
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/me/favorites");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<FavoritesPayload>();
        Assert.NotNull(listPayload);
        Assert.Equal(1, listPayload.Total);
        Assert.Single(listPayload.Items);
        Assert.Equal(seededJobId, listPayload.Items[0].Id);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbRoot = new InMemoryDatabaseRoot();

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.ConfigureTestServices(services =>
            {
                var dbDescriptors = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<JobsDbContext>) ||
                        d.ServiceType == typeof(JobsDbContext) ||
                        (d.ServiceType.IsGenericType &&
                         d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal) &&
                         d.ServiceType.GenericTypeArguments.Length == 1 &&
                         d.ServiceType.GenericTypeArguments[0] == typeof(JobsDbContext)))
                    .ToList();

                foreach (var descriptor in dbDescriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton(dbRoot);
                services.AddDbContext<JobsDbContext>((sp, opt) =>
                    opt.UseInMemoryDatabase(dbName, sp.GetRequiredService<InMemoryDatabaseRoot>()));

                var emailDescriptors = services
                    .Where(d => d.ServiceType == typeof(IEmailSender))
                    .ToList();

                foreach (var descriptor in emailDescriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<FakeEmailSender>();
                services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<FakeEmailSender>());

                services.PostConfigure<AppOptions>(opts =>
                {
                    opts.Auth.BffInternalApiKey = InternalApiKey;
                    opts.PublicUrls.FrontendBaseUrl = "http://frontend.local";
                    opts.Auth.Tokens.EmailVerificationMinutes = 60;
                    opts.Auth.Tokens.PasswordResetMinutes = 30;
                });
            });
        });
    }

    private static string ExtractToken(string input)
    {
        var match = Regex.Match(input, @"token=([^\s&]+)", RegexOptions.IgnoreCase);
        Assert.True(match.Success);
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = new();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed record CredentialsAuthPayload(Guid UserId, string Email, string DisplayName, string? AvatarUrl);

    private sealed record FavoritesPayload(int Total, List<FavoriteJobPayload> Items);

    private sealed record FavoriteJobPayload(Guid Id, string Title);
}
