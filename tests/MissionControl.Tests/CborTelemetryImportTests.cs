using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using MissionControl.Telemetry;

namespace MissionControl.Tests;

public sealed class CborTelemetryImportTests
{
    private static readonly string[] Probes = ["Juno", "Rosetta", "Cassini"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cbor_import_feeds_the_telemetry_dashboard()
    {
        await using var api = new WebApplicationFactory<Program>();
        using var client = api.CreateClient();

        var response = await client.PostAsync(
            "/telemetry/import", new TelemetryContent(TelemetryFormats.Cbor, Dataset(3_001).ToAsyncEnumerable()), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ImportSummaryDto>(Ct);
        Assert.Equal("cbor", summary!.Format);
        Assert.Equal(3_001, summary.Packets);
        Assert.Equal(Probes.Length, summary.Probes);

        var status = await client.GetFromJsonAsync<TelemetryStatusDto>("/telemetry", Ct);
        Assert.Equal(3_001, status!.Packets);
        Assert.Equal(Probes.Order(), status.Probes.Select(p => p.Name));
    }

    [Fact]
    public async Task Same_dataset_in_jsonl_and_cbor_gives_the_same_state()
    {
        var packets = Dataset(5_000);

        var fromJsonl = await ImportAndObserveAsync(new TelemetryContent(TelemetryFormats.JsonLines, packets.ToAsyncEnumerable()));
        var fromCbor = await ImportAndObserveAsync(new TelemetryContent(TelemetryFormats.Cbor, packets.ToAsyncEnumerable()));

        Assert.Equal(fromJsonl, fromCbor);
    }

    [Fact]
    public async Task Cbor_sent_in_random_chunks_gives_the_same_state_as_a_single_block()
    {
        var payload = await EncodeAsync(TelemetryFormats.Cbor, Dataset(2_000));

        var singleBlock = await ImportAndObserveAsync(Cbor(new ByteArrayContent(payload)));
        var chunked = await ImportAndObserveAsync(new RandomChunksContent(payload, seed: 7, maxChunkSize: 37));

        Assert.Equal(singleBlock, chunked);
    }

    [Fact]
    public async Task Sensor_values_round_trip_through_json_without_configuration()
    {
        await using var api = new WebApplicationFactory<Program>();
        using var client = api.CreateClient();
        const string body = """
            {"probe":"Juno","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"temperature","value":-12.5}
            {"probe":"Juno","seq":1,"at":"2026-11-17T08:00:01+00:00","sensor":"mode","value":"nominal"}
            {"probe":"Juno","seq":2,"at":"2026-11-17T08:00:02+00:00","sensor":"antenna","value":true}

            """;

        await client.PostAsync("/telemetry/import", new StringContent(body, Encoding.UTF8, "application/jsonl"), Ct);
        var window = await client.GetFromJsonAsync<JsonElement>("/telemetry/probes/Juno/launch", Ct);

        // Les paquets renvoyés par l'API sont relus avec les options STJ par défaut, sans convertisseur.
        var packets = window.GetProperty("packets").Deserialize<TelemetryPacket[]>(JsonSerializerOptions.Default)!;
        Assert.Equal([-12.5, "nominal", true], packets.Select(p => p.Value.Value));
    }

    /// <summary>Importe dans une API neuve, puis relève l'état observable (tableau de bord et débuts de vol).</summary>
    private static async Task<string> ImportAndObserveAsync(HttpContent content)
    {
        await using var api = new WebApplicationFactory<Program>();
        using var client = api.CreateClient();

        var response = await client.PostAsync("/telemetry/import", content, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = (await client.GetFromJsonAsync<JsonObject>("/telemetry", Ct))!;
        foreach (var probe in status["probes"]!.AsArray())
        {
            probe!.AsObject().Remove("lastReceivedAt"); // heure de réception : diffère forcément
        }

        var observed = new StringBuilder(status.ToJsonString());
        foreach (var probe in Probes)
        {
            observed.AppendLine().Append(await client.GetStringAsync($"/telemetry/probes/{probe}/launch?minutes=60", Ct));
        }
        return observed.ToString();
    }

    /// <summary>Un jeu de données déterministe qui couvre les trois cas de <see cref="SensorValue"/>.</summary>
    private static TelemetryPacket[] Dataset(int count)
    {
        var start = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(1));
        return [.. Enumerable.Range(0, count).Select(i => new TelemetryPacket(
            Probes[i % Probes.Length],
            i / Probes.Length,
            start.AddMilliseconds(250.0 * i),
            (i % 4) switch { 0 => "temperature", 1 => "battery", 2 => "mode", _ => "antenna" },
            (i % 4) switch
            {
                0 => -12.5 + i * 0.125,
                1 => (SensorValue)(double)(100 - i % 100),
                2 => i % 7 == 0 ? "dégradé" : "nominal",
                _ => i % 3 == 0,
            }))];
    }

    private static async Task<byte[]> EncodeAsync(ITelemetryFormat format, TelemetryPacket[] packets)
    {
        using var buffer = new MemoryStream();
        var writer = PipeWriter.Create(buffer);
        await format.WriteAsync(writer, packets.ToAsyncEnumerable(), Ct);
        await writer.CompleteAsync();
        return buffer.ToArray();
    }

    private static HttpContent Cbor(HttpContent content)
    {
        content.Headers.ContentType = new("application/cbor");
        return content;
    }

    /// <summary>Écrit le body en morceaux de taille aléatoire, avec un flush entre chaque : des paquets sont coupés en deux.</summary>
    private sealed class RandomChunksContent : HttpContent
    {
        private readonly byte[] _payload;
        private readonly Random _random;
        private readonly int _maxChunkSize;

        public RandomChunksContent(byte[] payload, int seed, int maxChunkSize)
        {
            _payload = payload;
            _random = new Random(seed);
            _maxChunkSize = maxChunkSize;
            Headers.ContentType = new("application/cbor");
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            for (var offset = 0; offset < _payload.Length;)
            {
                var size = Math.Min(_random.Next(1, _maxChunkSize + 1), _payload.Length - offset);
                await stream.WriteAsync(_payload.AsMemory(offset, size));
                await stream.FlushAsync();
                offset += size;
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed record ImportSummaryDto(string Format, long Packets, int Probes);

    private sealed record TelemetryStatusDto(long Packets, ProbeDto[] Probes);

    private sealed record ProbeDto(string Name, long Packets);
}
