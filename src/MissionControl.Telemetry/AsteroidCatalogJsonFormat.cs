using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MissionControl.Telemetry;

/// <summary>
/// JSON : le catalogue d'astéroïdes, un seul document qui embarque les relevés dans <c>observations</c>.
/// Contrairement à JSONL et CBOR, le document n'est exploitable qu'une fois lu en entier :
/// c'est le point de comparaison du banc d'essai.
/// </summary>
public sealed class AsteroidCatalogJsonFormat : ITelemetryFormat
{
    private const int FlushThreshold = 64 * 1024;

    /// <summary>Astéroïdes listés dans l'en-tête des catalogues produits par le simulateur.</summary>
    public static IReadOnlyList<Asteroid> KnownAsteroids { get; } =
    [
        new("1 Ceres", "Cérès", 939.4),
        new("4 Vesta", "Vesta", 525.4),
        new("2 Pallas", "Pallas", 512),
        new("433 Eros", "Éros", 16.8),
        new("25143 Itokawa", "Itokawa", 0.33),
        new("162173 Ryugu", "Ryugu", 0.9),
        new("101955 Bennu", "Bennu", 0.49),
        new("99942 Apophis", "Apophis", 0.37),
    ];

    public string Name => "json";

    public string MediaType => "application/json";

    /// <summary>Lit le catalogue complet : le document est désérialisé d'un bloc.</summary>
    public async ValueTask<AsteroidCatalog> ReadCatalogAsync(PipeReader source, CancellationToken cancellationToken = default)
    {
        // 🔁 Rappel .NET 10 : STJ désérialise directement depuis un PipeReader (Request.BodyReader), sans Stream.
        var catalog = await JsonSerializer.DeserializeAsync(
            source, AsteroidCatalogJsonContext.Default.AsteroidCatalog, cancellationToken);

        return catalog ?? throw new JsonException("Le catalogue d'astéroïdes est vide.");
    }

    public async IAsyncEnumerable<TelemetryPacket> ReadAsync(
        PipeReader source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var catalog = await ReadCatalogAsync(source, cancellationToken);
        foreach (var packet in catalog.Observations)
        {
            yield return packet;
        }
    }

    /// <summary>
    /// Écrit le catalogue au fil de l'eau : en-tête, puis chaque relevé dans le tableau <c>observations</c>.
    /// L'écriture reste en flux, seule la lecture a besoin du document entier.
    /// </summary>
    public async Task WriteAsync(
        PipeWriter destination, IAsyncEnumerable<TelemetryPacket> packets, CancellationToken cancellationToken = default)
    {
        await using var json = new Utf8JsonWriter(destination);

        json.WriteStartObject();
        json.WriteString("name", "Catalogue d'astéroïdes de la flotte");
        json.WriteString("exportedAt", DateTimeOffset.UtcNow);
        json.WritePropertyName("asteroids");
        JsonSerializer.Serialize(json, KnownAsteroids, AsteroidCatalogJsonContext.Default.IReadOnlyListAsteroid);
        json.WriteStartArray("observations");

        await foreach (var packet in packets.WithCancellation(cancellationToken))
        {
            JsonSerializer.Serialize(json, packet, AsteroidCatalogJsonContext.Default.TelemetryPacket);

            if (json.BytesPending >= FlushThreshold)
            {
                json.Flush();
                await destination.FlushAsync(cancellationToken);
            }
        }

        json.WriteEndArray();
        json.WriteEndObject();
        json.Flush();
        await destination.FlushAsync(cancellationToken);
    }
}
