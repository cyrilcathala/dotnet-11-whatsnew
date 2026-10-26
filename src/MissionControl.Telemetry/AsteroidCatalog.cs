using System.Text.Json.Serialization;

namespace MissionControl.Telemetry;

/// <summary>
/// Catalogue d'astéroïdes : un export classique, un seul gros document JSON.
/// En plus des relevés des sondes (<see cref="Observations"/>, les données communes à tous les formats),
/// il porte son en-tête et la liste des astéroïdes observés.
/// </summary>
public sealed record AsteroidCatalog(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("exportedAt")] DateTimeOffset ExportedAt,
    [property: JsonPropertyName("asteroids")] IReadOnlyList<Asteroid> Asteroids,
    [property: JsonPropertyName("observations")] IReadOnlyList<TelemetryPacket> Observations);

/// <summary>Une entrée du catalogue.</summary>
public sealed record Asteroid(
    [property: JsonPropertyName("designation")] string Designation,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("diameterKm")] double DiameterKm);

[JsonSerializable(typeof(AsteroidCatalog))]
internal sealed partial class AsteroidCatalogJsonContext : JsonSerializerContext;
