using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Houston;

/// <summary>Même pipeline, sans panne, mesuré sous .NET 10 puis sous .NET 11 (un job court par runtime).</summary>
[MemoryDiagnoser]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[ShortRunJob(RuntimeMoniker.Net11_0)]
public class TelemetryPipelineBenchmarks
{
    [Benchmark]
    public Task<double> IngestBatch() => TelemetryPipeline.IngestBatchAsync(packets: 100);
}
