using Microsoft.Extensions.Options;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Security;

public sealed class InputValidator(IOptions<SecurityOptions> options, ILogger<InputValidator> logger)
{
    private readonly SecurityOptions _options = options.Value;

    public (bool IsValid, string? Error, ChatRequest? SanitizedRequest) ValidateAndSanitize(ChatRequest request)
    {
        if (request.Messages is not { Count: > 0 })
            return (false, "At least one message is required.", null);

        // S4: Input length limits — max messages
        if (request.Messages.Count > _options.MaxMessages)
            return (false, $"Too many messages. Maximum is {_options.MaxMessages}.", null);

        int totalLength = 0;
        foreach (ChatMessageDto message in request.Messages)
        {
            int length = message.Content?.Length ?? 0;

            // S4: Max chars per message
            if (length > _options.MaxMessageLength)
                return (false, $"Message exceeds maximum length of {_options.MaxMessageLength} characters.", null);

            // S6: Reject messages containing HTML/script injection attempts
            if (message.Content is not null && ContainsHtmlInjection(message.Content))
            {
                logger.LogWarning("Blocked HTML/script injection attempt");
                return (false, "Message contains disallowed content.", null);
            }

            totalLength += length;
        }

        // S4: Max total chars
        if (totalLength > _options.MaxTotalLength)
            return (false, $"Total message content exceeds maximum of {_options.MaxTotalLength} characters.", null);

        // S3: Prompt injection detection
        foreach (ChatMessageDto message in request.Messages)
        {
            if (message.Content is null) continue;

            string lowerContent = message.Content.ToLowerInvariant();
            foreach (string pattern in _options.BlockedPatterns)
            {
                if (lowerContent.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Blocked prompt injection pattern detected: {Pattern}", pattern);
                    return (false, "Message contains disallowed content.", null);
                }
            }
        }

        // S5: Tool schema validation — validate ClientTools against allowlist
        if (request.ClientTools is { Count: > 0 })
        {
            foreach (ToolDefinitionDto tool in request.ClientTools)
            {
                if (!_options.AllowedToolNames.Contains(tool.Name))
                {
                    logger.LogWarning("Rejected unknown client tool: {ToolName}", tool.Name);
                    return (false, $"Tool '{tool.Name}' is not in the allowlist.", null);
                }
            }
        }

        // S1: Role stripping — remove system/assistant messages from user-authored content
        // Allow tool/assistant roles only as part of multi-turn tool continuation (not first message)
        List<ChatMessageDto> sanitizedMessages = [];
        for (int i = 0; i < request.Messages.Count; i++)
        {
            ChatMessageDto message = request.Messages[i];
            string role = message.Role.ToLowerInvariant();

            if (role is "system")
            {
                logger.LogWarning("Stripped system role message from user request");
                continue;
            }

            // Allow assistant/tool roles only after the first message (multi-turn continuation)
            if (role is "assistant" or "tool" && i == 0)
            {
                logger.LogWarning("Stripped {Role} role from first message position", role);
                continue;
            }

            // Force any other non-standard role to user
            if (role is not ("user" or "assistant" or "tool"))
            {
                sanitizedMessages.Add(message with { Role = "user" });
                continue;
            }

            sanitizedMessages.Add(message);
        }

        if (sanitizedMessages.Count == 0)
            return (false, "No valid messages after sanitization.", null);

        ChatRequest sanitizedRequest = request with { Messages = sanitizedMessages };
        return (true, null, sanitizedRequest);
    }

    private static bool ContainsHtmlInjection(string content)
    {
        var lower = content.AsSpan();
        return lower.Contains("<script", StringComparison.OrdinalIgnoreCase)
            || lower.Contains("<iframe", StringComparison.OrdinalIgnoreCase)
            || lower.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
            || lower.Contains("onerror=", StringComparison.OrdinalIgnoreCase)
            || lower.Contains("onload=", StringComparison.OrdinalIgnoreCase);
    }
}
