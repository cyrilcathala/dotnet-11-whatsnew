using MissionControl.Telemetry;

namespace MissionControl.Api.Telemetry;

/// <summary>
/// État de la télémétrie reçue, en mémoire et thread-safe.
/// La mémoire reste bornée quel que soit le volume importé : on garde des compteurs par sonde
/// et seulement l'enregistrement du début de vol (les premiers paquets de chaque sonde).
/// </summary>
public sealed class TelemetryStore(TimeProvider clock)
{
    /// <summary>Nombre de paquets conservés par sonde pour l'enregistrement du début de vol.</summary>
    public const int LaunchRecordCapacity = 4096;

    // 🆕 .NET 11 : EqualityComparer<T>.Create avec un sélecteur de clé, sans classe IEqualityComparer<T> écrite à la main.
    // Un paquet retransmis porte le même numéro de séquence et le même capteur.
    private static readonly EqualityComparer<TelemetryPacket> SamePacket =
        EqualityComparer<TelemetryPacket>.Create(p => (p!.Sequence, p.Sensor));

    private readonly Lock _gate = new();
    private readonly Dictionary<string, ProbeRecord> _probes = new(StringComparer.OrdinalIgnoreCase);
    private long _packets;

    public long Packets => Interlocked.Read(ref _packets);

    /// <summary>Ajoute un lot de paquets (un seul verrou pour tout le lot).</summary>
    public void Append(IReadOnlyList<TelemetryPacket> batch)
    {
        var receivedAt = clock.GetUtcNow();
        lock (_gate)
        {
            foreach (var packet in batch)
            {
                if (!_probes.TryGetValue(packet.Probe, out var probe))
                {
                    probe = new ProbeRecord(packet.Probe, packet.Timestamp);
                    _probes.Add(packet.Probe, probe);
                }

                probe.Add(packet, receivedAt);
            }

            Interlocked.Add(ref _packets, batch.Count);
        }
    }

    public TelemetryStatus GetStatus()
    {
        lock (_gate)
        {
            return new TelemetryStatus(
                _packets,
                [.. _probes.Values
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(p => new ProbeTelemetry(p.Name, p.Packets, p.FirstAt, p.LastAt, p.LastValue, p.LastReceivedAt))]);
        }
    }

    /// <summary>Copie de l'enregistrement du début de vol d'une sonde, dans l'ordre de réception.</summary>
    public IReadOnlyList<TelemetryPacket>? GetLaunchRecord(string probe)
    {
        lock (_gate)
        {
            return _probes.TryGetValue(probe, out var record) ? [.. record.LaunchRecord] : null;
        }
    }

    private sealed class ProbeRecord(string name, DateTimeOffset firstAt)
    {
        public string Name { get; } = name;
        public long Packets { get; private set; }
        public DateTimeOffset FirstAt { get; private set; } = firstAt;
        public DateTimeOffset LastAt { get; private set; } = firstAt;
        public SensorValue LastValue { get; private set; }
        public DateTimeOffset LastReceivedAt { get; private set; }
        public List<TelemetryPacket> LaunchRecord { get; } = [];
        private readonly HashSet<TelemetryPacket> _recorded = new(SamePacket);

        public void Add(TelemetryPacket packet, DateTimeOffset receivedAt)
        {
            Packets++;
            if (packet.Timestamp < FirstAt) FirstAt = packet.Timestamp;
            if (packet.Timestamp >= LastAt)
            {
                LastAt = packet.Timestamp;
                LastValue = packet.Value;
            }
            LastReceivedAt = receivedAt;

            if (LaunchRecord.Count < LaunchRecordCapacity && _recorded.Add(packet))
            {
                LaunchRecord.Add(packet);
            }
        }
    }
}

public sealed record TelemetryStatus(long Packets, IReadOnlyList<ProbeTelemetry> Probes);

public sealed record ProbeTelemetry(
    string Name,
    long Packets,
    DateTimeOffset FirstAt,
    DateTimeOffset LastAt,
    SensorValue LastValue,
    DateTimeOffset LastReceivedAt);
