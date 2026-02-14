using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Server.AI;

public sealed class CopilotCliChatClient(ILogger<CopilotCliChatClient> logger, string model = "gpt-5-mini") : IChatClient
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(60);

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

        try
        {
            using Process process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start copilot process.");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ProcessTimeout);

            string output = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);

            if (process.ExitCode != 0)
            {
                string stderr = await process.StandardError.ReadToEndAsync(CancellationToken.None);
                logger.LogWarning("Copilot CLI exited with code {ExitCode}: {StdErr}", process.ExitCode, stderr);
            }

            string cleanOutput = StripCopilotFooter(output);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, cleanOutput));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Copilot CLI timed out after {Timeout}s", ProcessTimeout.TotalSeconds);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "AI request timed out. Please try again."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Copilot CLI invocation failed");
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "AI service temporarily unavailable."));
        }
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
