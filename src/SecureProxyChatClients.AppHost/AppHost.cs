var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with pgvector for story memory (optional — falls back to in-memory)
var vectorDb = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("vectorstore");

var server = builder.AddProject<Projects.SecureProxyChatClients_Server>("server")
    .WithReference(vectorDb)
    .WaitFor(vectorDb);

var client = builder.AddProject<Projects.SecureProxyChatClients_Client_Web>("client-web")
    .WithReference(server)
    .WaitFor(server);

builder.Build().Run();
