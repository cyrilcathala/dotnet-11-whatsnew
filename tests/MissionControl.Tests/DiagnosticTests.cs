using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MissionControl.Tests;

public sealed class DiagnosticTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Diagnostic_reports_the_runtime_and_the_sdk_found_on_the_ground_station()
    {
        var diagnostic = await _client.GetFromJsonAsync<DiagnosticDto>("/mission/diagnostic", Ct);

        Assert.StartsWith(".NET 11", diagnostic!.Runtime);
        // SDK épinglé par le global.json du dépôt, tel que l'affiche « dotnet --version ».
        Assert.StartsWith("11.", diagnostic.Sdk);
        Assert.DoesNotContain("\n", diagnostic.Sdk);
    }

    private sealed record DiagnosticDto(string Runtime, string OperatingSystem, string? Sdk, string? SdkError);
}
