using System.Runtime.InteropServices;
using BenchmarkDotNet.Running;
using Houston;

if (args is ["bench", .. var benchArgs])
{
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(benchArgs);
    return;
}

Console.WriteLine($"🚀 Houston — {RuntimeInformation.FrameworkDescription}");
Console.WriteLine();

try
{
    await TelemetryPipeline.IngestBatchAsync(packets: 3, faultyPacket: 2);
}
catch (SensorFaultException fault)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(fault.Message);
    Console.ResetColor();

    // 🆕 .NET 11 : avec Runtime Async, plus de frames de state machine dans la pile réelle.
    Console.WriteLine();
    Console.WriteLine("Pile d'appels capturée au moment de la panne :");
    Console.WriteLine(fault.CapturedStack);
}
