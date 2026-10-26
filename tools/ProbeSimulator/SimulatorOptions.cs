using System.Diagnostics.CodeAnalysis;
using MissionControl.Telemetry;

namespace ProbeSimulator;

internal sealed record SimulatorOptions(ITelemetryFormat Format, string Compression, int Count, int Seed, Uri ApiUrl)
{
    private const int DefaultCount = 1_000_000;
    private const int DefaultSeed = 42;

    public static bool TryParse(string[] args, [NotNullWhen(true)] out SimulatorOptions? options, [NotNullWhen(false)] out string? error)
    {
        options = null;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                error = $"Argument invalide : {args[i]}";
                return false;
            }
            values[args[i][2..]] = args[i + 1];
        }

        var formatName = values.GetValueOrDefault("format", "jsonl");
        if (TelemetryFormats.FindByName(formatName) is not { } format)
        {
            error = $"Format non disponible : {formatName} (disponibles : {string.Join(", ", TelemetryFormats.All.Select(f => f.Name))}).";
            return false;
        }

        var compression = values.GetValueOrDefault("compression", "none");
        if (compression != "none")
        {
            error = $"Compression non disponible : {compression} (disponible : none).";
            return false;
        }

        if (!int.TryParse(values.GetValueOrDefault("count", $"{DefaultCount}"), out var count) || count < 0)
        {
            error = "--count doit être un entier positif.";
            return false;
        }

        if (!int.TryParse(values.GetValueOrDefault("seed", $"{DefaultSeed}"), out var seed))
        {
            error = "--seed doit être un entier.";
            return false;
        }

        // Aspire injecte l'adresse de l'API (WithReference) : on préfère HTTPS, qui négocie HTTP/2.
        var url = values.GetValueOrDefault("url")
            ?? Environment.GetEnvironmentVariable("services__api__https__0")
            ?? Environment.GetEnvironmentVariable("services__api__http__0")
            ?? "https://localhost:7269";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var apiUrl))
        {
            error = $"Adresse invalide : {url}";
            return false;
        }

        options = new SimulatorOptions(format, compression, count, seed, apiUrl);
        error = null;
        return true;
    }
}
