using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
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
        [FromBody] ChatRequest request,
        IChatClient chatClient,
        InputValidator inputValidator,
        ContentFilter contentFilter,
        SystemPromptService systemPromptService,
        ServerToolRegistry serverToolRegistry,
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

        // Build chat options with server tools
        ChatOptions chatOptions = new()
        {
            Tools = [.. serverToolRegistry.Tools],
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
                Shared.Contracts.ChatResponse response = ConvertToContractResponse(aiResponse);
                response = contentFilter.FilterResponse(response);
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
                    aiResponse, clientToolCalls);
                response = contentFilter.FilterResponse(response);
                return Results.Ok(response);
            }

            // All tool calls were server tools — loop to continue the conversation
        }

        // Max rounds exceeded — return whatever we have
        return Results.Ok(new Shared.Contracts.ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "Tool processing limit reached." }],
        });
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

    private static Shared.Contracts.ChatResponse ConvertToContractResponseWithToolCalls(
        Microsoft.Extensions.AI.ChatResponse aiResponse,
        List<FunctionCallContent> clientToolCalls)
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

        return new Shared.Contracts.ChatResponse { Messages = messages };
    }
}
