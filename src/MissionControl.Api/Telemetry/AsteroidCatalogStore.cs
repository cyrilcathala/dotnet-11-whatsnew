using MissionControl.Telemetry;

namespace MissionControl.Api.Telemetry;

/// <summary>
/// Dernier catalogue d'astéroïdes importé : son en-tête et ses astéroïdes.
/// Les relevés qu'il contient vont, eux, dans le <see cref="TelemetryStore"/> comme ceux des autres formats.
/// </summary>
public sealed class AsteroidCatalogStore(TimeProvider clock)
{
    private ImportedCatalog? _current;

    public ImportedCatalog? Current => Volatile.Read(ref _current);

    public void Record(AsteroidCatalog catalog)
        => Volatile.Write(ref _current, new ImportedCatalog(
            catalog.Name, catalog.ExportedAt, catalog.Asteroids, catalog.Observations.Count, clock.GetUtcNow()));
}

/// <summary>Résumé du catalogue renvoyé par <c>GET /telemetry/catalog</c>.</summary>
public sealed record ImportedCatalog(
    string Name,
    DateTimeOffset ExportedAt,
    IReadOnlyList<Asteroid> Asteroids,
    long Observations,
    DateTimeOffset ImportedAt);
