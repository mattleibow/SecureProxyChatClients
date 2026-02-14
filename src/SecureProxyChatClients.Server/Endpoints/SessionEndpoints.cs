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

        group.MapPost("/", CreateSessionAsync);
        group.MapGet("/", ListSessionsAsync);
        group.MapGet("/{id}/history", GetSessionHistoryAsync);

        return group;
    }

    private static async Task<IResult> CreateSessionAsync(
        HttpContext httpContext,
        IConversationStore store,
        CancellationToken ct)
    {
        string userId = GetUserId(httpContext);
        string sessionId = await store.CreateSessionAsync(userId, ct);
        return Results.Ok(new { sessionId });
    }

    private static async Task<IResult> ListSessionsAsync(
        HttpContext httpContext,
        IConversationStore store,
        CancellationToken ct)
    {
        string userId = GetUserId(httpContext);
        IReadOnlyList<SessionSummary> sessions = await store.GetUserSessionsAsync(userId, ct);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> GetSessionHistoryAsync(
        string id,
        HttpContext httpContext,
        IConversationStore store,
        CancellationToken ct)
    {
        string userId = GetUserId(httpContext);

        string? owner = await store.GetSessionOwnerAsync(id, ct);
        if (owner is null)
            return Results.NotFound();
        if (owner != userId)
            return Results.Forbid();

        IReadOnlyList<ChatMessageDto> history = await store.GetHistoryAsync(id, ct);
        return Results.Ok(history);
    }

    private static string GetUserId(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found in claims.");
}
