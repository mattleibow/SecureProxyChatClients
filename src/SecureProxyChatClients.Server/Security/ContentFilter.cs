using System.Text.RegularExpressions;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Security;

public sealed partial class ContentFilter(ILogger<ContentFilter> logger)
{
    // [Security Warning]
    // Regex-based HTML sanitization is sufficient for this reference sample but can be brittle.
    // For high-security production environments, consider using a dedicated library like HtmlSanitizer.
    [GeneratedRegex(@"<script[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptTagRegex();

    [GeneratedRegex(@"<iframe[^>]*>.*?</iframe>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex IframeTagRegex();

    [GeneratedRegex(@"<object[^>]*>.*?</object>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ObjectTagRegex();

    [GeneratedRegex(@"<embed[^>]*\/?>", RegexOptions.IgnoreCase)]
    private static partial Regex EmbedTagRegex();

    [GeneratedRegex(@"on\w+\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase)]
    private static partial Regex EventHandlerQuotedRegex();

    [GeneratedRegex(@"on\w+\s*=\s*\S+", RegexOptions.IgnoreCase)]
    private static partial Regex EventHandlerUnquotedRegex();

    [GeneratedRegex(@"javascript\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex JavascriptProtocolRegex();

    public ChatResponse FilterResponse(ChatResponse response)
    {
        var filtered = response.Messages.Select(m =>
        {
            if (m.Content is null) return m;

            string sanitized = m.Content;
            sanitized = ScriptTagRegex().Replace(sanitized, "[content removed]");
            sanitized = IframeTagRegex().Replace(sanitized, "[content removed]");
            sanitized = ObjectTagRegex().Replace(sanitized, "[content removed]");
            sanitized = EmbedTagRegex().Replace(sanitized, "[content removed]");
            sanitized = EventHandlerQuotedRegex().Replace(sanitized, "");
            sanitized = EventHandlerUnquotedRegex().Replace(sanitized, "");
            sanitized = JavascriptProtocolRegex().Replace(sanitized, "");

            if (sanitized != m.Content)
                logger.LogWarning("Content filter removed potentially unsafe content from response");

            return m with { Content = sanitized };
        }).ToList();

        return response with { Messages = filtered };
    }
}
