using System.Collections.Concurrent;

namespace MissionControl.Api.Probes;

public sealed record Probe(string Name, DateTime LaunchWindowOpensAt, DateTime LaunchWindowClosesAt)
{
    public bool Overlaps(DateTime opensAt, DateTime closesAt) => LaunchWindowOpensAt < closesAt && opensAt < LaunchWindowClosesAt;
}

public enum RegistrationStatus
{
    Registered,
    NameTaken,
    WindowTaken,
}

/// <summary>
/// Registre des sondes de la flotte. Répond « ce nom est-il disponible ? » avec une latence simulée.
/// </summary>
public sealed class ProbeRegistry
{
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(600);

    private static readonly string[] HistoricMissions = ["Spoutnik", "Apollo", "Rosetta"];

    // 🆕 C# 15 : collection expression arguments — with(...) passe le comparateur au constructeur.
    private static readonly HashSet<string> ReservedNames =
        [with(StringComparer.OrdinalIgnoreCase), "Voyager", "Pioneer", .. HistoricMissions];

    private readonly ConcurrentDictionary<string, Probe> _probes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _registering = new();

    public IReadOnlyList<Probe> Fleet => [.. _probes.Values.OrderBy(p => p.LaunchWindowOpensAt)];

    public async Task<bool> IsNameAvailableAsync(string name, CancellationToken cancellationToken = default)
    {
        await Task.Delay(Latency, cancellationToken);

        return !ReservedNames.Contains(name) && !_probes.ContainsKey(name);
    }

    /// <summary>
    /// Revérifie le nom et la fenêtre au moment d'enregistrer, d'un seul geste : deux demandes validées
    /// en même temps ne peuvent prendre ni le même nom ni le même créneau.
    /// </summary>
    public RegistrationStatus Register(Probe probe)
    {
        lock (_registering)
        {
            if (ReservedNames.Contains(probe.Name) || _probes.ContainsKey(probe.Name))
                return RegistrationStatus.NameTaken;

            if (_probes.Values.Any(p => p.Overlaps(probe.LaunchWindowOpensAt, probe.LaunchWindowClosesAt)))
                return RegistrationStatus.WindowTaken;

            _probes[probe.Name] = probe;
            return RegistrationStatus.Registered;
        }
    }
}
