using System.Diagnostics;

namespace Houston;

/// <summary>
/// Pipeline d'ingestion inspiré de Mission Control : 5 niveaux d'await,
/// la panne est injectée au plus profond (étalonnage du capteur).
/// </summary>
public static class TelemetryPipeline
{
    public static async Task<double> IngestBatchAsync(int packets, int faultyPacket = -1)
    {
        var total = 0.0;
        for (var sequence = 0; sequence < packets; sequence++)
        {
            // Attente du paquet suivant : vraie suspension, la suite reprend sur le pool de threads.
            await Task.Yield();

            total += await DecodePacketAsync(sequence, sequence == faultyPacket);
        }
        return total;
    }

    private static async Task<double> DecodePacketAsync(int sequence, bool faulty)
    {
        var reading = await ValidateReadingAsync(sequence, faulty);
        return reading * 1.5;
    }

    private static async Task<double> ValidateReadingAsync(int sequence, bool faulty)
    {
        var reading = await ReadSensorAsync(sequence, faulty);
        return Math.Max(reading, 0);
    }

    private static async Task<double> ReadSensorAsync(int sequence, bool faulty)
    {
        var raw = await CalibrateAsync(sequence, faulty);
        return raw + 0.5;
    }

    private static async Task<double> CalibrateAsync(int sequence, bool faulty)
    {
        var offset = await CalibrationTable.GetOffsetAsync(sequence);

        if (faulty)
        {
            // La boîte noire capture la pile d'appels réelle au moment de la panne.
            throw new SensorFaultException(sequence, new StackTrace(fNeedFileInfo: false));
        }

        return sequence % 100 + offset;
    }
}

/// <summary>Table d'étalonnage en cache : la réponse est disponible immédiatement.</summary>
public static class CalibrationTable
{
    private static readonly Task<double> CachedOffset = Task.FromResult(0.25);

    public static Task<double> GetOffsetAsync(int sequence) => CachedOffset;
}

public sealed class SensorFaultException(int sequence, StackTrace capturedStack)
    : InvalidOperationException($"Houston, on a un problème : capteur de pression hors limites (paquet #{sequence}).")
{
    public StackTrace CapturedStack { get; } = capturedStack;
}
