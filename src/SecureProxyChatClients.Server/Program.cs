using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecureProxyChatClients.Server.AI;
using SecureProxyChatClients.Server.Data;
using SecureProxyChatClients.Server.Endpoints;
using SecureProxyChatClients.Server.GameEngine;
using SecureProxyChatClients.Server.Security;
using SecureProxyChatClients.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));

builder.Services.AddAuthorization();

builder.Services.AddIdentityApiEndpoints<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddScoped<SeedDataService>();

var clientOrigin = builder.Configuration.GetValue<string>("Client:Origin") ?? "https://localhost:5002";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(clientOrigin)
              .WithHeaders("Content-Type", "Authorization", "Accept")
              .WithMethods("GET", "POST", "OPTIONS"));
});

builder.Services.AddOpenApi();

// AI services
builder.Services.AddAiServices(builder.Configuration);

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

// Rate limiting
int permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 30);
int windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("chat", limiterOptions =>
    {
        limiterOptions.PermitLimit = permitLimit;
        limiterOptions.Window = TimeSpan.FromSeconds(windowSeconds);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
});

var app = builder.Build();

// Ensure database is created and seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<SeedDataService>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapIdentityApi<IdentityUser>();
app.MapChatEndpoints();
app.MapSessionEndpoints();
app.MapPlayEndpoints();

app.MapGet("/api/ping", (HttpContext context) =>
{
    return Results.Ok(new
    {
        user = context.User.Identity?.Name,
        authenticated = true
    });
}).RequireAuthorization();

app.MapDefaultEndpoints();

app.Run();
