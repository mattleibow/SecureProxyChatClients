using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecureProxyChatClients.Server.AI;
using SecureProxyChatClients.Server.Data;
using SecureProxyChatClients.Server.Endpoints;
using SecureProxyChatClients.Server.GameEngine;
using SecureProxyChatClients.Server.Security;
using SecureProxyChatClients.Server.Services;
using SecureProxyChatClients.Server.VectorStore;

var builder = WebApplication.CreateBuilder(args);

// Load local secrets file if present (gitignored) — loaded early so env vars can override
var secretsPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "secrets.json");
if (File.Exists(secretsPath))
{
    builder.Configuration.AddJsonFile(Path.GetFullPath(secretsPath), optional: true, reloadOnChange: false);
}
// Re-add env vars so they override secrets.json
builder.Configuration.AddEnvironmentVariables();

builder.AddServiceDefaults();

// Database
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));

builder.Services.AddAuthorization();

builder.Services.AddIdentityApiEndpoints<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedEmail = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>();

// Configure Identity to use Bearer token by default and disable cookies
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.BearerScheme;
    options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
});

builder.Services.AddScoped<SeedDataService>();

var clientOrigin = builder.Configuration.GetValue<string>("Client:Origin") ?? "https://localhost:5002";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(clientOrigin)
              .WithHeaders("Content-Type", "Authorization", "Accept")
              .WithMethods("GET", "POST")
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
});

builder.Services.AddOpenApi();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// AI services
builder.Services.AddAiServices(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<AiProviderHealthCheck>("ai-provider", tags: ["ready"])
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

// Security services
builder.Services.Configure<SecurityOptions>(
    builder.Configuration.GetSection(SecurityOptions.SectionName));
builder.Services.AddSingleton<InputValidator>();
builder.Services.AddSingleton<ContentFilter>();
builder.Services.AddSingleton<SystemPromptService>();

// Conversation persistence
builder.Services.AddScoped<IConversationStore, EfConversationStore>();

// Game engine
builder.Services.AddSingleton<IGameStateStore, InMemoryGameStateStore>();
builder.Services.AddSingleton<GameToolRegistry>();

// Vector store — use PostgreSQL with pgvector when available, fallback to in-memory
string? vectorConnectionString = builder.Configuration.GetConnectionString("VectorStore");
if (!string.IsNullOrEmpty(vectorConnectionString))
{
    builder.Services.AddDbContext<VectorDbContext>(o => o.UseNpgsql(vectorConnectionString, npgsqlOptions =>
        npgsqlOptions.UseVector()));
    builder.Services.AddScoped<IStoryMemoryService, PgVectorStoryMemoryService>();
}
else
{
    builder.Services.AddSingleton<IStoryMemoryService, InMemoryStoryMemoryService>();
}

// Rate limiting — per-user token bucket to prevent single-user DoS
int permitLimit = Math.Max(1, builder.Configuration.GetValue("RateLimiting:PermitLimit", 30));
int windowSeconds = Math.Max(1, builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("chat", httpContext =>
    {
        // Partition by authenticated user ID, falling back to IP address
        string partitionKey = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = permitLimit,
            ReplenishmentPeriod = TimeSpan.FromSeconds(Math.Max(1, windowSeconds / permitLimit)),
            TokensPerPeriod = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 2,
            AutoReplenishment = true,
        });
    });

    // Stricter rate limiting for auth endpoints (login/register) to prevent brute-force
    options.AddPolicy("auth", httpContext =>
    {
        string partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter($"auth-{partitionKey}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });
    });
});

// Forwarded headers — ensures correct client IP behind reverse proxies (load balancers, Azure App Service)
// IMPORTANT: In production, configure KnownProxies/KnownIPNetworks to match your infrastructure.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // ForwardLimit prevents processing an unbounded chain of proxy headers
    options.ForwardLimit = 2;
    // In development, trust loopback proxies only. In production, add your specific proxy IPs/networks.
    // Do NOT clear KnownNetworks/KnownProxies — the defaults (loopback) are the safest baseline.
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1_048_576; // 1 MB
});

var app = builder.Build();

// Ensure database is created and seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Initialize vector DB if available
    var vectorDb = scope.ServiceProvider.GetService<VectorDbContext>();
    if (vectorDb is not null)
    {
        await vectorDb.Database.EnsureCreatedAsync();
    }

    // Only seed test data in Development or when explicitly configured
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue("SeedUser:Enabled", false))
    {
        var seeder = scope.ServiceProvider.GetRequiredService<SeedDataService>();
        await seeder.SeedAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

// ForwardedHeaders must run first to resolve correct client IP/proto for all downstream middleware
app.UseForwardedHeaders();

// Security headers middleware
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    headers["Cross-Origin-Embedder-Policy"] = "credentialless";
    headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'";
    await next();
});

// Security audit logging middleware
app.Use(async (context, next) =>
{
    await next();

    if (context.Response.StatusCode == StatusCodes.Status401Unauthorized ||
        context.Response.StatusCode == StatusCodes.Status403Forbidden)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var user = context.User.Identity?.Name ?? "anonymous";
        logger.LogWarning("Security Audit: Access denied ({StatusCode}) for user {User} at {Path}",
            context.Response.StatusCode, user, context.Request.Path);
    }
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
// Rate limiter runs after authentication so per-user partitioning can read bearer claims
app.UseRateLimiter();

app.MapIdentityApi<IdentityUser>()
    .RequireRateLimiting("auth");
app.MapChatEndpoints();
app.MapSessionEndpoints();
app.MapPlayEndpoints();
app.MapMemoryEndpoints();

app.MapGet("/api/ping", (HttpContext context) =>
{
    return Results.Ok(new
    {
        user = context.User.Identity?.Name,
        authenticated = true
    });
}).RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = IdentityConstants.BearerScheme })
  .RequireRateLimiting("chat")
  .WithName("Ping")
  .WithSummary("Health check for authenticated users")
  .Produces(StatusCodes.Status200OK)
  .Produces(StatusCodes.Status401Unauthorized);

app.MapDefaultEndpoints();

app.Run();

// Test host hook — allows WebApplicationFactory<Program> to reference this type
public partial class Program { }
