using System.Text.Json.Serialization;

namespace MissionControl.Api.Commands;

/// <summary>La sonde a exécuté la commande (horodatage de bord).</summary>
public sealed record Acknowledged(Guid CommandId, DateTimeOffset ProbeTime);

/// <summary>La sonde a refusé la commande.</summary>
public sealed record Rejected(Guid CommandId, string Reason);

/// <summary>La sonde n'a pas répondu dans le délai.</summary>
public sealed record TimedOut(Guid CommandId, TimeSpan After);

/// <summary>La sonde ne répond plus du tout (dernier contact connu).</summary>
public sealed record ProbeLost(Guid CommandId, DateTimeOffset LastContact);

/// <summary>
/// Résultat d'une commande : des records indépendants, sans base commune, réunis au cas par cas.
/// </summary>
// 🆕 C# 15 : union — compose des types existants sans hiérarchie (à la différence de ProbeCommand).
// 🆕 .NET 11 : STJ écrit le cas actif tel quel (ni enveloppe ni $type) et le reconnaît à ses propriétés.
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union CommandResult(Acknowledged, Rejected, TimedOut, ProbeLost)
{
    public Guid CommandId => this switch
    {
        Acknowledged a => a.CommandId,
        Rejected r => r.CommandId,
        TimedOut t => t.CommandId,
        ProbeLost l => l.CommandId,
    };
}
