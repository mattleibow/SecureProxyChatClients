using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using SecureProxyChatClients.Server.Security;
using SecureProxyChatClients.Server.VectorStore;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Endpoints;

public static class MemoryEndpoints
{
    public static RouteGroupBuilder MapMemoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/memory")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = $"{IdentityConstants.BearerScheme},{IdentityConstants.ApplicationScheme}"
            });

        group.MapGet("/recent", GetRecentMemoriesAsync)
            .WithName("GetRecentMemories")
            .WithSummary("Gets recent story memories for the current user");
        group.MapPost("/store", StoreMemoryAsync)
            .WithName("StoreMemory")
            .WithSummary("Stores a new story memory");

        return group;
    }

    private static async Task<IResult> GetRecentMemoriesAsync(
        HttpContext httpContext,
        IStoryMemoryService memoryService,
        CancellationToken ct)
    {
        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        var memories = await memoryService.GetRecentMemoriesAsync(userId, 20, ct);
        var result = memories.Select(m => new
        {
            m.Id,
            m.Content,
            m.MemoryType,
            m.Tags,
            m.CreatedAt,
        });

        return Results.Ok(result);
    }

    private static async Task<IResult> StoreMemoryAsync(
        HttpContext httpContext,
        StoreMemoryRequest request,
        IStoryMemoryService memoryService,
        InputValidator inputValidator,
        CancellationToken ct)
    {
        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Content))
            return Results.BadRequest(new { error = "Content is required." });

        if (request.Content.Length > 2000)
            return Results.BadRequest(new { error = "Content too long (max 2000 chars)." });

        // Validate content against injection patterns
        (bool isValid, string? error, _) = inputValidator.ValidateAndSanitize(
            new ChatRequest { Messages = [new ChatMessageDto { Role = "user", Content = request.Content }] });
            
        if (!isValid)
            return Results.BadRequest(new { error = error ?? "Invalid content." });

        await memoryService.StoreMemoryAsync(
            userId,
            request.SessionId ?? "global",
            request.Content,
            request.MemoryType ?? "event",
            ct: ct);

        return Results.Ok(new { stored = true });
    }
}

public sealed record StoreMemoryRequest
{
    public string Content { get; init; } = string.Empty;
    public string? MemoryType { get; init; }
    public string? SessionId { get; init; }
}
