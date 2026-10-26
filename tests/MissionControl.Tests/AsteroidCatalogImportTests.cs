using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MissionControl.Tests;

public sealed class AsteroidCatalogImportTests : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public AsteroidCatalogImportTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Json_catalog_import_feeds_the_telemetry_and_the_catalog()
    {
        const string body = """
            {
              "name": "Ceinture principale",
              "exportedAt": "2026-11-17T07:00:00+00:00",
              "asteroids": [
                { "designation": "1 Ceres", "name": "Cérès", "diameterKm": 939.4 },
                { "designation": "433 Eros", "name": "Éros", "diameterKm": 16.8 }
              ],
              "observations": [
                {"probe":"Juno","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"temperature","value":-12.5},
                {"probe":"Juno","seq":1,"at":"2026-11-17T08:00:01+00:00","sensor":"battery","value":98},
                {"probe":"Rosetta","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"radiation","value":0.42}
              ]
            }
            """;

        var response = await _client.PostAsync(
            "/telemetry/import", new StringContent(body, Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ImportSummaryDto>(Ct);
        Assert.Equal("json", summary!.Format);
        Assert.Equal(3, summary.Packets);
        Assert.Equal(2, summary.Probes);

        var status = await _client.GetFromJsonAsync<TelemetryStatusDto>("/telemetry", Ct);
        Assert.Equal(3, status!.Packets);

        var catalog = await _client.GetFromJsonAsync<CatalogDto>("/telemetry/catalog", Ct);
        Assert.Equal("Ceinture principale", catalog!.Name);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 7, 0, 0, TimeSpan.Zero), catalog.ExportedAt);
        Assert.Equal(3, catalog.Observations);
        Assert.Equal(
            [new("1 Ceres", "Cérès", 939.4), new("433 Eros", "Éros", 16.8)],
            catalog.Asteroids);
    }

    [Fact]
    public async Task Catalog_is_not_found_before_any_json_import()
    {
        var response = await _client.GetAsync("/telemetry/catalog", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed record ImportSummaryDto(string Format, long Packets, int Probes);

    private sealed record TelemetryStatusDto(long Packets);

    private sealed record CatalogDto(string Name, DateTimeOffset ExportedAt, AsteroidDto[] Asteroids, long Observations);

    private sealed record AsteroidDto(string Designation, string Name, double DiameterKm);
}
