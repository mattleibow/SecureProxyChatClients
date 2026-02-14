using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SecureProxyChatClients.Client.Web;
using SecureProxyChatClients.Client.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<AuthState>();
builder.Services.AddTransient<AuthenticatedHttpMessageHandler>();

var serverUrl = builder.Configuration.GetValue<string>("ServerUrl")
    ?? "http://localhost:5167";

builder.Services.AddHttpClient("ServerApi", client =>
    {
        client.BaseAddress = new Uri(serverUrl);
    })
    .AddHttpMessageHandler<AuthenticatedHttpMessageHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ServerApi"));

await builder.Build().RunAsync();
