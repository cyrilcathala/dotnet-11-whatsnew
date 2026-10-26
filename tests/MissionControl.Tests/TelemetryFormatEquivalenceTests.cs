using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MissionControl.Telemetry;

namespace MissionControl.Tests;

/// <summary>
/// Le même jeu de paquets, importé dans chacun des formats connus, doit produire le même état
/// pour les données communes (les paquets). Chaque format est comparé à JSONL, la référence.
/// </summary>
public sealed class TelemetryFormatEquivalenceTests
{
    private const int Count = 20_011;
    private static readonly string[] Probes = ["Juno", "Rosetta", "Cassini", "Huygens", "Galileo"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> Formats
        => [.. TelemetryFormats.All.Select(f => f.Name).Where(name => name != TelemetryFormats.JsonLines.Name)];

    [Theory]
    [MemberData(nameof(Formats))]
    public async Task Same_packets_give_the_same_state_as_jsonl(string format)
    {
        var expected = await ImportAndObserveAsync(TelemetryFormats.JsonLines);

        var actual = await ImportAndObserveAsync(TelemetryFormats.FindByName(format)!);

        Assert.Equal(Count, actual.Packets);
        Assert.Equal(expected.Packets, actual.Packets);
        Assert.Equal(expected.Probes, actual.Probes);
        Assert.Equal(expected.LaunchWindow, actual.LaunchWindow);
    }

    /// <summary>Importe le jeu de données dans une API neuve et relève l'état observable via l'API.</summary>
    private static async Task<ObservedState> ImportAndObserveAsync(ITelemetryFormat format)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/telemetry/import", new TelemetryContent(format, Generate()), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await client.GetFromJsonAsync<TelemetryStatusDto>("/telemetry", Ct);
        var window = await client.GetFromJsonAsync<LaunchWindowDto>("/telemetry/probes/Cassini/launch?minutes=10", Ct);

        // L'heure de réception dépend de l'exécution : seule la donnée importée est comparée.
        return new ObservedState(
            status!.Packets,
            [.. status.Probes.Select(p => p with { LastReceivedAt = default })],
            window!.Packets);
    }

    private static async IAsyncEnumerable<TelemetryPacket> Generate()
    {
        string[] sensors = ["temperature", "battery", "radiation", "pressure"];
        var start = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < Count; i++)
        {
            var sequence = i / Probes.Length;
            yield return new TelemetryPacket(
                Probes[i % Probes.Length],
                sequence,
                start.AddMilliseconds(sequence * 250),
                sensors[sequence % sensors.Length],
                Math.Round(Math.Sin(i) * 100, 3));

            if (i % 5_000 == 0)
            {
                await Task.Yield();
            }
        }
    }

    private sealed record ObservedState(long Packets, ProbeTelemetryDto[] Probes, PacketDto[] LaunchWindow);

    private sealed record TelemetryStatusDto(long Packets, ProbeTelemetryDto[] Probes);

    private sealed record ProbeTelemetryDto(
        string Name, long Packets, DateTimeOffset FirstAt, DateTimeOffset LastAt, double LastValue, DateTimeOffset LastReceivedAt);

    private sealed record LaunchWindowDto(string Probe, DateTimeOffset From, DateTimeOffset To, PacketDto[] Packets);

    private sealed record PacketDto(string Probe, long Seq, DateTimeOffset At, string Sensor, double Value);
}
