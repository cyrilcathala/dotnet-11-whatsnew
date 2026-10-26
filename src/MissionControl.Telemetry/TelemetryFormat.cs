using System.IO.Pipelines;

namespace MissionControl.Telemetry;

/// <summary>
/// Un format de télémétrie sur le réseau : sait écrire un flux de paquets (simulateur)
/// et le relire depuis un <see cref="PipeReader"/> (API), élément par élément.
/// </summary>
public interface ITelemetryFormat
{
    /// <summary>Nom court, utilisé par le simulateur (<c>--format</c>) et dans les résumés d'import.</summary>
    string Name { get; }

    /// <summary>Valeur de l'en-tête <c>Content-Type</c> associée.</summary>
    string MediaType { get; }

    /// <summary>Lit les paquets au fil de l'arrivée des données, sans charger le payload complet.</summary>
    IAsyncEnumerable<TelemetryPacket> ReadAsync(PipeReader source, CancellationToken cancellationToken = default);

    /// <summary>Écrit les paquets au fil de l'eau.</summary>
    Task WriteAsync(PipeWriter destination, IAsyncEnumerable<TelemetryPacket> packets, CancellationToken cancellationToken = default);
}

/// <summary>Les formats connus, retrouvés par nom ou par <c>Content-Type</c>.</summary>
public static class TelemetryFormats
{
    public static ITelemetryFormat JsonLines { get; } = new JsonLinesTelemetryFormat();

    public static ITelemetryFormat Cbor { get; } = new CborTelemetryFormat();

    public static IReadOnlyList<ITelemetryFormat> All { get; } = [JsonLines, Cbor];

    public static ITelemetryFormat? FindByName(string name)
        => All.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Retrouve le format d'après un <c>Content-Type</c> (paramètres comme <c>charset</c> ignorés).</summary>
    public static ITelemetryFormat? FindByMediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        return All.FirstOrDefault(f => string.Equals(f.MediaType, mediaType, StringComparison.OrdinalIgnoreCase));
    }
}
