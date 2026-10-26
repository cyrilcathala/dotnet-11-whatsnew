#!/usr/bin/env dotnet
#:include MissionApi.cs

// 🆕 .NET 11 : file-based app en plusieurs fichiers, sans .csproj. #:include ajoute MissionApi.cs à la compilation.
// Diagnostic de mission : interroge l'API et résume l'état de la station sol, de la flotte et de la télémétrie.
//
//   dotnet run tools/diagnostic/mission-diagnostic.cs
//   dotnet run tools/diagnostic/mission-diagnostic.cs -e MISSION_API_URL=https://localhost:7269

Console.OutputEncoding = System.Text.Encoding.UTF8;

var baseAddress =Environment.GetEnvironmentVariable("MISSION_API_URL") ?? "http://localhost:5011";
using var api = new MissionApi(new Uri(baseAddress));

Console.WriteLine($"🛰️  Diagnostic de mission → {baseAddress}");
Console.WriteLine();

try
{
    var station = await api.GetAsync("/mission/diagnostic");
    Console.WriteLine("Station sol");
    Console.WriteLine($"  Runtime de l'API : {station.GetProperty("runtime").GetString()}");
    Console.WriteLine($"  Système          : {station.GetProperty("operatingSystem").GetString()}");
    Console.WriteLine($"  SDK installé     : {station.GetProperty("sdk").GetString() ?? station.GetProperty("sdkError").GetString()}");

    var fleet = await api.GetAsync("/probes");
    Console.WriteLine();
    Console.WriteLine($"Flotte : {fleet.GetArrayLength()} sonde(s)");
    foreach (var probe in fleet.EnumerateArray())
    {
        Console.WriteLine($"  {probe.GetProperty("name").GetString(),-16} fenêtre {probe.GetProperty("launchWindowOpensAt").GetDateTime():g} → {probe.GetProperty("launchWindowClosesAt").GetDateTime():g}");
    }

    var telemetry = await api.GetAsync("/telemetry");
    Console.WriteLine();
    Console.WriteLine($"Télémétrie : {telemetry.GetProperty("packets").GetInt64():N0} paquet(s) reçu(s)");
    foreach (var probe in telemetry.GetProperty("probes").EnumerateArray())
    {
        Console.WriteLine($"  {probe.GetProperty("name").GetString(),-16} {probe.GetProperty("packets").GetInt64(),12:N0} paquets, dernier relevé {probe.GetProperty("lastValue")}");
    }

    var report = await api.GetAsync("/mission/report");
    Console.WriteLine();
    Console.WriteLine($"Rapport de mission : {report.GetArrayLength()} ligne(s)");
    foreach (var (category, count) in MissionApi.CountBy(report, "category"))
    {
        Console.WriteLine($"  {category,-16} {count}");
    }

    return 0;
}
catch (HttpRequestException exception)
{
    Console.Error.WriteLine($"❌ API injoignable ({exception.Message}). Lancez-la, ou indiquez son adresse avec -e MISSION_API_URL=...");
    return 1;
}
