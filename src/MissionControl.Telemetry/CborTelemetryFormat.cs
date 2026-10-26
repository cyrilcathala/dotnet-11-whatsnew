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

    // Octets du segment suivant recopiés au minimum avec un paquet coupé : un paquet de télémétrie y tient largement.
    private const int MinStitch = 512;

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
        byte[]? stitched = null;

        try
        {
            while (true)
            {
                var result = await source.ReadAsync(cancellationToken);
                var remaining = result.Buffer;

                // CborReader veut une mémoire contiguë : on lit le pipe segment par segment, sans copie.
                // Seul un paquet coupé à la fin d'un segment est recopié, avec le début du segment suivant.
                while (true)
                {
                    var window = remaining.IsSingleSegment || remaining.First.Length > stream.BytesRemaining
                        ? remaining.First
                        : Stitch(remaining, 2 * stream.BytesRemaining + MinStitch, ref stitched);
                    var isLast = window.Length == remaining.Length;

                    // La fenêtre commence par les octets que le lecteur n'a pas encore consommés
                    // (le paquet coupé) : on la fait glisser sur les nouvelles données.
                    stream.SlideData(window, isFinalBlock: result.IsCompleted && isLast);
                    DecodeCompletePackets(stream, packet, window, decoded);

                    remaining = remaining.Slice(window.Length - stream.BytesRemaining);
                    if (isLast)
                    {
                        break;
                    }
                }

                // NeedsMoreData : on rend au pipe ce qui est consommé, et on attend la suite.
                source.AdvanceTo(remaining.Start, result.Buffer.End);

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
            if (stitched is not null)
            {
                ArrayPool<byte>.Shared.Return(stitched);
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

    /// <summary>Seuls les paquets complets sont décodés : TrySkipValue refuse (sans bouger) un paquet coupé.</summary>
    private static void DecodeCompletePackets(
        CborReader stream, CborReader packet, ReadOnlyMemory<byte> window, List<TelemetryPacket> decoded)
    {
        while (stream.PeekState() is not (CborReaderState.Finished or CborReaderState.NeedsMoreData))
        {
            var start = window.Length - stream.BytesRemaining;
            if (!stream.TrySkipValue())
            {
                return;
            }

            packet.Reset(window[start..^stream.BytesRemaining], isFinalBlock: true);
            decoded.Add(Decode(packet));
        }
    }

    /// <summary>
    /// Recopie dans un tampon loué les <paramref name="length"/> premiers octets restants : la queue du paquet coupé
    /// et le début du segment suivant, pas plus. Si le paquet n'y tient toujours pas, la fenêtre suivante double.
    /// </summary>
    private static ReadOnlyMemory<byte> Stitch(ReadOnlySequence<byte> remaining, int length, ref byte[]? rented)
    {
        var bytes = remaining.Slice(0, Math.Min(length, remaining.Length));        if (rented is null || rented.Length < bytes.Length)
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
            rented = ArrayPool<byte>.Shared.Rent((int)bytes.Length);
        }

        bytes.CopyTo(rented);
        return rented.AsMemory(0, (int)bytes.Length);
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

        try
        {
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
        }
        catch (InvalidOperationException exception)
        {
            // CborReader signale un type inattendu (ex. "seq" en texte) par InvalidOperationException : le payload est mal formé.
            throw new CborContentException($"Paquet CBOR mal formé : {exception.Message}", exception);
        }

        return new TelemetryPacket(probe, sequence, timestamp, sensor, value);
    }

    /// <summary>Correspondance entre les types CBOR et les cas de l'union.</summary>
    private static SensorValue ReadSensorValue(CborReader cbor) => cbor.PeekState() switch
    {
        // Les entiers CBOR vont de -2⁶⁴ à 2⁶⁴ - 1 : au-delà de long, ReadInt64 lèverait OverflowException.
        CborReaderState.UnsignedInteger => (double)cbor.ReadUInt64(),
        CborReaderState.NegativeInteger => -1d - cbor.ReadCborNegativeIntegerRepresentation(),
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
