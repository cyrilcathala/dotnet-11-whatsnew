var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.MissionControl_Api>("api")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

// Démarré à la demande depuis le dashboard (bouton ▶) : chaque lancement envoie un import complet.
builder.AddProject<Projects.ProbeSimulator>("simulator")
    .WithReference(api)
    .WaitFor(api)
    .WithArgs("--format", "jsonl", "--compression", "none", "--count", "1000000", "--seed", "42")
    .WithExplicitStart();

// Banc d'essai : les 6 combinaisons format × compression, en séquence, même graine.
builder.AddProject<Projects.ProbeSimulator>("bench")
    .WithReference(api)
    .WaitFor(api)
    .WithArgs("--format", "all", "--compression", "both", "--count", "1000000", "--seed", "42")
    .WithExplicitStart();

builder.Build().Run();
