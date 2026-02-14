using System.Net.Http.Headers;

namespace SecureProxyChatClients.Client.Web.Services;

public class AuthenticatedHttpMessageHandler(AuthState authState) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (authState.IsAuthenticated)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
