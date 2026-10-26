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
public sealed record ImportSummary(string Format, long Packets, int Probes, TimeSpan Duration, double PacketsPerSecond);

/// <summary>Lit les paquets au fil de l'eau et alimente le store par lots.</summary>
public sealed class TelemetryImporter(TelemetryStore store) : ITelemetryImporter
{
    public const int BatchSize = 1024;

    public async Task<ImportSummary> ImportAsync(PipeReader body, ITelemetryFormat format, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var probes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long packets = 0;

        // 🆕 C# 15 : collection expression avec arguments, la capacité du tampon est fixée dès la création.
        List<TelemetryPacket> batch = [with(capacity: BatchSize)];

        await foreach (var packet in format.ReadAsync(body, cancellationToken))
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
