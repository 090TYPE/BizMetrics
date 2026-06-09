using System.Text;
using BizMetrics.Api.Auth;
using BizMetrics.Infrastructure.Analytics;
using BizMetrics.Infrastructure.Billing;
using BizMetrics.Infrastructure.Email;
using BizMetrics.Infrastructure.Persistence;
using BizMetrics.Infrastructure.Processing;
using BizMetrics.Infrastructure.Storage;
using BizMetrics.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Options ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
          ?? throw new InvalidOperationException("Jwt configuration section is missing.");

// --- Persistence + tenancy ---
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// --- Auth ---
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<SessionService>();

// --- Email (async via in-process queue + hosted drain) ---
builder.Services.AddSingleton<ChannelEmailQueue>();
builder.Services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<ChannelEmailQueue>());
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
builder.Services.AddHostedService<EmailBackgroundService>();

// --- Object storage (S3/MinIO) ---
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddSingleton<IObjectStorage, S3ObjectStorage>();

// --- Dataset processing (async via queue + hosted worker) ---
builder.Services.AddSingleton<ChannelDatasetProcessingQueue>();
builder.Services.AddSingleton<IDatasetProcessingQueue>(sp => sp.GetRequiredService<ChannelDatasetProcessingQueue>());
builder.Services.AddHostedService<DatasetProcessingService>();

// --- Analytics ---
builder.Services.AddScoped<AnalyticsService>();

// --- Billing (Stripe) ---
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<PlanGuard>();

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
        // Accept and emit enums as their names ("Admin") rather than integers,
        // so role payloads match what the client sends.
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

// --- Swagger with bearer auth ---
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

var app = builder.Build();

// Apply migrations and seed plans at startup (skipped under the test host).
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var stripeOpts = scope.ServiceProvider.GetRequiredService<IOptions<StripeOptions>>().Value;
    await DbInitializer.MigrateAndSeedAsync(db, stripeOpts.ProPriceId, stripeOpts.BusinessPriceId);
    await scope.ServiceProvider.GetRequiredService<IObjectStorage>().EnsureBucketAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposed so the integration test project can spin up the API with WebApplicationFactory.
public partial class Program;
