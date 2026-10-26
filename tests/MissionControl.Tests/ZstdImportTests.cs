using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MissionControl.Telemetry;

namespace MissionControl.Tests;

public sealed class ZstdImportTests : IAsyncDisposable
{
    private const string JsonlBody = """
        {"probe":"Juno","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"temperature","value":-12.5}
        {"probe":"Juno","seq":1,"at":"2026-11-17T08:00:01+00:00","sensor":"battery","value":98}
        {"probe":"Rosetta","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"radiation","value":0.42}

        """;

    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public ZstdImportTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> Formats => [.. TelemetryFormats.All.Select(f => f.Name)];

    [Theory]
    [MemberData(nameof(Formats))]
    public async Task Zstd_import_produces_the_same_state_as_an_uncompressed_import(string formatName)
    {
        var format = TelemetryFormats.FindByName(formatName)!;
        const int count = 20_011;

        await using var plainFactory = new WebApplicationFactory<Program>();
        using var plainClient = plainFactory.CreateClient();
        var plain = await plainClient.PostAsync(
            "/telemetry/import", new TelemetryContent(format, Generate(count)), Ct);

        var compressed = await _client.PostAsync(
            "/telemetry/import", new ZstandardCompressedContent(new TelemetryContent(format, Generate(count))), Ct);

        Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
        Assert.Equal(HttpStatusCode.OK, compressed.StatusCode);
        var summary = await compressed.Content.ReadFromJsonAsync<ImportSummaryDto>(Ct);
        Assert.Equal(format.Name, summary!.Format);
        Assert.Equal(count, summary.Packets);

        Assert.Equal(await StateOf(plainClient), await StateOf(_client));
    }

    [Fact]
    public async Task Zstd_compressed_jsonl_text_feeds_the_dashboard()
    {
        // 🆕 .NET 11 : StringStream lit un texte encodé à la volée, sans tableau d'octets intermédiaire.
        var body = new StreamContent(new StringStream(JsonlBody, Encoding.UTF8));
        body.Headers.ContentType = new MediaTypeHeaderValue("application/jsonl");

        var response = await _client.PostAsync("/telemetry/import", new ZstandardCompressedContent(body), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await _client.GetFromJsonAsync<TelemetryStatusDto>("/telemetry", Ct);
        Assert.Equal(3, status!.Packets);
    }

    [Theory]
    [InlineData("lz4")]
    [InlineData("zstd, gzip")]
    public async Task Unsupported_content_encoding_is_rejected(string encoding)
    {
        var content = new ByteArrayContent(Compress(Encoding.UTF8.GetBytes(JsonlBody)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/jsonl");
        content.Headers.TryAddWithoutValidation("Content-Encoding", encoding);

        var response = await _client.PostAsync("/telemetry/import", content, Ct);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(Ct);
        Assert.Contains("zstd", problem!.Detail);
        await AssertStoreIsEmpty();
    }

    [Fact]
    public async Task Zstd_body_without_content_encoding_is_rejected_with_a_hint()
    {
        var content = new ByteArrayContent(Compress(Encoding.UTF8.GetBytes(JsonlBody)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/jsonl");

        var response = await _client.PostAsync("/telemetry/import", content, Ct);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(Ct);
        Assert.Contains("Content-Encoding: zstd", problem!.Detail);
        Assert.Contains("\"probe\":\"Juno\"", problem.Detail);
        await AssertStoreIsEmpty();
    }

    [Fact]
    public async Task Corrupted_zstd_body_is_a_bad_request()
    {
        var compressed = Compress(Encoding.UTF8.GetBytes(JsonlBody));
        var corrupted = compressed[..8].Concat(Enumerable.Repeat((byte)0xFF, 64)).ToArray();
        var content = new ByteArrayContent(corrupted);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/jsonl");
        content.Headers.ContentEncoding.Add("zstd");

        var response = await _client.PostAsync("/telemetry/import", content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>État observable via l'API, sans l'heure de réception (qui varie d'un import à l'autre).</summary>
    private static async Task<string> StateOf(HttpClient client)
    {
        var status = await client.GetFromJsonAsync<JsonElement>("/telemetry", Ct);
        var state = new StringBuilder().AppendLine($"packets={status.GetProperty("packets")}");
        foreach (var probe in status.GetProperty("probes").EnumerateArray())
        {
            var name = probe.GetProperty("name").GetString();
            state.AppendLine($"{name} {probe.GetProperty("packets")} {probe.GetProperty("firstAt")} {probe.GetProperty("lastAt")} {probe.GetProperty("lastValue").GetRawText()}");
            state.AppendLine(await client.GetStringAsync($"/telemetry/probes/{name}/launch?minutes=60", Ct));
        }
        return state.ToString();
    }

    private async Task AssertStoreIsEmpty()
    {
        var status = await _client.GetFromJsonAsync<TelemetryStatusDto>("/telemetry", Ct);
        Assert.Equal(0, status!.Packets);
    }

    private static byte[] Compress(byte[] raw)
    {
        var output = new MemoryStream();
        using (var zstd = new ZstandardStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            // 🆕 .NET 11 : ReadOnlyMemoryStream expose un ReadOnlyMemory<byte> en Stream, sans copie.
            new ReadOnlyMemoryStream(raw).CopyTo(zstd);
        }
        return output.ToArray();
    }

    private static async IAsyncEnumerable<TelemetryPacket> Generate(int count)
    {
        string[] probes = ["Juno", "Rosetta", "Cassini", "Huygens", "Galileo"];
        var start = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < count; i++)
        {
            yield return new TelemetryPacket(
                probes[i % probes.Length], i / probes.Length, start.AddSeconds(i / probes.Length), "temperature", i * 0.25);

            if (i % 5_000 == 0)
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

    private sealed record ImportSummaryDto(string Format, long Packets);

    private sealed record TelemetryStatusDto(long Packets);

    private sealed record ProblemDto(string? Title, string? Detail);
}
