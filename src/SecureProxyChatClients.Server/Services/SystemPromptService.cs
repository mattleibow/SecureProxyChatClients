using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Services;

public sealed class SystemPromptService(IConfiguration configuration)
{
    private const string DefaultSystemPrompt =
        "You are a helpful assistant in the LoreEngine creative writing application.";

    private readonly string _systemPrompt =
        configuration.GetValue<string>("AI:SystemPrompt") ?? DefaultSystemPrompt;

    public IReadOnlyList<ChatMessageDto> PrependSystemPrompt(IReadOnlyList<ChatMessageDto> messages)
    {
        var systemMessage = new ChatMessageDto
        {
            Role = "system",
            Content = _systemPrompt
        };

        return [systemMessage, .. messages];
    }
}
