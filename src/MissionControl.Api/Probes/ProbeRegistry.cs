using System.Collections.Concurrent;

namespace MissionControl.Api.Probes;

public sealed record Probe(string Name, DateTime LaunchWindowOpensAt, DateTime LaunchWindowClosesAt);

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

    public IReadOnlyList<Probe> Fleet => [.. _probes.Values.OrderBy(p => p.LaunchWindowOpensAt)];

    public async Task<bool> IsNameAvailableAsync(string name, CancellationToken cancellationToken = default)
    {
        await Task.Delay(Latency, cancellationToken);

        return !ReservedNames.Contains(name) && !_probes.ContainsKey(name);
    }

    public bool TryRegister(Probe probe) => !ReservedNames.Contains(probe.Name) && _probes.TryAdd(probe.Name, probe);
}
