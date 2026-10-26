namespace MissionControl.Api.Commands;

/// <summary>Une commande envoyée et le résultat obtenu.</summary>
public sealed record CommandLogEntry(ProbeCommand Command, CommandResult Result);

/// <summary>
/// Historique en mémoire des commandes envoyées et des accusés de réception (résultats) obtenus.
/// Thread-safe, enregistré en singleton ; relu par le rapport de mission.
/// </summary>
public sealed class CommandHistory
{
    private readonly Lock _lock = new();
    private readonly List<CommandLogEntry> _entries = [];

    public IReadOnlyList<CommandLogEntry> Entries
    {
        get { lock (_lock) return [.. _entries]; }
    }

    public IReadOnlyList<ProbeCommand> Commands => [.. Entries.Select(e => e.Command)];

    public IReadOnlyList<CommandResult> Results => [.. Entries.Select(e => e.Result)];

    public void Record(ProbeCommand command, CommandResult result)
    {
        lock (_lock) _entries.Add(new(command, result));
    }
}
