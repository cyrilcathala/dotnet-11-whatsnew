using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using MissionControl.Telemetry;
using ProbeSimulator;

// Simulateur de sondes : génère la télémétrie et l'envoie à l'API en HTTP/2, au fil de l'eau.
//   --format jsonl          format du body (json | jsonl | cbor | all)
//   --compression none      compression du body (none | zstd | both)
//   --count 1000000         nombre de paquets
//   --seed 42               graine du générateur
//   --url https://...       adresse de l'API (par défaut : celle fournie par Aspire)
//
// Banc d'essai : --format all --compression both enchaîne les 6 combinaisons EN SÉQUENCE,
// avec la même graine, puis affiche le tableau des mesures renvoyées par l'API.

if (!SimulatorOptions.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine("Usage : ProbeSimulator [--format json|jsonl|cbor|all] [--compression none|zstd|both] [--count 1000000] [--seed 42] [--url https://localhost:7269]");
    return 2;
}

using var http = new HttpClient
{
    BaseAddress = options.ApiUrl,
    Timeout = Timeout.InfiniteTimeSpan,
    DefaultRequestVersion = HttpVersion.Version20,
    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
};

List<JsonElement> summaries = [];
foreach (var format in options.Formats)
{
    foreach (var compression in options.Compressions)
    {
        // Les mesures portent sur tout le processus de l'API : un import à la fois, jamais en parallèle.
        if (await ImportAsync(format, compression) is not { } summary)
        {
            return 1;
        }
        summaries.Add(summary);
    }
}

if (summaries.Count > 1)
{
    PrintBench(summaries);
}
return 0;

async Task<JsonElement?> ImportAsync(ITelemetryFormat format, string compression)
{
    Console.WriteLine($"🛰️  {options.Count:N0} paquets en {format.Name} (compression : {compression}, graine {options.Seed}) → {options.ApiUrl}");

    var stopwatch = Stopwatch.StartNew();
    var packets = Progress(ProbeFleet.GenerateAsync(options.Count, options.Seed), options.Count);
    HttpContent content = new TelemetryContent(format, packets);
    if (compression == "zstd")
    {
        // 🆕 .NET 11 : compresse au fil de l'envoi et pose Content-Encoding: zstd (le Content-Type est conservé).
        content = new ZstandardCompressedContent(content, CompressionLevel.Fastest);
    }

    using var response = await http.PostAsync("/telemetry/import", content);
    var body = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"❌ {(int)response.StatusCode} {response.ReasonPhrase} : {body}");
        return null;
    }

    Console.WriteLine($"✅ Import terminé en {stopwatch.Elapsed.TotalSeconds:N1} s (HTTP/{response.Version}) : {body}");
    return JsonDocument.Parse(body).RootElement.Clone();
}

static void PrintBench(IReadOnlyList<JsonElement> summaries)
{
    Console.WriteLine();
    Console.WriteLine("Banc d'essai (mesures sur tout le processus de l'API, imports séquentiels)");
    Console.WriteLine($"| {"Format",-6} | {"Compr.",-6} | {"Paquets",10} | {"Réseau",10} | {"Payload",10} | {"Taux",6} | {"Alloc.",10} | {"Pic WS",10} | {"CPU",7} | {"Durée",7} | {"Débit (p/s)",12} |");
    Console.WriteLine($"|{new string('-', 8)}|{new string('-', 8)}|{new string('-', 12)}|{new string('-', 12)}|{new string('-', 12)}|{new string('-', 8)}|{new string('-', 12)}|{new string('-', 12)}|{new string('-', 9)}|{new string('-', 9)}|{new string('-', 14)}|");
    foreach (var s in summaries)
    {
        var m = s.GetProperty("measurements");
        Console.WriteLine(
            $"| {s.GetProperty("format").GetString(),-6} | {m.GetProperty("compression").GetString(),-6} " +
            $"| {s.GetProperty("packets").GetInt64(),10:N0} " +
            $"| {Mb(m, "networkBytes"),10} | {Mb(m, "payloadBytes"),10} | {m.GetProperty("compressionRatio").GetDouble(),6:P0} " +
            $"| {Mb(m, "allocatedBytes"),10} | {Mb(m, "peakWorkingSet"),10} " +
            $"| {Seconds(m, "cpuTime"),7} | {Seconds(s, "duration"),7} | {s.GetProperty("packetsPerSecond").GetDouble(),12:N0} |");
    }

    static string Mb(JsonElement e, string name) => $"{e.GetProperty(name).GetInt64() / 1_000_000d:N1} Mo";

    static string Seconds(JsonElement e, string name)
        => $"{TimeSpan.Parse(e.GetProperty(name).GetString()!, CultureInfo.InvariantCulture).TotalSeconds:N2} s";
}

static async IAsyncEnumerable<TelemetryPacket> Progress(IAsyncEnumerable<TelemetryPacket> packets, int total)
{
    var step = Math.Max(1, total / 10);
    var sent = 0;
    await foreach (var packet in packets)
    {
        yield return packet;
        if (++sent % step == 0)
        {
            Console.WriteLine($"   … {sent:N0} / {total:N0}");
        }
    }
}
