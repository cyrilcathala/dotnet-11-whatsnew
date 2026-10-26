using System.Text.Json.Serialization;

namespace MissionControl.Telemetry;

/// <summary>
/// Contrat JSON des paquets, généré à la compilation (pas de réflexion sur le chemin chaud).
/// Noms courts : ils sont répétés un million de fois sur le réseau.
/// Public : l'export du journal de bord écrit le même contrat que l'import JSONL.
/// </summary>
[JsonSerializable(typeof(TelemetryPacket))]
public sealed partial class TelemetryJsonContext : JsonSerializerContext;
