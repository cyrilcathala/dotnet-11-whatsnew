using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MissionControl.Telemetry;

namespace MissionControl.Api.Logbook;

/// <summary>Où la station range son journal de bord (section <c>Logbook</c> de la configuration).</summary>
public sealed class LogbookOptions
{
    /// <summary>
    /// Dossier racine. Chaque instance de l'application y crée son propre sous-dossier,
    /// supprimé à l'arrêt. Par défaut : le dossier temporaire du système.
    /// </summary>
    public string? Directory { get; set; }
}

/// <summary>
/// Journal de bord : tous les paquets reçus par import, dans l'ordre de réception, ajoutés lot par lot
/// à un fichier JSON Lines sur disque (append-only). Un export peut être réimporté tel quel
/// par <c>POST /telemetry/import</c>. La mémoire ne dépend pas de la taille du journal.
/// </summary>
public sealed class CrewLogbook : IDisposable
{
    private readonly string _directory;
    private readonly string _path;
    private readonly FileStream _file;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ArrayBufferWriter<byte> _buffer = new(64 * 1024);
    private readonly Utf8JsonWriter _json;

    // Longueur des lignes complètes déjà écrites : un export ne lit jamais au-delà.
    private long _length;

    public CrewLogbook(IOptions<LogbookOptions> options)
    {
        var root = options.Value.Directory is { Length: > 0 } configured ? configured : Path.GetTempPath();
        _directory = Path.Combine(root, "mission-control", $"logbook-{Environment.ProcessId}-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "journal-de-bord.jsonl");

        _file = new FileStream(_path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.Read | FileShare.Delete,
            Options = FileOptions.Asynchronous,
        });
        _json = new Utf8JsonWriter(_buffer);
    }

    /// <summary>Ajoute un lot de paquets en fin de journal (une seule écriture disque par lot).</summary>
    public async Task AppendAsync(IReadOnlyList<TelemetryPacket> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            _buffer.ResetWrittenCount();
            foreach (var packet in batch)
            {
                JsonSerializer.Serialize(_json, packet, TelemetryJsonContext.Default.TelemetryPacket);
                _json.Flush();
                _json.Reset();
                _buffer.Write("\n"u8);
            }

            // Pas d'annulation en cours d'écriture : une ligne à moitié écrite corromprait le journal.
            await _file.WriteAsync(_buffer.WrittenMemory, CancellationToken.None);
            await _file.FlushAsync(CancellationToken.None);
            Volatile.Write(ref _length, _file.Position);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Relit le journal au fil de l'eau, tel qu'il était au début de la lecture :
    /// les lots ajoutés pendant un export n'y figurent pas, aucune ligne n'est coupée.
    /// </summary>
    public async IAsyncEnumerable<TelemetryPacket> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var length = Volatile.Read(ref _length);
        if (length == 0)
        {
            yield break;
        }

        await using var file = new FileStream(_path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite | FileShare.Delete,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        });
        await using var snapshot = new BoundedStream(file, length);

        // 🔁 Rappel .NET 10 : lecture valeur par valeur d'un flux JSON Lines (topLevelValues).
        var packets = JsonSerializer.DeserializeAsyncEnumerable(
            snapshot, TelemetryJsonContext.Default.TelemetryPacket, topLevelValues: true, cancellationToken);

        await foreach (var packet in packets)
        {
            if (packet is not null)
            {
                yield return packet;
            }
        }
    }

    public void Dispose()
    {
        _json.Dispose();
        _file.Dispose();
        _gate.Dispose();
        try
        {
            System.IO.Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Un export encore en cours garde le fichier ouvert : le système de fichiers le libérera.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Lecture seule, limitée aux <paramref name="length"/> premiers octets du flux.</summary>
    private sealed class BoundedStream(Stream inner, long length) : Stream
    {
        private long _remaining = length;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer[..Limit(buffer.Length)]);
            _remaining -= read;
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer[..Limit(buffer.Length)], cancellationToken);
            _remaining -= read;
            return read;
        }

        private int Limit(int requested) => (int)Math.Min(requested, _remaining);

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
