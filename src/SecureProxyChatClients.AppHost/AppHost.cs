var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddProject<Projects.SecureProxyChatClients_Server>("server");

var client = builder.AddProject<Projects.SecureProxyChatClients_Client_Web>("client-web")
    .WithReference(server)
    .WaitFor(server);

builder.Build().Run();
