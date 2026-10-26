using System.Diagnostics;
using System.Text.Json.Serialization;
using MissionControl.Api.Commands;

namespace MissionControl.Api.Report;

[JsonConverter(typeof(JsonStringEnumConverter<ReportCategory>))]
public enum ReportCategory
{
    /// <summary>La commande a son accusé de réception.</summary>
    [JsonStringEnumMemberName("matched")] Matched,

    /// <summary>Commande sans accusé : perdue dans l'espace.</summary>
    [JsonStringEnumMemberName("unacknowledged")] Unacknowledged,

    /// <summary>Accusé orphelin : la sonde parle sans qu'on lui ait rien demandé.</summary>
    [JsonStringEnumMemberName("orphan")] Orphan,
}

/// <summary>Une ligne du rapport : la commande, l'accusé, ou les deux.</summary>
public sealed record ReportLine(ReportCategory Category, string ProbeName, ProbeCommand? Command, CommandResult? Acknowledgement);

/// <summary>
/// Rapport de mission : rapproche les commandes envoyées des accusés de réception reçus.
/// </summary>
public sealed class MissionReport(CommandHistory history)
{
    public IReadOnlyList<ReportLine> Build()
    {
        var entries = history.Entries;

        // Accusé = réponse de la sonde, positive ou négative ; plus les accusés spontanés.
        ReceivedAck[] acks =
        [
            .. entries.Where(e => IsAcknowledgement(e.Result)).Select(e => new ReceivedAck(e.Command.ProbeName, e.Result)),
            .. history.Unsolicited.Select(u => new ReceivedAck(u.ProbeName, u.Ack)),
        ];

        // 🆕 .NET 11 : FullJoin — les commandes sans accusé et les accusés sans commande restent dans le résultat.
        return
        [
            .. entries.Select(e => e.Command)
                .FullJoin(acks, command => command.Id, ack => ack.Result.CommandId, Classify),
        ];
    }

    // Côté non apparié : null (d'où des records des deux côtés).
    private static ReportLine Classify(ProbeCommand? command, ReceivedAck? ack) => (command, ack) switch
    {
        (not null, not null) => new(ReportCategory.Matched, command.ProbeName, command, ack.Result),
        (not null, null) => new(ReportCategory.Unacknowledged, command.ProbeName, command, null),
        (null, not null) => new(ReportCategory.Orphan, ack.ProbeName, null, ack.Result),
        (null, null) => throw new UnreachableException("FullJoin ne produit jamais une ligne vide."),
    };

    // Union : pas de réponse de la sonde en cas d'expiration ou de perte.
    private static bool IsAcknowledgement(CommandResult result) => result switch
    {
        Acknowledged or Rejected => true,
        TimedOut or ProbeLost => false,
    };

    private sealed record ReceivedAck(string ProbeName, CommandResult Result);
}
