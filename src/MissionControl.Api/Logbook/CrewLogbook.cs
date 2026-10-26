using System.Runtime.CompilerServices;
using MissionControl.Api.Telemetry;
using MissionControl.Telemetry;

namespace MissionControl.Api.Logbook;

/// <summary>
/// Journal de bord : les entrées conservées par la station, sonde par sonde (ordre alphabétique),
/// chacune dans l'ordre de réception. Ce sont les paquets JSONL reçus, si bien qu'un export
/// peut être réimporté tel quel par <c>POST /telemetry/import</c>.
/// </summary>
public sealed class CrewLogbook(TelemetryStore store)
{
    /// <summary>
    /// Parcourt le journal au fil de l'eau : une seule sonde est copiée hors du verrou à la fois,
    /// le journal complet n'est jamais matérialisé.
    /// </summary>
    public async IAsyncEnumerable<TelemetryPacket> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var probe in store.GetStatus().Probes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var entry in store.GetLaunchRecord(probe.Name) ?? [])
            {
                yield return entry;
            }

            // Laisse le sérialiseur vider son tampon vers la réponse entre deux sondes.
            await Task.Yield();
        }
    }
}
