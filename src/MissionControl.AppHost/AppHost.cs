var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.MissionControl_Api>("api")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.Build().Run();
