using MissionControl.Telemetry;

namespace ProbeSimulator;

/// <summary>
/// Génère la télémétrie d'une flotte de sondes. Graine fixe : deux exécutions
/// avec la même graine produisent exactement les mêmes paquets.
/// </summary>
internal static class ProbeFleet
{
    private static readonly string[] Probes = ["Juno", "Rosetta", "Cassini", "Huygens", "Galileo", "Philae", "Messenger", "Ulysses"];
    private static readonly string[] Sensors = ["temperature", "battery", "radiation", "pressure"];
    private static readonly DateTimeOffset MissionStart = new(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

    public static async IAsyncEnumerable<TelemetryPacket> GenerateAsync(int count, int seed)
    {
        var random = new Random(seed);
        var readings = new double[Probes.Length, Sensors.Length];

        for (var i = 0; i < count; i++)
        {
            var probe = i % Probes.Length;
            var sequence = i / Probes.Length;
            var sensor = sequence % Sensors.Length;

            // Marche aléatoire : des courbes plausibles plutôt que du bruit.
            readings[probe, sensor] += random.NextDouble() - 0.5;

            yield return new TelemetryPacket(
                Probes[probe],
                sequence,
                MissionStart.AddSeconds(sequence),
                Sensors[sensor],
                Math.Round(readings[probe, sensor], 3));

            // Laisse respirer l'envoi HTTP de temps en temps.
            if (i % 8192 == 0)
            {
                await Task.Yield();
            }
        }
    }
}
