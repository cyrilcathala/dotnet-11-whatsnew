using System.Buffers;
using System.Formats.Cbor;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace MissionControl.Telemetry;

/// <summary>
/// CBOR : une suite de maps à la racine, une par paquet, avec les mêmes clés courtes qu'en JSON.
/// </summary>
internal sealed class CborTelemetryFormat : ITelemetryFormat
{
    private const int FlushThreshold = 64 * 1024;

    // Lax : obligatoire pour lire avec isFinalBlock: false.
    private static readonly CborReaderOptions StreamOptions = new()
    {
        ConformanceMode = CborConformanceMode.Lax,
        AllowMultipleRootLevelValues = true,
    };

    public string Name => "cbor";

    public string MediaType => "application/cbor";

    public async IAsyncEnumerable<TelemetryPacket> ReadAsync(
        PipeReader source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 🆕 .NET 11 : CborReader incrémental, il sait qu'il n'a pas encore tout le payload.
        var stream = new CborReader(ReadOnlyMemory<byte>.Empty, StreamOptions, isFinalBlock: false);
        var packet = new CborReader(ReadOnlyMemory<byte>.Empty, CborConformanceMode.Lax);
        var decoded = new List<TelemetryPacket>();
        byte[]? contiguous = null;

        try
        {
            while (true)
            {
                var result = await source.ReadAsync(cancellationToken);
                var buffer = result.Buffer;
                var memory = Contiguous(buffer, ref contiguous);

                // Le tampon du pipe commence par les octets que le lecteur n'a pas encore consommés
                // (voir AdvanceTo plus bas) : on fait glisser la fenêtre sur les nouvelles données.
                stream.SlideData(memory, isFinalBlock: result.IsCompleted);

                // Seuls les paquets complets sont décodés : TrySkipValue refuse (sans bouger) un paquet coupé.
                while (stream.PeekState() is not (CborReaderState.Finished or CborReaderState.NeedsMoreData))
                {
                    var start = memory.Length - stream.BytesRemaining;
                    if (!stream.TrySkipValue())
                    {
                        break;
                    }

                    packet.Reset(memory[start..^stream.BytesRemaining], isFinalBlock: true);
                    decoded.Add(Decode(packet));
                }

                // NeedsMoreData : on rend au pipe ce qui est consommé, et on attend la suite.
                source.AdvanceTo(buffer.GetPosition(memory.Length - stream.BytesRemaining), buffer.End);

                foreach (var item in decoded)
                {
                    yield return item;
                }
                decoded.Clear();

                if (result.IsCompleted)
                {
                    if (stream.BytesRemaining != 0)
                    {
                        throw new CborContentException("Payload CBOR tronqué : le dernier paquet est incomplet.");
                    }
                    yield break;
                }
            }
        }
        finally
        {
            if (contiguous is not null)
            {
                ArrayPool<byte>.Shared.Return(contiguous);
            }
        }
    }

    public async Task WriteAsync(
        PipeWriter destination, IAsyncEnumerable<TelemetryPacket> packets, CancellationToken cancellationToken = default)
    {
        var cbor = new CborWriter(CborConformanceMode.Lax);

        await foreach (var packet in packets.WithCancellation(cancellationToken))
        {
            cbor.Reset();
            Encode(cbor, packet);
            destination.Advance(cbor.Encode(destination.GetSpan(cbor.BytesWritten)));

            if (!destination.CanGetUnflushedBytes || destination.UnflushedBytes >= FlushThreshold)
            {
                await destination.FlushAsync(cancellationToken);
            }
        }

        await destination.FlushAsync(cancellationToken);
    }

    /// <summary>CborReader veut une mémoire contiguë : sans copie si le pipe n'a qu'un segment, sinon copie dans un tampon loué.</summary>
    private static ReadOnlyMemory<byte> Contiguous(ReadOnlySequence<byte> buffer, ref byte[]? rented)
    {
        if (buffer.IsSingleSegment)
        {
            return buffer.First;
        }

        if (rented is null || rented.Length < buffer.Length)
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
            rented = ArrayPool<byte>.Shared.Rent((int)buffer.Length);
        }

        buffer.CopyTo(rented);
        return rented.AsMemory(0, (int)buffer.Length);
    }

    private static void Encode(CborWriter cbor, TelemetryPacket packet)
    {
        cbor.WriteStartMap(5);
        cbor.WriteTextString("probe");
        cbor.WriteTextString(packet.Probe);
        cbor.WriteTextString("seq");
        cbor.WriteInt64(packet.Sequence);
        cbor.WriteTextString("at");
        cbor.WriteDateTimeOffset(packet.Timestamp);
        cbor.WriteTextString("sensor");
        cbor.WriteTextString(packet.Sensor);
        cbor.WriteTextString("value");
        switch (packet.Value)
        {
            case double reading: cbor.WriteDouble(reading); break;
            case string text: cbor.WriteTextString(text); break;
            case bool flag: cbor.WriteBoolean(flag); break;
            case null: cbor.WriteNull(); break;
        }
        cbor.WriteEndMap();
    }

    private static TelemetryPacket Decode(CborReader cbor)
    {
        string probe = "", sensor = "";
        long sequence = 0;
        DateTimeOffset timestamp = default;
        SensorValue value = default;

        cbor.ReadStartMap();
        while (cbor.PeekState() != CborReaderState.EndMap)
        {
            switch (cbor.ReadTextString())
            {
                case "probe": probe = cbor.ReadTextString(); break;
                case "seq": sequence = cbor.ReadInt64(); break;
                case "at": timestamp = cbor.ReadDateTimeOffset(); break;
                case "sensor": sensor = cbor.ReadTextString(); break;
                case "value": value = ReadSensorValue(cbor); break;
                default: cbor.SkipValue(); break;
            }
        }
        cbor.ReadEndMap();

        return new TelemetryPacket(probe, sequence, timestamp, sensor, value);
    }

    /// <summary>Correspondance entre les types CBOR et les cas de l'union.</summary>
    private static SensorValue ReadSensorValue(CborReader cbor) => cbor.PeekState() switch
    {
        CborReaderState.UnsignedInteger or CborReaderState.NegativeInteger => (double)cbor.ReadInt64(),
        CborReaderState.HalfPrecisionFloat or CborReaderState.SinglePrecisionFloat or CborReaderState.DoublePrecisionFloat => cbor.ReadDouble(),
        CborReaderState.TextString => cbor.ReadTextString(),
        CborReaderState.Boolean => cbor.ReadBoolean(),
        CborReaderState.Null => ReadNull(cbor),
        var other => throw new CborContentException($"Valeur de capteur CBOR non prise en charge : {other}."),
    };

    private static SensorValue ReadNull(CborReader cbor)
    {
        cbor.ReadNull();
        return default;
    }
}
