using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using SecureProxyChatClients.Server.Data;
using SecureProxyChatClients.Server.Security;
using SecureProxyChatClients.Server.Services;
using SecureProxyChatClients.Server.Tools;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Endpoints;

public static class ChatEndpoints
{
    private const int MaxToolCallRounds = 5;

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
        HttpContext httpContext,
        [FromBody] ChatRequest request,
        IChatClient chatClient,
        InputValidator inputValidator,
        ContentFilter contentFilter,
        SystemPromptService systemPromptService,
        ServerToolRegistry serverToolRegistry,
        IConversationStore conversationStore,
        CancellationToken cancellationToken)
    {
        // Validate and sanitize input
        (bool isValid, string? error, ChatRequest? sanitizedRequest) = inputValidator.ValidateAndSanitize(request);
        if (!isValid)
            return Results.BadRequest(new { error });

        string userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Session management: use provided or auto-create
        string sessionId;
        if (sanitizedRequest!.SessionId is { Length: > 0 } sid)
        {
            string? owner = await conversationStore.GetSessionOwnerAsync(sid, cancellationToken);
            if (owner is null)
                return Results.NotFound(new { error = "Session not found." });
            if (owner != userId)
                return Results.Forbid();
            sessionId = sid;
        }
        else
        {
            sessionId = await conversationStore.CreateSessionAsync(userId, cancellationToken);
        }

        // Persist user messages
        var userMessages = sanitizedRequest.Messages
            .Where(m => m.Role == "user")
            .ToList();
        if (userMessages.Count > 0)
            await conversationStore.AppendMessagesAsync(sessionId, userMessages, cancellationToken);

        // Prepend system prompt
        IReadOnlyList<ChatMessageDto> messagesWithSystem =
            systemPromptService.PrependSystemPrompt(sanitizedRequest!.Messages);

        // Convert DTOs to MEAI ChatMessages
        List<ChatMessage> chatMessages = ConvertToChatMessages(messagesWithSystem);

        // Build chat options with server tools and response format
        ChatOptions chatOptions = new()
        {
            Tools = [.. serverToolRegistry.Tools],
            ResponseFormat = ConvertResponseFormat(sanitizedRequest.Options?.ResponseFormat),
        };

        // Tool execution loop
        for (int round = 0; round < MaxToolCallRounds; round++)
        {
            Microsoft.Extensions.AI.ChatResponse aiResponse =
                await chatClient.GetResponseAsync(chatMessages, chatOptions, cancellationToken);

            // Check for function call content in the response
            List<FunctionCallContent> functionCalls = aiResponse.Messages
                .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .ToList();

            if (functionCalls.Count == 0)
            {
                // No tool calls — return the final response
                Shared.Contracts.ChatResponse response = ConvertToContractResponse(aiResponse, sessionId);
                response = contentFilter.FilterResponse(response);

                // Persist assistant messages
                var assistantMessages = response.Messages.Where(m => m.Role == "assistant").ToList();
                if (assistantMessages.Count > 0)
                    await conversationStore.AppendMessagesAsync(sessionId, assistantMessages, cancellationToken);

                return Results.Ok(response);
            }

            // Add assistant message with tool calls to conversation
            chatMessages.AddRange(aiResponse.Messages);

            // Process each function call
            List<FunctionCallContent> clientToolCalls = [];
            foreach (FunctionCallContent functionCall in functionCalls)
            {
                AIFunction? serverTool = serverToolRegistry.GetTool(functionCall.Name);
                if (serverTool is not null)
                {
                    // Execute server tool and add result to messages
                    AIFunctionArguments? args = functionCall.Arguments is { Count: > 0 }
                        ? new AIFunctionArguments(functionCall.Arguments!)
                        : null;
                    object? result = await serverTool.InvokeAsync(args, cancellationToken);
                    string resultJson = JsonSerializer.Serialize(result);
                    chatMessages.Add(new ChatMessage(ChatRole.Tool,
                    [
                        new FunctionResultContent(functionCall.CallId, resultJson),
                    ]));
                }
                else
                {
                    // Unknown tool — treat as client tool, return to caller
                    clientToolCalls.Add(functionCall);
                }
            }

            // If there are client tool calls, return them to the client
            if (clientToolCalls.Count > 0)
            {
                Shared.Contracts.ChatResponse response = ConvertToContractResponseWithToolCalls(
                    aiResponse, clientToolCalls, sessionId);
                response = contentFilter.FilterResponse(response);
                return Results.Ok(response);
            }

            // All tool calls were server tools — loop to continue the conversation
        }

        // Max rounds exceeded — return whatever we have
        return Results.Ok(new Shared.Contracts.ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "Tool processing limit reached." }],
            SessionId = sessionId,
        });
    }

    private static async Task HandleChatStreamAsync(
        HttpContext httpContext,
        [FromBody] ChatRequest request,
        IChatClient chatClient,
        InputValidator inputValidator,
        SystemPromptService systemPromptService,
        IConversationStore conversationStore,
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

        string userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Session management
        string sessionId;
        if (sanitizedRequest!.SessionId is { Length: > 0 } sid)
        {
            string? owner = await conversationStore.GetSessionOwnerAsync(sid, cancellationToken);
            if (owner is null || owner != userId)
            {
                httpContext.Response.StatusCode = owner is null ? 404 : 403;
                await httpContext.Response.WriteAsJsonAsync(new { error = "Session access denied." }, cancellationToken);
                return;
            }
            sessionId = sid;
        }
        else
        {
            sessionId = await conversationStore.CreateSessionAsync(userId, cancellationToken);
        }

        // Persist user messages
        var userMessages = sanitizedRequest.Messages.Where(m => m.Role == "user").ToList();
        if (userMessages.Count > 0)
            await conversationStore.AppendMessagesAsync(sessionId, userMessages, cancellationToken);

        // Prepend system prompt
        IReadOnlyList<ChatMessageDto> messagesWithSystem =
            systemPromptService.PrependSystemPrompt(sanitizedRequest!.Messages);

        // Convert DTOs to MEAI ChatMessages
        List<ChatMessage> chatMessages = ConvertToChatMessages(messagesWithSystem);

        // Set SSE headers
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        // Stream from IChatClient and collect full response for persistence
        var fullText = new System.Text.StringBuilder();
        await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(
            chatMessages, cancellationToken: cancellationToken))
        {
            if (update.Text is { Length: > 0 } text)
            {
                fullText.Append(text);
                string data = JsonSerializer.Serialize(new { text });
                await httpContext.Response.WriteAsync($"event: text-delta\ndata: {data}\n\n", cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);
            }
        }

        // Persist assistant response
        if (fullText.Length > 0)
        {
            await conversationStore.AppendMessagesAsync(sessionId,
                [new ChatMessageDto { Role = "assistant", Content = fullText.ToString() }], cancellationToken);
        }

        // Send done event with sessionId
        string doneData = JsonSerializer.Serialize(new { sessionId });
        await httpContext.Response.WriteAsync($"event: done\ndata: {doneData}\n\n", cancellationToken);
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

            // Reconstruct assistant messages with tool calls
            if (role == ChatRole.Assistant && dto.ToolCalls is { Count: > 0 })
            {
                List<AIContent> contents = [];
                if (dto.Content is { Length: > 0 })
                    contents.Add(new TextContent(dto.Content));
                foreach (var tc in dto.ToolCalls)
                {
                    Dictionary<string, object?>? args = tc.Arguments is { } a
                        ? JsonSerializer.Deserialize<Dictionary<string, object?>>(a.GetRawText())
                        : null;
                    contents.Add(new FunctionCallContent(tc.CallId, tc.Name, args));
                }
                messages.Add(new ChatMessage(ChatRole.Assistant, contents));
                continue;
            }

            // Reconstruct tool result messages
            if (role == ChatRole.Tool && dto.ToolCallId is not null)
            {
                messages.Add(new ChatMessage(ChatRole.Tool,
                [
                    new FunctionResultContent(dto.ToolCallId, dto.Content ?? ""),
                ]));
                continue;
            }

            messages.Add(new ChatMessage(role, dto.Content));
        }
        return messages;
    }

    private static Shared.Contracts.ChatResponse ConvertToContractResponse(
        Microsoft.Extensions.AI.ChatResponse aiResponse,
        string? sessionId = null)
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

        return new Shared.Contracts.ChatResponse { Messages = messages, SessionId = sessionId };
    }

    private static Shared.Contracts.ChatResponse ConvertToContractResponseWithToolCalls(
        Microsoft.Extensions.AI.ChatResponse aiResponse,
        List<FunctionCallContent> clientToolCalls,
        string? sessionId = null)
    {
        List<ChatMessageDto> messages = [];

        // Build an assistant message with text + tool calls combined
        List<ToolCallDto> toolCallDtos = clientToolCalls.Select(tc => new ToolCallDto
        {
            CallId = tc.CallId,
            Name = tc.Name,
            Arguments = tc.Arguments is not null
                ? JsonSerializer.SerializeToElement(tc.Arguments)
                : null,
        }).ToList();

        string? textContent = string.Join("",
            aiResponse.Messages.Where(m => m.Text is { Length: > 0 }).Select(m => m.Text));

        messages.Add(new ChatMessageDto
        {
            Role = "assistant",
            Content = textContent is { Length: > 0 } ? textContent : null,
            ToolCalls = toolCallDtos,
        });

        return new Shared.Contracts.ChatResponse { Messages = messages, SessionId = sessionId };
    }

    private static ChatResponseFormat? ConvertResponseFormat(JsonElement? responseFormat)
    {
        if (responseFormat is null || responseFormat.Value.ValueKind == JsonValueKind.Undefined)
            return null;

        if (responseFormat.Value.ValueKind == JsonValueKind.String)
        {
            string formatStr = responseFormat.Value.GetString()!;
            return formatStr.Equals("json", StringComparison.OrdinalIgnoreCase)
                ? ChatResponseFormat.Json
                : ChatResponseFormat.Text;
        }

        // Object with schema — treat as JSON schema response format
        if (responseFormat.Value.ValueKind == JsonValueKind.Object)
        {
            return ChatResponseFormat.ForJsonSchema(responseFormat.Value);
        }

        return null;
    }
}
