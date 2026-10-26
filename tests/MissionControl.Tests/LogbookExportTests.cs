using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MissionControl.Tests;

public sealed class LogbookExportTests : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public LogbookExportTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Export_writes_one_compact_json_line_per_entry()
    {
        const string body = """
            {"probe":"Rosetta","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"radiation","value":0.42}
            {"probe":"Juno","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"temperature","value":-12.5}
            {"probe":"Juno","seq":1,"at":"2026-11-17T08:00:01+00:00","sensor":"battery","value":98}

            """;
        await _client.PostAsync("/telemetry/import", new StringContent(body, Encoding.UTF8, "application/jsonl"), Ct);

        var response = await _client.GetAsync("/logbook/export", HttpCompletionOption.ResponseHeadersRead, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/jsonl", response.Content.Headers.ContentType?.MediaType);
        var jsonl = await response.Content.ReadAsStringAsync(Ct);

        Assert.EndsWith("\n", jsonl);
        Assert.DoesNotContain("\r", jsonl);
        var lines = jsonl[..^1].Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.All(lines, line =>
        {
            using var entry = JsonDocument.Parse(line);
            Assert.Equal(JsonValueKind.Object, entry.RootElement.ValueKind);
            Assert.DoesNotContain(" ", line);
        });
        Assert.Equal(
            ["Juno #0", "Juno #1", "Rosetta #0"],
            lines.Select(line => JsonSerializer.Deserialize<EntryDto>(line, JsonSerializerOptions.Web)!).Select(e => $"{e.Probe} #{e.Seq}"));
    }

    [Fact]
    public async Task Exported_logbook_can_be_imported_again()
    {
        const string body = """
            {"probe":"Juno","seq":0,"at":"2026-11-17T08:00:00+00:00","sensor":"temperature","value":-12.5}
            {"probe":"Juno","seq":1,"at":"2026-11-17T08:00:01+00:00","sensor":"battery","value":98}

            """;
        await _client.PostAsync("/telemetry/import", new StringContent(body, Encoding.UTF8, "application/jsonl"), Ct);
        var exported = await _client.GetStringAsync("/logbook/export", Ct);

        await using var other = new WebApplicationFactory<Program>();
        using var otherClient = other.CreateClient();
        await otherClient.PostAsync("/telemetry/import", new StringContent(exported, Encoding.UTF8, "application/jsonl"), Ct);

        Assert.Equal(exported, await otherClient.GetStringAsync("/logbook/export", Ct));
    }

    [Fact]
    public async Task Empty_logbook_exports_an_empty_body()
    {
        var response = await _client.GetAsync("/logbook/export", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/jsonl", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("", await response.Content.ReadAsStringAsync(Ct));
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed record EntryDto(string Probe, long Seq);
}
