var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with pgvector for story memory (optional — falls back to in-memory)
var vectorDb = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("vectorstore");

var server = builder.AddProject<Projects.SecureProxyChatClients_Server>("server")
    .WithReference(vectorDb)
    .WaitFor(vectorDb);

// Allow overriding the AI provider via configuration (useful for testing)
string? aiProvider = builder.Configuration["AI:Provider"];
if (!string.IsNullOrEmpty(aiProvider))
{
    server = server.WithEnvironment("AI__Provider", aiProvider);
}

var client = builder.AddProject<Projects.SecureProxyChatClients_Client_Web>("client-web")
    .WithReference(server)
    .WaitFor(server);

builder.Build().Run();
