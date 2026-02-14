using Microsoft.JSInterop;

namespace SecureProxyChatClients.Client.Web.Services;

public class AuthState(IJSRuntime jsRuntime)
{
    private const string StorageKey = "auth_token";
    private string? _accessToken;
    private bool _initialized;

    public string? AccessToken => _accessToken;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        // If token was already set in memory (e.g., after login), no need to read from storage
        if (!string.IsNullOrEmpty(_accessToken)) return;

        try
        {
            _accessToken = await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
        }
        catch
        {
            // SSR or prerender — no JS available
        }
    }

    public async Task SetTokenAsync(string token)
    {
        _accessToken = token;
        _initialized = true;
        try
        {
            await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", StorageKey, token);
        }
        catch { /* SSR fallback */ }
    }

    public async Task ClearAsync()
    {
        _accessToken = null;
        _initialized = false;
        try
        {
            await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
        }
        catch { /* SSR fallback */ }
    }
}
