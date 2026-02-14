using Microsoft.JSInterop;

namespace SecureProxyChatClients.Client.Web.Services;

/// <summary>
/// Client-side authentication state.
/// Uses a static token field so all DI scope instances (including IHttpClientFactory handlers)
/// share the same auth state. Raises <see cref="OnChange"/> so UI components can re-render.
/// SECURITY NOTE: Token is stored in sessionStorage for page-refresh survival.
/// For production apps requiring higher security, consider a BFF (Backend-for-Frontend)
/// pattern with HttpOnly/Secure/SameSite cookies, or use short-lived access tokens
/// with a refresh token flow. sessionStorage is tab-scoped and cleared on tab close,
/// which limits exposure compared to localStorage.
/// </summary>
public class AuthState(IJSRuntime jsRuntime)
{
    private const string StorageKey = "auth_token";

    // Static so all DI-scoped instances (including HttpClientFactory handler scope) share the token
    private static string? s_accessToken;
    private static bool s_initialized;

    /// <summary>Raised when authentication state changes so UI components can re-render.</summary>
    public static event Action? OnChange;

    public string? AccessToken => s_accessToken;
    public bool IsAuthenticated => !string.IsNullOrEmpty(s_accessToken);

    public async Task InitializeAsync()
    {
        if (s_initialized) return;
        s_initialized = true;

        if (!string.IsNullOrEmpty(s_accessToken)) return;

        try
        {
            s_accessToken = await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
        }
        catch
        {
            // SSR or prerender — no JS available
        }
    }

    public async Task SetTokenAsync(string token)
    {
        s_accessToken = token;
        s_initialized = true;
        try
        {
            await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", StorageKey, token);
        }
        catch { /* SSR fallback */ }
        OnChange?.Invoke();
    }

    public async Task ClearAsync()
    {
        s_accessToken = null;
        s_initialized = false;
        try
        {
            await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
        }
        catch { /* SSR fallback */ }
        OnChange?.Invoke();
    }
}
