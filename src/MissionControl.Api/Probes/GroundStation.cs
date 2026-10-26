namespace MissionControl.Api.Probes;

/// <summary>
/// Station sol simulée : une fenêtre de lancement est disponible si aucune sonde de la flotte
/// n'a déjà réservé un créneau qui la chevauche.
/// </summary>
public sealed class GroundStation(ProbeRegistry registry)
{
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(400);

    /// <summary>Nombre de jours explorés pour proposer une autre fenêtre.</summary>
    public const int SearchDays = 30;

    public async Task<bool> IsWindowAvailableAsync(DateTime opensAt, DateTime closesAt, CancellationToken cancellationToken = default)
    {
        await Task.Delay(Latency, cancellationToken);

        return !registry.Fleet.Any(p => p.Overlaps(opensAt, closesAt));
    }

    /// <summary>
    /// La fenêtre revient chaque jour à la même heure : la première des <see cref="SearchDays"/> suivantes
    /// qu'aucune sonde n'a réservée, ou <c>null</c>.
    /// </summary>
    public (DateTime OpensAt, DateTime ClosesAt)? FindNextFreeWindow(DateTime opensAt, DateTime closesAt)
    {
        var fleet = registry.Fleet;

        // 🆕 C# 15 : continue labellisé, on passe au jour suivant dès qu'une sonde occupe le créneau (sans drapeau).
        nextDay: for (var day = 1; day <= SearchDays; day++)
        {
            var (opens, closes) = (opensAt.AddDays(day), closesAt.AddDays(day));
            foreach (var probe in fleet)
            {
                if (probe.Overlaps(opens, closes))
                    continue nextDay;
            }

            return (opens, closes);
        }

        return null;
    }
}
