using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Server.AI;

public static class AiServiceExtensions
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        string provider = configuration.GetValue<string>("AI:Provider") ?? "Fake";

        switch (provider.ToLowerInvariant())
        {
            case "fake":
                var fakeChatClient = new FakeChatClient();
                fakeChatClient.Responses.Enqueue(
                    new ChatResponse(new ChatMessage(ChatRole.Assistant, "This is a fake response.")));
                services.AddSingleton<IChatClient>(fakeChatClient);
                break;

            case "copilotcli":
                string model = configuration.GetValue<string>("AI:CopilotCli:Model") ?? "gpt-5-mini";
                services.AddSingleton<IChatClient>(new CopilotCliChatClient(model));
                break;

            case "azureopenai":
                string endpoint = configuration["AI:Endpoint"]
                    ?? throw new InvalidOperationException("AI:Endpoint is required for AzureOpenAI provider.");
                string apiKey = configuration["AI:ApiKey"]
                    ?? throw new InvalidOperationException("AI:ApiKey is required for AzureOpenAI provider.");
                string deploymentName = configuration["AI:DeploymentName"] ?? "gpt-4o";

                var azureClient = new AzureOpenAIClient(
                    new Uri(endpoint),
                    new System.ClientModel.ApiKeyCredential(apiKey));

                IChatClient chatClient = azureClient.GetChatClient(deploymentName).AsIChatClient();
                services.AddSingleton(chatClient);
                break;

            default:
                throw new InvalidOperationException($"Unknown AI provider: {provider}");
        }

        return services;
    }
}
