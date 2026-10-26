using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MissionControl.Telemetry;

namespace MissionControl.Tests;

public sealed class LogbookExportTests : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public LogbookExportTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Export_writes_one_compact_json_line_per_entry()
    {
        const string body = """
            {"probe":"Rosetta","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"radiation","value":0.42}
            {"probe":"Juno","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"temperature","value":-12.5}
            {"probe":"Juno","seq":1,"at":"2026-11-17T08:00:01+00:00","sensor":"battery","value":98}

            """;
        await _client.PostAsync("/telemetry/import", new StringContent(body, Encoding.UTF8, "application/jsonl"), Ct);

        var response = await _client.GetAsync("/logbook/export", HttpCompletionOption.ResponseHeadersRead, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/jsonl", response.Content.Headers.ContentType?.MediaType);
        var jsonl = await response.Content.ReadAsStringAsync(Ct);

        Assert.EndsWith("\n", jsonl);
        Assert.DoesNotContain("\r", jsonl);
        var lines = jsonl[..^1].Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.All(lines, line =>
        {
            using var entry = JsonDocument.Parse(line);
            Assert.Equal(JsonValueKind.Object, entry.RootElement.ValueKind);
            Assert.DoesNotContain(" ", line);
        });
        Assert.Equal(
            ["Rosetta #0", "Juno #0", "Juno #1"],
            lines.Select(line => JsonSerializer.Deserialize<EntryDto>(line, JsonSerializerOptions.Web)!).Select(e => $"{e.Probe} #{e.Seq}"));
    }

    [Fact]
    public async Task Exported_logbook_can_be_imported_again()
    {
        const string body = """
            {"probe":"Juno","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"temperature","value":-12.5}
            {"probe":"Juno","seq":1,"at":"2026-11-17T08:00:01+00:00","sensor":"battery","value":98}

            """;
        await _client.PostAsync("/telemetry/import", new StringContent(body, Encoding.UTF8, "application/jsonl"), Ct);
        var exported = await _client.GetStringAsync("/logbook/export", Ct);

        await using var other = new WebApplicationFactory<Program>();
        using var otherClient = other.CreateClient();
        await otherClient.PostAsync("/telemetry/import", new StringContent(exported, Encoding.UTF8, "application/jsonl"), Ct);

        Assert.Equal(exported, await otherClient.GetStringAsync("/logbook/export", Ct));
    }

    [Fact]
    public async Task Empty_logbook_exports_an_empty_body()
    {
        var response = await _client.GetAsync("/logbook/export", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/jsonl", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("", await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("jsonl")]
    [InlineData("cbor")]
    public async Task Export_contains_every_imported_packet_in_reception_order(string format)
    {
        // Bien au-delà de l'enregistrement de début de vol (4096 paquets par sonde).
        var packets = Generate(30_011);
        await ImportAsync(TelemetryFormats.FindByName(format)!, packets);

        var exported = await _client.GetStringAsync("/logbook/export", Ct);

        Assert.Equal(packets, ParseLines(exported));
    }

    [Fact]
    public async Task Export_during_an_import_gives_complete_lines_received_so_far()
    {
        var packets = Generate(20_000);
        var import = _client.PostAsync(
            "/telemetry/import", new TelemetryContent(TelemetryFormats.JsonLines, Slowly(packets)), Ct);

        while (await _client.GetFromJsonAsync<StatusDto>("/telemetry", Ct) is { Packets: 0 })
        {
            await Task.Delay(10, Ct);
        }
        var duringImport = ParseLines(await _client.GetStringAsync("/logbook/export", Ct));
        Assert.Equal(HttpStatusCode.OK, (await import).StatusCode);

        Assert.InRange(duringImport.Length, 1, packets.Length - 1);
        Assert.Equal(packets.Take(duringImport.Length), duringImport);
        Assert.Equal(packets, ParseLines(await _client.GetStringAsync("/logbook/export", Ct)));
    }

    [Fact]
    public async Task Logbook_leaves_nothing_on_disk_once_the_station_stops()
    {
        var root = Directory.CreateTempSubdirectory("mission-control-tests-");
        try
        {
            await using (var station = _factory.WithWebHostBuilder(host => host.UseSetting("Logbook:Directory", root.FullName)))
            {
                using var client = station.CreateClient();
                await client.PostAsync(
                    "/telemetry/import", new TelemetryContent(TelemetryFormats.JsonLines, Generate(5_000).ToAsyncEnumerable()), Ct);
                Assert.Equal(5_000, ParseLines(await client.GetStringAsync("/logbook/export", Ct)).Length);
                Assert.NotEmpty(root.EnumerateFiles("*", SearchOption.AllDirectories));
            }

            Assert.Empty(root.EnumerateFiles("*", SearchOption.AllDirectories));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Station_cleans_up_the_logbook_of_a_station_that_was_killed()
    {
        var root = Directory.CreateTempSubdirectory("mission-control-tests-");
        try
        {
            // Une station tuée sans arrêt propre (processus disparu) a laissé son journal derrière elle.
            var orphan = Directory.CreateDirectory(Path.Combine(root.FullName, "mission-control", $"logbook-{int.MaxValue}-dead"));
            await File.WriteAllTextAsync(Path.Combine(orphan.FullName, "journal-de-bord.jsonl"), "{}\n", Ct);

            await using (var station = _factory.WithWebHostBuilder(host => host.UseSetting("Logbook:Directory", root.FullName)))
            {
                using var client = station.CreateClient();
                Assert.Equal("", await client.GetStringAsync("/logbook/export", Ct));
            }

            Assert.False(Directory.Exists(orphan.FullName));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task ImportAsync(ITelemetryFormat format, IReadOnlyList<TelemetryPacket> packets)
    {
        var response = await _client.PostAsync("/telemetry/import", new TelemetryContent(format, packets.ToAsyncEnumerable()), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static TelemetryPacket[] ParseLines(string jsonl)
    {
        Assert.True(jsonl.Length == 0 || jsonl.EndsWith('\n'));
        return [.. jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize(line, TelemetryJsonContext.Default.TelemetryPacket)!)];
    }

    private static TelemetryPacket[] Generate(int count)
    {
        string[] probes = ["Rosetta", "Juno", "Cassini"];
        string[] sensors = ["temperature", "battery", "radiation"];
        var start = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

        return [.. Enumerable.Range(0, count).Select(i => new TelemetryPacket(
            probes[i % probes.Length],
            i / probes.Length,
            start.AddMilliseconds(i * 250),
            sensors[i % sensors.Length],
            Math.Round(Math.Sin(i) * 100, 3)))];
    }

    /// <summary>Un flux qui s'étire : une pause tous les 1000 paquets, pour exporter pendant l'import.</summary>
    private static async IAsyncEnumerable<TelemetryPacket> Slowly(IEnumerable<TelemetryPacket> packets)
    {
        var i = 0;
        foreach (var packet in packets)
        {
            if (++i % 1_000 == 0)
            {
                await Task.Delay(20);
            }
            yield return packet;
        }
    }

    private sealed record StatusDto(long Packets);

    private sealed record EntryDto(string Probe, long Seq);
}
