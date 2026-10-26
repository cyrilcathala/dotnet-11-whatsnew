using System.Diagnostics;
using System.IO.Pipelines;
using MissionControl.Telemetry;

namespace MissionControl.Api.Telemetry;

/// <summary>
/// Importe un flux de télémétrie. Point d'extension : l'instrumentation du banc d'essai
/// enveloppe cette interface (décorateur) pour mesurer chaque import.
/// </summary>
public interface ITelemetryImporter
{
    Task<ImportSummary> ImportAsync(PipeReader body, ITelemetryFormat format, CancellationToken cancellationToken);
}

/// <summary>Résumé renvoyé par <c>POST /telemetry/import</c>.</summary>
public sealed record ImportSummary(
    string Format, long Packets, int Probes, TimeSpan Duration, double PacketsPerSecond, ImportMeasurements? Measurements = null);

/// <summary>
/// Mesures du banc d'essai pour un import. Le taux de compression vaut octets réseau / octets payload.
/// Allocations, working set et CPU portent sur tout le processus.
/// </summary>
public sealed record ImportMeasurements(
    string Compression,
    long NetworkBytes,
    long PayloadBytes,
    double CompressionRatio,
    long AllocatedBytes,
    long PeakWorkingSet,
    TimeSpan CpuTime);

/// <summary>Lit les paquets au fil de l'eau et alimente le store par lots.</summary>
public sealed class TelemetryImporter(TelemetryStore store, AsteroidCatalogStore catalogs) : ITelemetryImporter
{
    public const int BatchSize = 1024;

    public async Task<ImportSummary> ImportAsync(PipeReader body, ITelemetryFormat format, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var probes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long packets = 0;

        // 🆕 C# 15 : collection expression avec arguments, la capacité du tampon est fixée dès la création.
        List<TelemetryPacket> batch = [with(capacity: BatchSize)];

        var source = format.ReadAsync(body, cancellationToken);
        if (format is AsteroidCatalogJsonFormat json)
        {
            // Le catalogue est un seul document : lu en entier, son en-tête est conservé, ses relevés importés.
            var catalog = await json.ReadCatalogAsync(body, cancellationToken);
            catalogs.Record(catalog);
            source = catalog.Observations.ToAsyncEnumerable();
        }

        await foreach (var packet in source)
        {
            batch.Add(packet);
            probes.Add(packet.Probe);
            packets++;

            if (batch.Count == BatchSize)
            {
                store.Append(batch);
                batch.Clear();
            }
        }

        store.Append(batch);

        var duration = stopwatch.Elapsed;
        return new ImportSummary(
            format.Name,
            packets,
            probes.Count,
            duration,
            duration > TimeSpan.Zero ? packets / duration.TotalSeconds : 0);
    }
}
