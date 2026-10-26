using MissionControl.Api.Probes;

namespace MissionControl.Api.Commands;

/// <summary>
/// Centre de commandes : transmet une commande à une sonde de la flotte, simule sa réponse
/// et l'inscrit dans l'historique.
/// </summary>
public sealed class CommandCenter(ProbeRegistry registry, CommandHistory history, TimeProvider clock)
{
    public const double MaxDeltaV = 100;
    public const double MaxRotationFromRest = 90;
    public static readonly TimeSpan RotationDeadline = TimeSpan.FromSeconds(30);

    /// <summary>Envoie la commande ; <c>false</c> si la sonde n'est pas dans la flotte.</summary>
    public bool TrySend(ProbeCommand command, out CommandResult result)
    {
        if (!registry.Fleet.Any(p => string.Equals(p.Name, command.ProbeName, StringComparison.OrdinalIgnoreCase)))
        {
            result = default;
            return false;
        }

        if (command.Id == Guid.Empty) command = command with { Id = Guid.CreateVersion7() };

        // Hiérarchie fermée : les quatre cas suffisent, aucune branche default.
        result = command switch
        {
            Thrust thrust => Execute(thrust),
            Rotate rotate => Execute(rotate),
            CapturePhoto photo => Execute(photo),
            SelfDestruct selfDestruct => Execute(selfDestruct),
        };

        history.Record(command, result);
        return true;
    }

    private CommandResult Execute(Thrust thrust) =>
        Math.Abs(thrust.DeltaV) > MaxDeltaV
            ? new Rejected(thrust.Id, $"Poussée supérieure à la réserve de carburant ({MaxDeltaV} m/s).")
            : Acknowledge(thrust);

    private CommandResult Execute(Rotate rotate)
    {
        if (rotate.Attitude is < 0 or >= 360)
            return new Rejected(rotate.Id, "Attitude cible hors de l'intervalle [0°, 360°[.");

        // La sonde pivote lentement : au-delà de 90° de sa position de repos, elle ne répond pas à temps.
        var fromRest = Math.Min(rotate.Attitude, 360 - rotate.Attitude);
        return fromRest > MaxRotationFromRest ? new TimedOut(rotate.Id, RotationDeadline) : Acknowledge(rotate);
    }

    private CommandResult Execute(CapturePhoto photo) =>
        string.IsNullOrWhiteSpace(photo.Target)
            ? new Rejected(photo.Id, "La cible de la photo est obligatoire.")
            : Acknowledge(photo);

    private CommandResult Execute(SelfDestruct selfDestruct) =>
        string.Equals(selfDestruct.ConfirmationCode, selfDestruct.ProbeName, StringComparison.OrdinalIgnoreCase)
            ? Acknowledge(selfDestruct)
            : new Rejected(selfDestruct.Id, "Code de confirmation incorrect : retapez le nom de la sonde.");

    private Acknowledged Acknowledge(ProbeCommand command) => new(command.Id, clock.GetUtcNow());
}
