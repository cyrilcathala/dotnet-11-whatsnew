using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using MissionControl.Telemetry;

namespace MissionControl.Tests;

public sealed class TelemetryImportTests : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public TelemetryImportTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Jsonl_import_feeds_the_telemetry_dashboard()
    {
        const string body = """
            {"probe":"Juno","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"temperature","value":-12.5}
            {"probe":"Juno","seq":1,"at":"2026-11-17T08:00:01+00:00","sensor":"battery","value":98}
            {"probe":"Rosetta","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"radiation","value":0.42}

            """;

        var response = await _client.PostAsync("/telemetry/import", Jsonl(body), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ImportSummaryDto>(Ct);
        Assert.Equal("jsonl", summary!.Format);
        Assert.Equal(3, summary.Packets);

        var status = await _client.GetFromJsonAsync<TelemetryStatusDto>("/telemetry", Ct);
        Assert.Equal(3, status!.Packets);
        Assert.Equal(
            [new("Juno", 2, Utc(8, 0, 1)), new("Rosetta", 1, Utc(8, 0, 0))],
            status.Probes.Select(p => new ProbeLine(p.Name, p.Packets, p.LastAt)));
    }

    [Fact]
    public async Task Streamed_jsonl_import_counts_every_packet()
    {
        string[] probes = ["Juno", "Rosetta", "Cassini", "Huygens", "Galileo"];
        const int count = 50_003;

        var response = await _client.PostAsync(
            "/telemetry/import", new TelemetryContent(TelemetryFormats.JsonLines, Generate(probes, count)), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ImportSummaryDto>(Ct);
        Assert.Equal(count, summary!.Packets);
        Assert.Equal(probes.Length, summary.Probes);

        var status = await _client.GetFromJsonAsync<TelemetryStatusDto>("/telemetry", Ct);
        Assert.Equal(count, status!.Packets);
        Assert.Equal(count, status.Probes.Sum(p => p.Packets));
        Assert.Equal(probes.Order(), status.Probes.Select(p => p.Name));
    }

    [Fact]
    public async Task Launch_window_returns_the_first_minutes_of_flight()
    {
        const string body = """
            {"probe":"Juno","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"temperature","value":-12.5}
            {"probe":"Juno","seq":1,"at":"2026-11-17T08:02:00+00:00","sensor":"temperature","value":-13}
            {"probe":"Rosetta","seq":0,"at":"2026-11-17T08:03:00+00:00","sensor":"radiation","value":0.42}
            {"probe":"Juno","seq":2,"at":"2026-11-17T08:04:59+00:00","sensor":"battery","value":97.5}
            {"probe":"Juno","seq":3,"at":"2026-11-17T08:05:00+00:00","sensor":"battery","value":97.4}
            {"probe":"Juno","seq":4,"at":"2026-11-17T08:30:00+00:00","sensor":"temperature","value":-20}

            """;
        await _client.PostAsync("/telemetry/import", Jsonl(body), Ct);

        var window = await _client.GetFromJsonAsync<LaunchWindowDto>("/telemetry/probes/juno/launch?minutes=5", Ct);

        Assert.Equal("Juno", window!.Probe);
        Assert.Equal(Utc(8, 0, 0), window.From);
        Assert.Equal(Utc(8, 5, 0), window.To);
        Assert.Equal([0, 1, 2], window.Packets.Select(p => p.Seq));
        Assert.Equal(97.5, window.Packets[^1].Value);
    }

    [Fact]
    public async Task Launch_window_of_an_unknown_probe_is_not_found()
    {
        var response = await _client.GetAsync("/telemetry/probes/Voyager/launch", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("text/csv")]
    [InlineData("application/xml")]
    public async Task Unsupported_content_type_is_rejected(string contentType)
    {
        var response = await _client.PostAsync(
            "/telemetry/import", new StringContent("probe;seq\nJuno;0\n", Encoding.UTF8, contentType), Ct);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var status = await _client.GetFromJsonAsync<TelemetryStatusDto>("/telemetry", Ct);
        Assert.Equal(0, status!.Packets);
    }

    private static async IAsyncEnumerable<TelemetryPacket> Generate(string[] probes, int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return new TelemetryPacket(
                probes[i % probes.Length], i / probes.Length, Utc(8, 0, 0).AddSeconds(i / probes.Length), "temperature", i * 0.5);

            if (i % 10_000 == 0)
            {
                await Task.Yield();
            }
        }
    }

    private static HttpContent Jsonl(string body)
        => new StringContent(body, Encoding.UTF8, "application/jsonl");

    private static DateTimeOffset Utc(int hours, int minutes, int seconds)
        => new(2026, 11, 17, hours, minutes, seconds, TimeSpan.Zero);

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed record ImportSummaryDto(string Format, long Packets, int Probes);

    private sealed record TelemetryStatusDto(long Packets, ProbeTelemetryDto[] Probes);

    private sealed record ProbeTelemetryDto(string Name, long Packets, DateTimeOffset FirstAt, DateTimeOffset LastAt);

    private sealed record LaunchWindowDto(string Probe, DateTimeOffset From, DateTimeOffset To, PacketDto[] Packets);

    private sealed record PacketDto(string Probe, long Seq, DateTimeOffset At, string Sensor, double Value);

    private sealed record ProbeLine(string Name, long Packets, DateTimeOffset LastAt);
}
