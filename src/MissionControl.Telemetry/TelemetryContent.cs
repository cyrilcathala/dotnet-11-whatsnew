using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;

namespace MissionControl.Telemetry;

/// <summary>
/// Body HTTP qui sérialise les paquets au moment de l'envoi, au fil de l'eau :
/// rien n'est préparé en mémoire, la taille n'est pas connue à l'avance (transfert par morceaux).
/// </summary>
public sealed class TelemetryContent : HttpContent
{
    private readonly ITelemetryFormat _format;
    private readonly IAsyncEnumerable<TelemetryPacket> _packets;

    public TelemetryContent(ITelemetryFormat format, IAsyncEnumerable<TelemetryPacket> packets)
    {
        _format = format;
        _packets = packets;
        Headers.ContentType = new MediaTypeHeaderValue(format.MediaType);
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        var writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
        await _format.WriteAsync(writer, _packets, cancellationToken);
        await writer.CompleteAsync();
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
