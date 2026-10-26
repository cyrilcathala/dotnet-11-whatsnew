using System.Text.Json;
using System.Text.Json.Serialization;

namespace MissionControl.Telemetry;

/// <summary>
/// Valeur lue sur un capteur. Pour l'instant toujours numérique :
/// deviendra <c>union SensorValue(double, string, bool)</c> au chapitre suivant.
/// Toute la (dé)sérialisation est isolée ici pour que ce remplacement ne touche rien d'autre.
/// </summary>
[JsonConverter(typeof(SensorValueJsonConverter))]
public readonly record struct SensorValue(double Reading)
{
    public static implicit operator SensorValue(double reading) => new(reading);

    public override string ToString() => Reading.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
}

/// <summary>Écrit la valeur comme un simple nombre JSON, sans objet englobant.</summary>
public sealed class SensorValueJsonConverter : JsonConverter<SensorValue>
{
    public override SensorValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDouble();

    public override void Write(Utf8JsonWriter writer, SensorValue value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Reading);
}
