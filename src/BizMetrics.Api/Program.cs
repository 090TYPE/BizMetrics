using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using BizMetrics.Api.Auth;
using BizMetrics.Api.Demo;
using BizMetrics.Api.Health;
using BizMetrics.Infrastructure.Analytics;
using BizMetrics.Infrastructure.Audit;
using BizMetrics.Infrastructure.Billing;
using BizMetrics.Infrastructure.Email;
using BizMetrics.Infrastructure.Persistence;
using BizMetrics.Infrastructure.Processing;
using BizMetrics.Infrastructure.Storage;
using BizMetrics.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// ── Sentry (error tracking) ────────────────────────────────────────────────
// Enabled only when SENTRY_DSN / Sentry__Dsn is set in the environment.
// Wrap before anything else so exceptions during startup are captured.

var builder = WebApplication.CreateBuilder(args);

// UseSentry reads Sentry:Dsn from config automatically when present.
// Providing the DSN here is enough; fine-tuning goes in appsettings.json.
var sentryDsn = builder.Configuration["Sentry:Dsn"] ?? builder.Configuration["SENTRY_DSN"];
if (!string.IsNullOrWhiteSpace(sentryDsn))
    builder.WebHost.UseSentry(sentryDsn);

// ── Options ────────────────────────────────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
          ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection(GoogleOptions.SectionName));

// ── Persistence + tenancy ──────────────────────────────────────────────────
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// ── Auth ───────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<SessionService>();

// ── Email (async via in-process queue + hosted drain) ──────────────────────
builder.Services.AddSingleton<ChannelEmailQueue>();
builder.Services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<ChannelEmailQueue>());
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
builder.Services.AddHostedService<EmailBackgroundService>();

// ── Object storage (S3/MinIO) ──────────────────────────────────────────────
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddSingleton<IObjectStorage, S3ObjectStorage>();

// ── Dataset processing (async via queue + hosted worker) ──────────────────
builder.Services.AddSingleton<ChannelDatasetProcessingQueue>();
builder.Services.AddSingleton<IDatasetProcessingQueue>(sp => sp.GetRequiredService<ChannelDatasetProcessingQueue>());
builder.Services.AddHostedService<DatasetProcessingService>();

// ── Analytics ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<AnalyticsService>();

// ── Billing (Stripe) ───────────────────────────────────────────────────────
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<PlanGuard>();

// ── Audit log ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuditService>();

// ── Health checks ──────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);

// ── Rate limiting ──────────────────────────────────────────────────────────
// auth     — 10 requests/min per IP  (brute-force protection on login/register)
// upload   — 10 requests/min per user (expensive CSV ingestion)
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = 429;

    opts.AddPolicy("auth", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    opts.AddPolicy("upload", ctx =>
    {
        var userId = ctx.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? ctx.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization(options => options.AddOrgPolicies());

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
        // Accept and emit enums as their names ("Admin") rather than integers.
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

// ── Swagger ────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BizMetrics API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

// ─────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Apply migrations and seed plans at startup (skipped under the test host).
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var stripeOpts = scope.ServiceProvider.GetRequiredService<IOptions<StripeOptions>>().Value;
    await DbInitializer.MigrateAndSeedAsync(db, stripeOpts.ProPriceId, stripeOpts.BusinessPriceId);
    await scope.ServiceProvider.GetRequiredService<IObjectStorage>().EnsureBucketAsync();

    // Auto-seed demo workspace when DEMO_SEED=true (e.g. Fly.io review apps).
    var demoSeed = app.Configuration["DEMO_SEED"];
    if (string.Equals(demoSeed, "true", StringComparison.OrdinalIgnoreCase)
        || app.Environment.IsDevelopment())
    {
        await DemoSeeder.SeedAsync(db);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

app.MapControllers();

// Liveness probe — always fast, no DB round-trip
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Readiness probe — checks DB + storage (used by orchestrators before routing traffic)
app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), description = e.Value.Description })
        };
        await ctx.Response.WriteAsJsonAsync(result);
    }
});

app.Run();

// Exposed so the integration test project can spin up the API with WebApplicationFactory.
public partial class Program;
