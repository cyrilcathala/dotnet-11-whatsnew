using System.Text.Json.Serialization;

namespace MissionControl.Api.Commands;

/// <summary>
/// Commande envoyée à une sonde. Une famille de types qu'on possède, avec une base commune
/// (identifiant, sonde visée) : c'est une hiérarchie fermée.
/// </summary>
// 🆕 C# 15 : closed — implicitement abstraite, les dérivés vivent dans cet assembly,
// et un switch qui couvre les quatre cas est exhaustif, sans branche default.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Thrust), "thrust")]
[JsonDerivedType(typeof(Rotate), "rotate")]
[JsonDerivedType(typeof(CapturePhoto), "capturePhoto")]
[JsonDerivedType(typeof(SelfDestruct), "selfDestruct")]
public closed record ProbeCommand(Guid Id, string ProbeName);

/// <summary>Poussée : variation de vitesse demandée, en m/s.</summary>
public sealed record Thrust(Guid Id, string ProbeName, double DeltaV) : ProbeCommand(Id, ProbeName);

/// <summary>Rotation : attitude cible, en degrés depuis la position de repos.</summary>
public sealed record Rotate(Guid Id, string ProbeName, double Attitude) : ProbeCommand(Id, ProbeName);

/// <summary>Capture photo d'une cible.</summary>
public sealed record CapturePhoto(Guid Id, string ProbeName, string Target) : ProbeCommand(Id, ProbeName);

/// <summary>Autodestruction : le code de confirmation est le nom de la sonde, retapé.</summary>
public sealed record SelfDestruct(Guid Id, string ProbeName, string ConfirmationCode) : ProbeCommand(Id, ProbeName);
