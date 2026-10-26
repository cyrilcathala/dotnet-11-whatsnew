using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using MissionControl.Telemetry;

namespace MissionControl.Api.Telemetry;

/// <summary>
/// Banc d'essai : publie les mesures de chaque import en métriques OpenTelemetry
/// (histogrammes tagués <c>format</c> et <c>compression</c>) et garde la dernière mesure
/// de chaque combinaison format × compression pour la page « Banc d'essai ».
/// </summary>
public sealed class TestBench
{
    public const string MeterName = "MissionControl.Telemetry";

    /// <summary>Les compressions comparées par le banc (le simulateur n'en envoie pas d'autres).</summary>
    public static IReadOnlyList<string> Compressions { get; } = ["none", "zstd"];

    /// <summary>Ordre du tableau : du document unique au flux binaire.</summary>
    public static IReadOnlyList<ITelemetryFormat> Formats { get; } = [TelemetryFormats.Json, TelemetryFormats.JsonLines, TelemetryFormats.Cbor];

    private readonly ConcurrentDictionary<(string Format, string Compression), ImportSummary> _latest = new();
    private readonly Histogram<long> _networkBytes;
    private readonly Histogram<long> _payloadBytes;
    private readonly Histogram<long> _allocatedBytes;
    private readonly Histogram<long> _peakWorkingSet;
    private readonly Histogram<double> _cpuTime;
    private readonly Histogram<double> _duration;
    private readonly Histogram<double> _throughput;

    public TestBench(IMeterFactory meters)
    {
        var meter = meters.Create(MeterName);
        _networkBytes = meter.CreateHistogram<long>("mission_control.import.network_bytes", "By", "Octets reçus sur le réseau (avant décompression)");
        _payloadBytes = meter.CreateHistogram<long>("mission_control.import.payload_bytes", "By", "Octets du payload (après décompression)");
        _allocatedBytes = meter.CreateHistogram<long>("mission_control.import.allocated_bytes", "By", "Mémoire allouée pendant l'import (tout le processus)");
        _peakWorkingSet = meter.CreateHistogram<long>("mission_control.import.peak_working_set", "By", "Pic de working set pendant l'import");
        _cpuTime = meter.CreateHistogram<double>("mission_control.import.cpu_time", "s", "Temps CPU consommé pendant l'import (tout le processus)");
        _duration = meter.CreateHistogram<double>("mission_control.import.duration", "s", "Durée de l'import");
        _throughput = meter.CreateHistogram<double>("mission_control.import.throughput", "{packet}/s", "Débit de l'import");
    }

    /// <summary>Déclenché après chaque import mesuré.</summary>
    public event Action? Recorded;

    public void Record(ImportSummary summary)
    {
        if (summary.Measurements is not { } m)
        {
            return;
        }

        var tags = new TagList { { "format", summary.Format }, { "compression", m.Compression } };
        _networkBytes.Record(m.NetworkBytes, tags);
        _payloadBytes.Record(m.PayloadBytes, tags);
        _allocatedBytes.Record(m.AllocatedBytes, tags);
        _peakWorkingSet.Record(m.PeakWorkingSet, tags);
        _cpuTime.Record(m.CpuTime.TotalSeconds, tags);
        _duration.Record(summary.Duration.TotalSeconds, tags);
        _throughput.Record(summary.PacketsPerSecond, tags);

        _latest[(summary.Format, m.Compression)] = summary;
        Recorded?.Invoke();
    }

    /// <summary>Une ligne par combinaison format × compression, mesurée ou pas encore.</summary>
    public IReadOnlyList<TestBenchRow> GetRows()
        => [.. from format in Formats
               from compression in Compressions
               select new TestBenchRow(format.Name, compression, _latest.GetValueOrDefault((format.Name, compression)))];
}

/// <summary>Une ligne du tableau du banc d'essai ; <c>Last</c> est vide tant que la combinaison n'a pas été importée.</summary>
public sealed record TestBenchRow(string Format, string Compression, ImportSummary? Last);
