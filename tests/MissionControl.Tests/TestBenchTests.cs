using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MissionControl.Telemetry;

namespace MissionControl.Tests;

public sealed class TestBenchTests : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public TestBenchTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("none")]
    [InlineData("zstd")]
    public async Task Import_response_contains_the_measurements(string compression)
    {
        var response = await _client.PostAsync("/telemetry/import", Body(TelemetryFormats.Cbor, compression), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var measurements = summary.GetProperty("measurements");
        Assert.Equal(compression, measurements.GetProperty("compression").GetString());
        string[] expected =
        [
            "networkBytes", "payloadBytes", "compressionRatio", "allocatedBytes", "peakWorkingSet", "cpuTime",
        ];
        Assert.All(expected, name => Assert.True(measurements.TryGetProperty(name, out _), $"{name} manquant"));
    }

    [Fact]
    public async Task Network_bytes_are_counted_before_decompression_and_payload_bytes_after()
    {
        var raw = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 2_000).Select(i =>
            $$"""{"probe":"Juno","seq":{{i}},"at":"2026-11-17T08:00:00+00:00","sensor":"temperature","value":1.5}""" + "\n")));
        var compressed = Compress(raw);
        var content = new ByteArrayContent(compressed);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/jsonl");
        content.Headers.ContentEncoding.Add("zstd");

        var response = await _client.PostAsync("/telemetry/import", content, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var measurements = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("measurements");
        Assert.Equal(compressed.Length, measurements.GetProperty("networkBytes").GetInt64());
        Assert.Equal(raw.Length, measurements.GetProperty("payloadBytes").GetInt64());
    }

    [Fact]
    public async Task Bench_keeps_one_row_per_format_and_compression()
    {
        await _client.PostAsync("/telemetry/import", Body(TelemetryFormats.JsonLines, "none"), Ct);
        await _client.PostAsync("/telemetry/import", Body(TelemetryFormats.JsonLines, "zstd"), Ct);

        var rows = (await _client.GetFromJsonAsync<BenchRowDto[]>("/telemetry/bench", Ct))!;

        Assert.Equal(
            ["json/none", "json/zstd", "jsonl/none", "jsonl/zstd", "cbor/none", "cbor/zstd"],
            rows.Select(r => $"{r.Format}/{r.Compression}"));
        Assert.Equal(
            ["jsonl/none", "jsonl/zstd"],
            rows.Where(r => r.Last is not null).Select(r => $"{r.Format}/{r.Compression}"));
    }

    private static byte[] Compress(byte[] raw)
    {
        var output = new MemoryStream();
        using (var zstd = new ZstandardStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zstd.Write(raw);
        }
        return output.ToArray();
    }

    private sealed record BenchRowDto(string Format, string Compression, JsonElement? Last);

    private static HttpContent Body(ITelemetryFormat format, string compression)
    {
        HttpContent content = new TelemetryContent(format, Generate(5_000));
        return compression == "zstd" ? new ZstandardCompressedContent(content) : content;
    }

    private static async IAsyncEnumerable<TelemetryPacket> Generate(int count)
    {
        var start = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < count; i++)
        {
            yield return new TelemetryPacket("Juno", i, start.AddSeconds(i), "temperature", i * 0.25);
            if (i % 1_000 == 0)
            {
                await Task.Yield();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
