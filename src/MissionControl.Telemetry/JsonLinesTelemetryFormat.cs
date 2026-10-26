using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MissionControl.Telemetry;

/// <summary>
/// JSON Lines : un paquet JSON compact par ligne, terminé par LF.
/// </summary>
internal sealed class JsonLinesTelemetryFormat : ITelemetryFormat
{
    private const int FlushThreshold = 64 * 1024;

    public string Name => "jsonl";

    public string MediaType => "application/jsonl";

    public async IAsyncEnumerable<TelemetryPacket> ReadAsync(
        PipeReader source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 🔁 Rappel .NET 10 : STJ lit directement un PipeReader, valeur par valeur (topLevelValues).
        var packets = JsonSerializer.DeserializeAsyncEnumerable(
            source, TelemetryJsonContext.Default.TelemetryPacket, topLevelValues: true, cancellationToken);

        await foreach (var packet in packets)
        {
            if (packet is not null)
            {
                yield return packet;
            }
        }
    }

    public async Task WriteAsync(
        PipeWriter destination, IAsyncEnumerable<TelemetryPacket> packets, CancellationToken cancellationToken = default)
    {
        await using var json = new Utf8JsonWriter(destination);

        await foreach (var packet in packets.WithCancellation(cancellationToken))
        {
            JsonSerializer.Serialize(json, packet, TelemetryJsonContext.Default.TelemetryPacket);
            json.Flush();
            json.Reset();
            destination.Write("\n"u8);

            if (!destination.CanGetUnflushedBytes || destination.UnflushedBytes >= FlushThreshold)
            {
                await destination.FlushAsync(cancellationToken);
            }
        }

        await destination.FlushAsync(cancellationToken);
    }
}
