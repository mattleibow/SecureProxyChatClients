using System.Net;
using System.Net.Http.Headers;

namespace SecureProxyChatClients.Client.Web.Services;

/// <summary>
/// Attaches bearer token to outgoing requests and clears auth state on 401 responses.
/// </summary>
public class AuthenticatedHttpMessageHandler(AuthState authState) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await authState.InitializeAsync();

        if (authState.IsAuthenticated)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Clear auth state on 401 — session expired or token invalid
        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            await authState.ClearAsync();
        }

        return response;
    }
}
