using MissionControl.Api;
using MissionControl.Api.Commands;
using MissionControl.Api.Components;
using MissionControl.Api.Probes;
using MissionControl.Api.Report;
using MissionControl.Api.Telemetry;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<MissionStore>();
builder.Services.AddProbes();
builder.Services.AddCommands();
builder.Services.AddTelemetry();
builder.Services.AddMissionReport();

// 🆕 ASP.NET Core 11 : le middleware de décompression gère zstd nativement (zstd, br, deflate, gzip).
builder.Services.AddRequestDecompression();

builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "Mission Control";
    return Task.CompletedTask;
}));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
// Banc d'essai : les octets réseau se comptent avant que le body ne soit décompressé.
app.UseImportTraffic();
app.UseRequestDecompression();

app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapScalarApiReference();
app.MapProbes();
app.MapCommands();
app.MapTelemetry();
app.MapMissionReport();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
