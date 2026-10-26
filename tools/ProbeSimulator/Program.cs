using System.Diagnostics;
using System.Net;
using MissionControl.Telemetry;
using ProbeSimulator;

// Simulateur de sondes : génère la télémétrie et l'envoie à l'API en HTTP/2, au fil de l'eau.
//   --format jsonl          format du body (jsonl ; cbor et json arrivent ensuite)
//   --compression none      compression du body (none ; zstd arrive ensuite)
//   --count 1000000         nombre de paquets
//   --seed 42               graine du générateur
//   --url https://...       adresse de l'API (par défaut : celle fournie par Aspire)

if (!SimulatorOptions.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine("Usage : ProbeSimulator [--format jsonl] [--compression none] [--count 1000000] [--seed 42] [--url https://localhost:7269]");
    return 2;
}

using var http = new HttpClient
{
    BaseAddress = options.ApiUrl,
    Timeout = Timeout.InfiniteTimeSpan,
    DefaultRequestVersion = HttpVersion.Version20,
    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
};

Console.WriteLine($"🛰️  {options.Count:N0} paquets en {options.Format.Name} (compression : {options.Compression}, graine {options.Seed}) → {options.ApiUrl}");

var stopwatch = Stopwatch.StartNew();
var packets = Progress(ProbeFleet.GenerateAsync(options.Count, options.Seed), options.Count);
using var response = await http.PostAsync("/telemetry/import", new TelemetryContent(options.Format, packets));
var body = await response.Content.ReadAsStringAsync();

if (!response.IsSuccessStatusCode)
{
    Console.Error.WriteLine($"❌ {(int)response.StatusCode} {response.ReasonPhrase} : {body}");
    return 1;
}

Console.WriteLine($"✅ Import terminé en {stopwatch.Elapsed.TotalSeconds:N1} s (HTTP/{response.Version}) : {body}");
return 0;

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
