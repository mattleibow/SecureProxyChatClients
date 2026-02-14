using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SecureProxyChatClients.Client.Web;
using SecureProxyChatClients.Client.Web.Services;
using SecureProxyChatClients.Client.Web.Tools;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<AuthState>();
builder.Services.AddTransient<AuthenticatedHttpMessageHandler>();

var serverUrl = builder.Configuration.GetValue<string>("ServerUrl")
    ?? "https://localhost:5167";

builder.Services.AddHttpClient("ServerApi", client =>
    {
        client.BaseAddress = new Uri(serverUrl);
        client.Timeout = TimeSpan.FromMinutes(5);
    })
    .AddHttpMessageHandler<AuthenticatedHttpMessageHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ServerApi"));

// Story state and client tools
builder.Services.AddSingleton<StoryStateService>();
builder.Services.AddSingleton<GetStoryGraphTool>();
builder.Services.AddSingleton<SearchStoryTool>();
builder.Services.AddSingleton<SaveStoryStateTool>();
builder.Services.AddSingleton<GetWorldRulesTool>();
builder.Services.AddSingleton<ClientToolRegistry>();
builder.Services.AddScoped<ProxyChatClient>();

await builder.Build().RunAsync();
