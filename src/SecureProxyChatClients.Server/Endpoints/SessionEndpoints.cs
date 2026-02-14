using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using SecureProxyChatClients.Server.Data;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Endpoints;

public static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/sessions")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = $"{IdentityConstants.BearerScheme},{IdentityConstants.ApplicationScheme}"
            });

        group.MapPost("/", CreateSessionAsync)
            .WithName("CreateSession")
            .WithSummary("Creates a new conversation session");
        group.MapGet("/", ListSessionsAsync)
            .WithName("ListSessions")
            .WithSummary("Lists all sessions for the current user");
        group.MapGet("/{id}/history", GetSessionHistoryAsync)
            .WithName("GetSessionHistory")
            .WithSummary("Gets the message history for a session");

        return group;
    }

    private static string? TryGetUserId(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

    private static async Task<IResult> CreateSessionAsync(
        HttpContext httpContext,
        IConversationStore store,
        ILogger<IConversationStore> logger,
        CancellationToken ct)
    {
        string? userId = TryGetUserId(httpContext);
        if (userId is null)
            return Results.Unauthorized();

        string sessionId = await store.CreateSessionAsync(userId, ct);
        logger.LogInformation("Session {SessionId} created for user {UserId}", sessionId, userId);
        return Results.Ok(new { sessionId });
    }

    private static async Task<IResult> ListSessionsAsync(
        HttpContext httpContext,
        IConversationStore store,
        CancellationToken ct)
    {
        string? userId = TryGetUserId(httpContext);
        if (userId is null)
            return Results.Unauthorized();

        IReadOnlyList<SessionSummary> sessions = await store.GetUserSessionsAsync(userId, ct);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> GetSessionHistoryAsync(
        string id,
        HttpContext httpContext,
        IConversationStore store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
            return Results.BadRequest(new { error = "Invalid session ID." });

        string? userId = TryGetUserId(httpContext);
        if (userId is null)
            return Results.Unauthorized();

        string? owner = await store.GetSessionOwnerAsync(id, ct);
        if (owner is null)
            return Results.NotFound();
        if (owner != userId)
            return Results.Forbid();

        IReadOnlyList<ChatMessageDto> history = await store.GetHistoryAsync(id, ct);
        return Results.Ok(history);
    }
}
