using MissionControl.Api;
using MissionControl.Api.Components;
using MissionControl.Api.Probes;
using MissionControl.Api.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<MissionStore>();
builder.Services.AddProbes();
builder.Services.AddTelemetry();

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

app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapProbes();
app.MapTelemetry();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
