using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using MissionControl.Telemetry;

namespace MissionControl.Api.Telemetry;

/// <summary>
/// Décorateur du banc d'essai : mesure chaque import et ajoute les mesures au résumé.
/// Les compteurs (allocations, CPU, working set) portent sur tout le processus : les imports du banc sont séquentiels.
/// </summary>
public sealed class InstrumentedTelemetryImporter(
    ITelemetryImporter inner, IHttpContextAccessor http, TestBench bench) : ITelemetryImporter
{
    public async Task<ImportSummary> ImportAsync(PipeReader body, ITelemetryFormat format, CancellationToken cancellationToken)
    {
        var traffic = http.HttpContext?.Features.Get<ImportTraffic>() ?? ImportTraffic.Uncompressed;
        var payload = new CountingPipeReader(body);

        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var cpuBefore = ProcessorTime();
        var workingSet = PeakWorkingSet.StartSampling();

        var summary = await inner.ImportAsync(payload, format, cancellationToken);

        var peakWorkingSet = await workingSet.StopAsync();
        var cpu = ProcessorTime() - cpuBefore;
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;

        var payloadBytes = payload.BytesRead;
        var networkBytes = traffic.NetworkBytes ?? payloadBytes;
        var measured = summary with
        {
            Measurements = new ImportMeasurements(
                traffic.Compression,
                networkBytes,
                payloadBytes,
                payloadBytes > 0 ? (double)networkBytes / payloadBytes : 1,
                allocated,
                peakWorkingSet,
                cpu),
        };

        bench.Record(measured);
        return measured;
    }

    private static TimeSpan ProcessorTime()
    {
        using var process = Process.GetCurrentProcess();
        return process.TotalProcessorTime;
    }

    /// <summary>Pic de working set, échantillonné périodiquement pendant l'import.</summary>
    private sealed class PeakWorkingSet
    {
        private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(20);

        private readonly CancellationTokenSource _stop = new();
        private long _peak = Environment.WorkingSet;
        private Task _sampling = Task.CompletedTask;

        public static PeakWorkingSet StartSampling()
        {
            var sampler = new PeakWorkingSet();
            sampler._sampling = sampler.SampleAsync();
            return sampler;
        }

        public async Task<long> StopAsync()
        {
            await _stop.CancelAsync();
            await _sampling;
            _stop.Dispose();
            Sample();
            return _peak;
        }

        private async Task SampleAsync()
        {
            using var timer = new PeriodicTimer(Period);
            try
            {
                while (await timer.WaitForNextTickAsync(_stop.Token))
                {
                    Sample();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void Sample() => _peak = Math.Max(_peak, Environment.WorkingSet);
    }

    /// <summary>Compte les octets du payload (après décompression) livrés au lecteur de format.</summary>
    private sealed class CountingPipeReader(PipeReader inner) : PipeReader
    {
        private ReadOnlySequence<byte> _buffer;
        private long _unconsumed;

        public long BytesRead { get; private set; }

        public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
            => Remember(await inner.ReadAsync(cancellationToken));

        public override bool TryRead(out ReadResult result)
        {
            if (!inner.TryRead(out result))
            {
                return false;
            }
            Remember(result);
            return true;
        }

        public override void AdvanceTo(SequencePosition consumed) => AdvanceTo(consumed, consumed);

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            // Ce qui n'est pas consommé sera relivré par la lecture suivante : on ne le recompte pas.
            _unconsumed = _buffer.Slice(consumed).Length;
            inner.AdvanceTo(consumed, examined);
        }

        public override void CancelPendingRead() => inner.CancelPendingRead();

        public override void Complete(Exception? exception = null) => inner.Complete(exception);

        private ReadResult Remember(ReadResult result)
        {
            _buffer = result.Buffer;
            BytesRead += result.Buffer.Length - _unconsumed;
            return result;
        }
    }
}
