using System.Text.Json;
using Jobs.Domain.Models;
using Jobs.Infrastructure;
using Jobs.Infrastructure.BulkIngestion;
using Jobs.Infrastructure.Data;
using Jobs.Infrastructure.Identity;
using Jobs.Infrastructure.Options;
using Jobs.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddJobsInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Test"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
    await db.Database.EnsureCreatedAsync();

    var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>();
    var meili = scope.ServiceProvider.GetRequiredService<MeiliClient>();
    await meili.EnsureIndexAsync(appOptions.Value.SearchIndexName, CancellationToken.None);
}

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/sources", async (JobsDbContext db, CancellationToken ct) =>
{
    var sources = await db.Sources
        .OrderBy(s => s.Name)
        .Select(s => new { s.Id, s.Name, Type = s.Type.ToString(), s.BaseUrl, s.Enabled })
        .ToListAsync(ct);

    return Results.Ok(sources);
});

app.MapGet("/api/jobs/{id:guid}", async (Guid id, JobsDbContext db, CancellationToken ct) =>
{
    var job = await db.JobPostings.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (job is null)
    {
        return Results.NotFound();
    }

    var metadata = TryParseJson(job.MetadataJson);

    return Results.Ok(new
    {
        id = job.Id,
        title = job.Title,
        company = new { name = job.CompanyName, website = job.CompanyWebsite, industry = job.CompanyIndustry },
        locationText = job.LocationText,
        location = new { country = job.Country, state = job.State, city = job.City },
        workMode = job.WorkMode.ToString(),
        seniority = job.Seniority.ToString(),
        employmentType = job.EmploymentType.ToString(),
        salary = job.SalaryMin is null && job.SalaryMax is null
            ? null
            : new { min = job.SalaryMin, max = job.SalaryMax, currency = job.SalaryCurrency, period = job.SalaryPeriod },
        descriptionText = job.DescriptionText,
        tags = job.Tags,
        languages = job.Languages,
        source = new { name = job.SourceName, type = job.SourceType.ToString(), url = job.SourceUrl, sourceJobId = job.SourceJobId },
        postedAt = job.PostedAt,
        capturedAt = job.CapturedAt,
        lastSeenAt = job.LastSeenAt,
        status = job.Status.ToString(),
        dedupe = new { fingerprint = job.Fingerprint, clusterId = job.ClusterId },
        metadata
    });
});

app.MapGet("/api/jobs", async (
    string? q,
    string? tags,
    string? workMode,
    string? seniority,
    string? employmentType,
    string? sourceName,
    string? company,
    string? location,
    DateTime? postedFrom,
    string? sort,
    int page,
    int pageSize,
    MeiliClient meili,
    IOptions<AppOptions> appOptions,
    CancellationToken ct) =>
{
    page = page <= 0 ? 1 : page;
    pageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

    var parsedTags = tags?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(t => t.ToLowerInvariant())
        .Distinct()
        .ToArray() ?? Array.Empty<string>();

    var filters = new List<string>();

    if (TryParseEnum(workMode, out WorkMode workModeFilter) && workModeFilter != WorkMode.Unknown)
    {
        filters.Add($"workMode = \"{workModeFilter}\"");
    }

    if (TryParseEnum(seniority, out Seniority seniorityFilter) && seniorityFilter != Seniority.Unknown)
    {
        filters.Add($"seniority = \"{seniorityFilter}\"");
    }

    if (TryParseEnum(employmentType, out EmploymentType employmentTypeFilter) && employmentTypeFilter != EmploymentType.Unknown)
    {
        filters.Add($"employmentType = \"{employmentTypeFilter}\"");
    }

    if (parsedTags.Length > 0)
    {
        var tagExpr = string.Join(" OR ", parsedTags.Select(t => $"tags = \"{EscapeFilterValue(t)}\""));
        filters.Add($"({tagExpr})");
    }

    if (!string.IsNullOrWhiteSpace(company))
    {
        filters.Add($"company = \"{EscapeFilterValue(company)}\"");
    }

    if (!string.IsNullOrWhiteSpace(location))
    {
        filters.Add($"locationText = \"{EscapeFilterValue(location)}\"");
    }

    if (!string.IsNullOrWhiteSpace(sourceName))
    {
        filters.Add($"sourceName = \"{EscapeFilterValue(sourceName)}\"");
    }

    if (postedFrom is not null)
    {
        var floor = postedFrom.Value.Date.ToString("yyyy-MM-dd");
        filters.Add($"postedAt >= \"{floor}\"");
    }

    await meili.EnsureIndexAsync(appOptions.Value.SearchIndexName, ct);

    var sortOrder = ResolveSort(sort);

    var raw = await meili.SearchAsync(appOptions.Value.SearchIndexName, new
    {
        q = q ?? string.Empty,
        filter = filters.Count > 0 ? string.Join(" AND ", filters) : null,
        sort = sortOrder,
        offset = (page - 1) * pageSize,
        limit = pageSize
    }, ct);

    var hits = raw.GetProperty("hits").EnumerateArray()
        .Where(hit => !hit.TryGetProperty("status", out var statusEl) ||
                      string.Equals(statusEl.GetString(), "Active", StringComparison.OrdinalIgnoreCase))
        .ToList();
    var total = raw.TryGetProperty("estimatedTotalHits", out var estimated)
        ? estimated.GetInt32()
        : hits.Count;

    var paged = hits
        .Select(hit => new
        {
            id = hit.GetProperty("id").GetGuid(),
            title = hit.GetProperty("title").GetString(),
            company = new { name = hit.GetProperty("company").GetString() },
            locationText = hit.TryGetProperty("locationText", out var loc) ? loc.GetString() : null,
            workMode = hit.TryGetProperty("workMode", out var wm) ? wm.GetString() : null,
            seniority = hit.TryGetProperty("seniority", out var sr) ? sr.GetString() : null,
            employmentType = hit.TryGetProperty("employmentType", out var et) ? et.GetString() : null,
            tags = hit.TryGetProperty("tags", out var tg)
                ? tg.EnumerateArray().Select(x => x.GetString()).Where(x => x is not null).Cast<string>().ToArray()
                : Array.Empty<string>(),
            postedAt = hit.TryGetProperty("postedAt", out var pa) ? pa.GetString() : null,
            capturedAt = hit.TryGetProperty("capturedAt", out var ca) ? ca.GetString() : null,
            source = new
            {
                name = hit.TryGetProperty("sourceName", out var sn) ? sn.GetString() : null,
                url = hit.TryGetProperty("sourceUrl", out var su) ? su.GetString() : null
            }
        })
        .ToList();

    return Results.Ok(new
    {
        page,
        pageSize,
        total,
        items = paged
    });
});

app.MapPost("/api/ingestion/jobs/bulk", async (
    BulkIngestionRequest request,
    BulkJobIngestionService ingestionService,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    // Security: validate X-Ingestion-Key header when a key is configured
    var providedKey = httpContext.Request.Headers["X-Ingestion-Key"].FirstOrDefault();
    if (!ingestionService.IsApiKeyValid(providedKey))
    {
        return Results.Problem(
            title: "Unauthorized",
            detail: "X-Ingestion-Key header is missing or invalid.",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    // Validate batch size
    if (request.Items is null || request.Items.Count == 0)
    {
        return Results.Problem(
            title: "Bad Request",
            detail: "items must contain at least 1 item.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    if (request.Items.Count > 100)
    {
        return Results.Problem(
            title: "Bad Request",
            detail: $"Batch size exceeds maximum allowed (100). Received {request.Items.Count} items.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var response = await ingestionService.ProcessAsync(request, ct);
    return Results.Ok(response);
});

app.MapPost("/api/account/register", async (
    RegisterAccountRequest request,
    IAccountService accountService,
    IOptions<AppOptions> appOptions,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    SetNoStore(httpContext);

    var internalAuth = EnsureInternalApiKey(httpContext, appOptions.Value);
    if (internalAuth is not null)
    {
        return internalAuth;
    }

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return ValidationProblem("email", "Email is required.");
    }

    if (string.IsNullOrWhiteSpace(request.Password))
    {
        return ValidationProblem("password", "Password is required.");
    }

    var result = await accountService.RegisterLocalAsync(
        request.Email,
        request.DisplayName ?? string.Empty,
        request.Password,
        ct);

    return result.Status switch
    {
        RegisterLocalAccountStatus.Created => Results.Created($"/api/account/users/{result.UserId}", new { userId = result.UserId }),
        RegisterLocalAccountStatus.EmailAlreadyInUse => Results.Conflict(new { message = "Email already in use." }),
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
    };
});

app.MapPost("/api/account/verify-email", async (
    VerifyEmailRequest request,
    IAccountService accountService,
    IOptions<AppOptions> appOptions,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    SetNoStore(httpContext);

    var internalAuth = EnsureInternalApiKey(httpContext, appOptions.Value);
    if (internalAuth is not null)
    {
        return internalAuth;
    }

    if (string.IsNullOrWhiteSpace(request.Token))
    {
        return ValidationProblem("token", "Token is required.");
    }

    var result = await accountService.VerifyEmailAsync(request.Token, ct);
    if (!result.IsSuccess)
    {
        return Results.BadRequest(new { message = "Invalid or expired verification token." });
    }

    return Results.Ok(new { userId = result.UserId, verified = true });
});

app.MapPost("/api/account/resend-verification", async (
    ResendVerificationRequest request,
    IAccountService accountService,
    IOptions<AppOptions> appOptions,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    SetNoStore(httpContext);

    var internalAuth = EnsureInternalApiKey(httpContext, appOptions.Value);
    if (internalAuth is not null)
    {
        return internalAuth;
    }

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return ValidationProblem("email", "Email is required.");
    }

    var result = await accountService.ResendEmailVerificationAsync(request.Email, ct);

    return result.Status switch
    {
        ResendEmailVerificationStatus.Sent => Results.Ok(new { sent = true }),
        ResendEmailVerificationStatus.AlreadyVerified => Results.Ok(new { sent = false, alreadyVerified = true }),
        ResendEmailVerificationStatus.AccountNotFound => Results.NotFound(new { message = "Account not found." }),
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
    };
});

app.MapPost("/api/account/password/forgot", async (
    ForgotPasswordRequest request,
    IAccountService accountService,
    IOptions<AppOptions> appOptions,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    SetNoStore(httpContext);

    var internalAuth = EnsureInternalApiKey(httpContext, appOptions.Value);
    if (internalAuth is not null)
    {
        return internalAuth;
    }

    await accountService.RequestPasswordResetAsync(request.Email ?? string.Empty, ct);

    return Results.Ok(new { message = "If the account exists, reset instructions were sent." });
});

app.MapPost("/api/account/password/reset", async (
    ResetPasswordRequest request,
    IAccountService accountService,
    IOptions<AppOptions> appOptions,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    SetNoStore(httpContext);

    var internalAuth = EnsureInternalApiKey(httpContext, appOptions.Value);
    if (internalAuth is not null)
    {
        return internalAuth;
    }

    if (string.IsNullOrWhiteSpace(request.Token))
    {
        return ValidationProblem("token", "Token is required.");
    }

    if (string.IsNullOrWhiteSpace(request.NewPassword))
    {
        return ValidationProblem("newPassword", "New password is required.");
    }

    var result = await accountService.ResetPasswordAsync(request.Token, request.NewPassword, ct);

    return result.Status switch
    {
        PasswordResetStatus.Reset => Results.Ok(new { reset = true, userId = result.UserId }),
        PasswordResetStatus.InvalidOrExpiredToken => Results.BadRequest(new { message = "Invalid or expired reset token." }),
        PasswordResetStatus.AccountWithoutPassword => Results.Conflict(new { message = "Account does not have local credentials." }),
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
    };
});

app.MapPost("/api/account/auth/credentials", async (
    CredentialsAuthRequest request,
    IAccountService accountService,
    JobsDbContext db,
    IOptions<AppOptions> appOptions,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    SetNoStore(httpContext);

    var internalAuth = EnsureInternalApiKey(httpContext, appOptions.Value);
    if (internalAuth is not null)
    {
        return internalAuth;
    }

    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return ValidationProblem("credentials", "Email and password are required.");
    }

    var authResult = await accountService.AuthenticateWithPasswordAsync(request.Email, request.Password, ct);

    if (authResult.Status == PasswordAuthenticationStatus.EmailNotVerified)
    {
        return Results.Json(
            new { message = "Email is not verified." },
            statusCode: StatusCodes.Status403Forbidden);
    }

    if (!authResult.IsSuccess)
    {
        return Results.Unauthorized();
    }

    var user = await db.Users
        .AsNoTracking()
        .FirstAsync(x => x.Id == authResult.UserId, ct);

    return Results.Ok(new
    {
        userId = user.Id,
        email = user.Email,
        displayName = user.DisplayName,
        avatarUrl = user.AvatarUrl
    });
});

app.MapPost("/api/account/auth/oauth", async (
    OAuthAuthRequest request,
    IAccountService accountService,
    JobsDbContext db,
    IOptions<AppOptions> appOptions,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    SetNoStore(httpContext);

    var internalAuth = EnsureInternalApiKey(httpContext, appOptions.Value);
    if (internalAuth is not null)
    {
        return internalAuth;
    }

    if (string.IsNullOrWhiteSpace(request.Provider))
    {
        return ValidationProblem("provider", "Provider is required.");
    }

    if (string.IsNullOrWhiteSpace(request.ProviderUserId))
    {
        return ValidationProblem("providerUserId", "Provider user ID is required.");
    }

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return ValidationProblem("email", "Email is required.");
    }

    var result = await accountService.ResolveOAuthSignInAsync(
        request.Provider,
        request.ProviderUserId,
        request.Email,
        request.IsEmailVerified,
        request.DisplayName,
        request.AvatarUrl,
        ct);

    if (result.Status == OAuthSignInStatus.EmailNotVerified)
    {
        return Results.Json(
            new { message = "Provider email is not verified." },
            statusCode: StatusCodes.Status403Forbidden);
    }

    if (!result.IsSuccess)
    {
        return Results.BadRequest(new { message = "Invalid OAuth payload." });
    }

    var user = await db.Users
        .AsNoTracking()
        .FirstAsync(x => x.Id == result.UserId, ct);

    return Results.Ok(new
    {
        userId = user.Id,
        email = user.Email,
        displayName = user.DisplayName,
        avatarUrl = user.AvatarUrl
    });
});

app.MapGet("/api/me/favorites", async (
    IFavoritesService favoritesService,
    IOptions<AppOptions> appOptions,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    SetNoStore(httpContext);

    var internalAuth = EnsureInternalApiKey(httpContext, appOptions.Value);
    if (internalAuth is not null)
    {
        return internalAuth;
    }

    if (!TryGetUserId(httpContext, out var userId, out var userIdError))
    {
        return Results.BadRequest(new { message = userIdError });
    }

    var favorites = await favoritesService.ListFavoritesAsync(userId, ct);
    return Results.Ok(new
    {
        total = favorites.Count,
        items = favorites.Select(job => new
        {
            id = job.Id,
            title = job.Title,
            company = new { name = job.CompanyName },
            locationText = job.LocationText,
            workMode = job.WorkMode.ToString(),
            seniority = job.Seniority.ToString(),
            employmentType = job.EmploymentType.ToString(),
            tags = job.Tags,
            postedAt = job.PostedAt,
            capturedAt = job.CapturedAt,
            source = new { name = job.SourceName, url = job.SourceUrl }
        })
    });
});

app.MapPut("/api/me/favorites/{jobId:guid}", async (
    Guid jobId,
    IFavoritesService favoritesService,
    IOptions<AppOptions> appOptions,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    SetNoStore(httpContext);

    var internalAuth = EnsureInternalApiKey(httpContext, appOptions.Value);
    if (internalAuth is not null)
    {
        return internalAuth;
    }

    if (!TryGetUserId(httpContext, out var userId, out var userIdError))
    {
        return Results.BadRequest(new { message = userIdError });
    }

    var result = await favoritesService.AddFavoriteAsync(userId, jobId, ct);

    return result.Status switch
    {
        AddFavoriteStatus.Added => Results.Ok(new { status = "added" }),
        AddFavoriteStatus.AlreadyExists => Results.Ok(new { status = "already_exists" }),
        AddFavoriteStatus.UserNotFound => Results.NotFound(new { message = "User not found." }),
        AddFavoriteStatus.JobNotFound => Results.NotFound(new { message = "Job not found." }),
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
    };
});

app.MapDelete("/api/me/favorites/{jobId:guid}", async (
    Guid jobId,
    IFavoritesService favoritesService,
    IOptions<AppOptions> appOptions,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    SetNoStore(httpContext);

    var internalAuth = EnsureInternalApiKey(httpContext, appOptions.Value);
    if (internalAuth is not null)
    {
        return internalAuth;
    }

    if (!TryGetUserId(httpContext, out var userId, out var userIdError))
    {
        return Results.BadRequest(new { message = userIdError });
    }

    var result = await favoritesService.RemoveFavoriteAsync(userId, jobId, ct);

    return result.Status switch
    {
        RemoveFavoriteStatus.Removed => Results.Ok(new { status = "removed" }),
        RemoveFavoriteStatus.NotFound => Results.Ok(new { status = "not_found" }),
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
    };
});

app.Run();

static string[] ResolveSort(string? sort)
{
    if (string.IsNullOrWhiteSpace(sort))
    {
        return new[] { "postedAt:desc", "capturedAt:desc" };
    }

    return sort.Trim().ToLowerInvariant() switch
    {
        "postedat:asc" => new[] { "postedAt:asc", "capturedAt:desc" },
        "capturedat:asc" => new[] { "capturedAt:asc" },
        "capturedat:desc" => new[] { "capturedAt:desc" },
        _ => new[] { "postedAt:desc", "capturedAt:desc" }
    };
}

static bool TryParseEnum<TEnum>(string? raw, out TEnum value)
    where TEnum : struct, Enum
{
    if (!string.IsNullOrWhiteSpace(raw) && Enum.TryParse<TEnum>(raw, true, out var parsed))
    {
        value = parsed;
        return true;
    }

    value = default;
    return false;
}

static object TryParseJson(string rawJson)
{
    try
    {
        return JsonSerializer.Deserialize<JsonElement>(rawJson);
    }
    catch
    {
        return new { };
    }
}

static string EscapeFilterValue(string value)
{
    return value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);
}

static void SetNoStore(HttpContext httpContext)
{
    httpContext.Response.Headers.CacheControl = "no-store";
    httpContext.Response.Headers.Pragma = "no-cache";
}

static IResult? EnsureInternalApiKey(HttpContext httpContext, AppOptions appOptions)
{
    var expectedKey = appOptions.Auth.BffInternalApiKey;
    if (string.IsNullOrWhiteSpace(expectedKey))
    {
        return null;
    }

    var providedKey = httpContext.Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
    if (string.Equals(expectedKey, providedKey, StringComparison.Ordinal))
    {
        return null;
    }

    return Results.Problem(
        title: "Unauthorized",
        detail: "X-Internal-Api-Key header is missing or invalid.",
        statusCode: StatusCodes.Status401Unauthorized);
}

static bool TryGetUserId(HttpContext httpContext, out Guid userId, out string error)
{
    var raw = httpContext.Request.Headers["X-User-Id"].FirstOrDefault();
    if (Guid.TryParse(raw, out userId))
    {
        error = string.Empty;
        return true;
    }

    error = "X-User-Id header is missing or invalid.";
    return false;
}

static IResult ValidationProblem(string field, string message)
{
    return Results.ValidationProblem(new Dictionary<string, string[]>
    {
        [field] = new[] { message }
    });
}

public sealed record RegisterAccountRequest(string Email, string? DisplayName, string Password);
public sealed record VerifyEmailRequest(string Token);
public sealed record ResendVerificationRequest(string Email);
public sealed record ForgotPasswordRequest(string? Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record CredentialsAuthRequest(string Email, string Password);
public sealed record OAuthAuthRequest(
    string Provider,
    string ProviderUserId,
    string Email,
    bool IsEmailVerified,
    string? DisplayName,
    string? AvatarUrl);

public partial class Program { }
