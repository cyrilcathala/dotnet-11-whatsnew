using System.Buffers;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Text;

namespace MissionControl.Api.Telemetry;

/// <summary>
/// Repère un body compressé en zstd envoyé sans <c>Content-Encoding</c> (erreur classique côté client)
/// et en extrait un aperçu décompressé pour le message d'erreur. Le body n'est pas consommé.
/// </summary>
internal static class UndeclaredZstd
{
    /// <summary>Un bloc zstd fait au plus 128 Kio : cette fenêtre suffit pour décompresser le début du flux.</summary>
    private const int PreviewWindow = 160 * 1024;

    private const int PreviewLength = 120;

    /// <summary>Numéro magique d'une trame zstd (RFC 8878), little-endian.</summary>
    private static ReadOnlySpan<byte> FrameMagic => [0x28, 0xB5, 0x2F, 0xFD];

    /// <summary>
    /// Renvoie <c>null</c> si le body ne commence pas par une trame zstd, sinon un aperçu
    /// (éventuellement vide) du contenu décompressé.
    /// </summary>
    public static async Task<string?> DetectAsync(PipeReader body, CancellationToken cancellationToken)
    {
        var result = await body.ReadAtLeastAsync(FrameMagic.Length, cancellationToken);
        var isZstd = StartsWithMagic(result.Buffer);
        body.AdvanceTo(result.Buffer.Start);
        if (!isZstd)
        {
            return null;
        }

        result = await body.ReadAtLeastAsync(PreviewWindow, cancellationToken);
        var window = result.Buffer.Slice(0, Math.Min(result.Buffer.Length, PreviewWindow));
        var preview = Preview(window);
        body.AdvanceTo(result.Buffer.Start);
        return preview;
    }

    private static bool StartsWithMagic(ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length < FrameMagic.Length)
        {
            return false;
        }

        Span<byte> head = stackalloc byte[4];
        buffer.Slice(0, head.Length).CopyTo(head);
        return head.SequenceEqual(FrameMagic);
    }

    private static string Preview(ReadOnlySequence<byte> compressed)
    {
        try
        {
            // 🆕 .NET 11 : ReadOnlySequenceStream passe les segments du PipeReader à ZstandardStream
            // (qui attend un Stream) sans les recopier dans un tableau contigu.
            using var zstd = new ZstandardStream(new ReadOnlySequenceStream(compressed), CompressionMode.Decompress);
            var bytes = new byte[PreviewLength];
            var read = zstd.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
            var text = Encoding.UTF8.GetString(bytes, 0, read).Split('\n', 2)[0];
            return string.Concat(text.Select(c => char.IsControl(c) ? '·' : c));
        }
        catch (InvalidDataException)
        {
            return "";
        }
    }
}
