using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Server.AI;

public sealed class CopilotCliChatClient(string model = "gpt-5-mini") : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ChatMessage? lastMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
        string prompt = lastMessage?.Text ?? string.Empty;

        var psi = new ProcessStartInfo("copilot")
        {
            Arguments = $"""-p "{EscapeForShell(prompt)}" --model {model} --available-tools "" --allow-all-tools""",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(psi)!;
        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        string cleanOutput = StripCopilotFooter(output);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, cleanOutput));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (string word in (response.Messages[0].Text ?? "").Split(' '))
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, word + " ");
            await Task.Delay(30, cancellationToken);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    private static string EscapeForShell(string input) =>
        input.Replace("\"", "\\\"").Replace("\n", " ");

    private static string StripCopilotFooter(string output)
    {
        string[] lines = output.Split('\n');
        int endIndex = lines.Length;
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].Contains("Total usage") || lines[i].Contains("Continuing autonomously"))
            {
                endIndex = i;
                break;
            }
        }
        return string.Join('\n', lines[..endIndex]).TrimEnd();
    }
}
