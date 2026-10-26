using System.Text.Json.Serialization;

namespace MissionControl.Telemetry;

/// <summary>
/// Contrat JSON des paquets, généré à la compilation (pas de réflexion sur le chemin chaud).
/// Noms courts : ils sont répétés un million de fois sur le réseau.
/// </summary>
[JsonSerializable(typeof(TelemetryPacket))]
internal sealed partial class TelemetryJsonContext : JsonSerializerContext;
