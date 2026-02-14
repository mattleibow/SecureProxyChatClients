using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using SecureProxyChatClients.Server.Security;
using SecureProxyChatClients.Server.Services;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Endpoints;

public static class ChatEndpoints
{
    public static RouteGroupBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/chat")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = $"{IdentityConstants.BearerScheme},{IdentityConstants.ApplicationScheme}"
            })
            .RequireRateLimiting("chat");

        group.MapPost("/", HandleChatAsync);
        group.MapPost("/stream", HandleChatStreamAsync);

        return group;
    }

    private static async Task<IResult> HandleChatAsync(
        [FromBody] ChatRequest request,
        IChatClient chatClient,
        InputValidator inputValidator,
        ContentFilter contentFilter,
        SystemPromptService systemPromptService,
        CancellationToken cancellationToken)
    {
        // Validate and sanitize input
        (bool isValid, string? error, ChatRequest? sanitizedRequest) = inputValidator.ValidateAndSanitize(request);
        if (!isValid)
            return Results.BadRequest(new { error });

        // Prepend system prompt
        IReadOnlyList<ChatMessageDto> messagesWithSystem =
            systemPromptService.PrependSystemPrompt(sanitizedRequest!.Messages);

        // Convert DTOs to MEAI ChatMessages
        List<ChatMessage> chatMessages = ConvertToChatMessages(messagesWithSystem);

        // Forward to IChatClient
        Microsoft.Extensions.AI.ChatResponse aiResponse =
            await chatClient.GetResponseAsync(chatMessages, cancellationToken: cancellationToken);

        // Convert response
        Shared.Contracts.ChatResponse response = ConvertToContractResponse(aiResponse);

        // Filter response
        response = contentFilter.FilterResponse(response);

        return Results.Ok(response);
    }

    private static async Task HandleChatStreamAsync(
        HttpContext httpContext,
        [FromBody] ChatRequest request,
        IChatClient chatClient,
        InputValidator inputValidator,
        SystemPromptService systemPromptService,
        CancellationToken cancellationToken)
    {
        // Validate and sanitize input
        (bool isValid, string? error, ChatRequest? sanitizedRequest) = inputValidator.ValidateAndSanitize(request);
        if (!isValid)
        {
            httpContext.Response.StatusCode = 400;
            await httpContext.Response.WriteAsJsonAsync(new { error }, cancellationToken);
            return;
        }

        // Prepend system prompt
        IReadOnlyList<ChatMessageDto> messagesWithSystem =
            systemPromptService.PrependSystemPrompt(sanitizedRequest!.Messages);

        // Convert DTOs to MEAI ChatMessages
        List<ChatMessage> chatMessages = ConvertToChatMessages(messagesWithSystem);

        // Set SSE headers
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        // Stream from IChatClient
        await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(
            chatMessages, cancellationToken: cancellationToken))
        {
            if (update.Text is { Length: > 0 } text)
            {
                string data = JsonSerializer.Serialize(new { text });
                await httpContext.Response.WriteAsync($"event: text-delta\ndata: {data}\n\n", cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);
            }
        }

        // Send done event
        await httpContext.Response.WriteAsync("event: done\ndata: {}\n\n", cancellationToken);
        await httpContext.Response.Body.FlushAsync(cancellationToken);
    }

    private static List<ChatMessage> ConvertToChatMessages(IReadOnlyList<ChatMessageDto> dtos)
    {
        List<ChatMessage> messages = [];
        foreach (ChatMessageDto dto in dtos)
        {
            ChatRole role = dto.Role.ToLowerInvariant() switch
            {
                "system" => ChatRole.System,
                "assistant" => ChatRole.Assistant,
                "tool" => ChatRole.Tool,
                _ => ChatRole.User,
            };
            messages.Add(new ChatMessage(role, dto.Content));
        }
        return messages;
    }

    private static Shared.Contracts.ChatResponse ConvertToContractResponse(
        Microsoft.Extensions.AI.ChatResponse aiResponse)
    {
        List<ChatMessageDto> messages = [];
        foreach (ChatMessage msg in aiResponse.Messages)
        {
            messages.Add(new ChatMessageDto
            {
                Role = msg.Role.Value,
                Content = msg.Text,
            });
        }

        return new Shared.Contracts.ChatResponse { Messages = messages };
    }
}
