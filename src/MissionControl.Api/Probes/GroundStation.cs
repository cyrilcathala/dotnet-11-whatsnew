namespace MissionControl.Api.Probes;

/// <summary>
/// Station sol simulée : une fenêtre de lancement est disponible si aucune sonde de la flotte
/// n'a déjà réservé un créneau qui la chevauche.
/// </summary>
public sealed class GroundStation(ProbeRegistry registry)
{
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(400);

    public async Task<bool> IsWindowAvailableAsync(DateTime opensAt, DateTime closesAt, CancellationToken cancellationToken = default)
    {
        await Task.Delay(Latency, cancellationToken);

        return !registry.Fleet.Any(p => p.LaunchWindowOpensAt < closesAt && opensAt < p.LaunchWindowClosesAt);
    }
}
