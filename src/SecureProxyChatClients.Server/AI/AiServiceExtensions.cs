using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using SecureProxyChatClients.Server.Tools;

namespace SecureProxyChatClients.Server.AI;

public static class AiServiceExtensions
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register the server tool registry
        services.AddSingleton<ServerToolRegistry>();

        string provider = configuration.GetValue<string>("AI:Provider") ?? "Fake";

        switch (provider.ToLowerInvariant())
        {
            case "fake":
                services.AddSingleton<IChatClient>(new FakeChatClient());
                break;

            case "copilotcli":
                string model = configuration.GetValue<string>("AI:CopilotCli:Model") ?? "gpt-5-mini";
                services.AddSingleton<IChatClient>(sp =>
                    new CopilotCliChatClient(sp.GetRequiredService<ILogger<CopilotCliChatClient>>(), model));
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
