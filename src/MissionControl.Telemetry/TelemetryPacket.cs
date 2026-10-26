using System.Text.Json.Serialization;

namespace MissionControl.Telemetry;

/// <summary>
/// Un relevé de capteur émis par une sonde.
/// </summary>
/// <param name="Probe">Nom de la sonde émettrice.</param>
/// <param name="Sequence">Numéro d'ordre du paquet pour cette sonde.</param>
/// <param name="Timestamp">Heure de bord de la mesure.</param>
/// <param name="Sensor">Capteur mesuré (température, batterie...).</param>
/// <param name="Value">Valeur lue.</param>
public sealed record TelemetryPacket(
    [property: JsonPropertyName("probe")] string Probe,
    [property: JsonPropertyName("seq")] long Sequence,
    [property: JsonPropertyName("at")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("sensor")] string Sensor,
    [property: JsonPropertyName("value")] SensorValue Value);
