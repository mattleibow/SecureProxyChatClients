using System.Net.Http.Headers;

namespace SecureProxyChatClients.Client.Web.Services;

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

        return await base.SendAsync(request, cancellationToken);
    }
}
