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

        // Add any text content from the response
        foreach (ChatMessage msg in aiResponse.Messages)
        {
            if (msg.Text is { Length: > 0 })
            {
                messages.Add(new ChatMessageDto
                {
                    Role = msg.Role.Value,
                    Content = msg.Text,
                });
            }
        }

        // Add tool call messages for the client to handle
        foreach (FunctionCallContent toolCall in clientToolCalls)
        {
            string argsJson = toolCall.Arguments is not null
                ? JsonSerializer.Serialize(toolCall.Arguments)
                : "{}";

            messages.Add(new ChatMessageDto
            {
                Role = "tool_call",
                Content = JsonSerializer.Serialize(new
                {
                    callId = toolCall.CallId,
                    name = toolCall.Name,
                    arguments = argsJson,
                }),
            });
        }

        return new Shared.Contracts.ChatResponse { Messages = messages };
    }
}
