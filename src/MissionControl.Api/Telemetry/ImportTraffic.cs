namespace MissionControl.Api.Telemetry;

/// <summary>
/// Ce qui est arrivé sur le réseau pour un import : l'encodage annoncé et, si le body est compressé,
/// le compteur des octets bruts. Posé dans les features de la requête par <see cref="ImportTrafficMiddleware"/>.
/// </summary>
public sealed class ImportTraffic(string compression, CountingStream? compressedBody)
{
    public static ImportTraffic Uncompressed { get; } = new("none", null);

    public string Compression { get; } = compression;

    /// <summary>Octets reçus, avant décompression. Sans compression, le body arrive tel quel : <c>null</c>.</summary>
    public long? NetworkBytes => compressedBody?.BytesRead;
}

public static class ImportTrafficMiddleware
{
    private const string ImportPath = "/telemetry/import";

    /// <summary>
    /// Compte les octets du body tels qu'ils arrivent du réseau. À placer AVANT <c>UseRequestDecompression</c> :
    /// le middleware de décompression remplace le body et retire l'en-tête <c>Content-Encoding</c>.
    /// </summary>
    public static IApplicationBuilder UseImportTraffic(this IApplicationBuilder app)
        => app.Use(async (context, next) =>
        {
            var request = context.Request;
            if (HttpMethods.IsPost(request.Method) && request.Path.Equals(ImportPath, StringComparison.OrdinalIgnoreCase))
            {
                var traffic = ImportTraffic.Uncompressed;
                if (request.Headers.ContentEncoding.ToString() is { Length: > 0 } encoding)
                {
                    // Seul un body compressé est enveloppé : sinon le body Kestrel reste intact (et son PipeReader natif).
                    var counter = new CountingStream(request.Body);
                    request.Body = counter;
                    traffic = new ImportTraffic(encoding, counter);
                }
                context.Features.Set(traffic);
            }

            await next(context);
        });
}

/// <summary>Flux en lecture seule qui compte les octets lus.</summary>
public sealed class CountingStream(Stream inner) : Stream
{
    private long _bytesRead;

    public long BytesRead => Interlocked.Read(ref _bytesRead);

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

    public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => Count(await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => Count(await inner.ReadAsync(buffer, cancellationToken));

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int Count(int read)
    {
        Interlocked.Add(ref _bytesRead, read);
        return read;
    }
}
