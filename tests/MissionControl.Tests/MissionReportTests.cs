using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MissionControl.Api.Commands;
using MissionControl.Api.Report;

namespace MissionControl.Tests;

public sealed class MissionReportTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Report_reconciles_commands_with_acknowledgements_in_three_categories()
    {
        var probe = await RegisterProbeAsync();
        var photo = await SendAsync(new CapturePhoto(Guid.Empty, probe, "Europe"));
        var refusedThrust = await SendAsync(new Thrust(Guid.Empty, probe, DeltaV: 900));
        var slowRotation = await SendAsync(new Rotate(Guid.Empty, probe, Attitude: 170));

        var report = await GetReportAsync(probe);

        // Appariées : la sonde a répondu, y compris par un refus.
        var matched = report.Where(l => l.Category == ReportCategory.Matched).ToArray();
        Assert.Equal([photo.CommandId, refusedThrust.CommandId], matched.Select(l => l.Command!.Id));
        Assert.Equal(photo, matched[0].Acknowledgement);
        Assert.Equal(refusedThrust, matched[1].Acknowledgement);

        // Sans accusé : la rotation trop lente n'a jamais été acquittée.
        var unacknowledged = Assert.Single(report, l => l.Category == ReportCategory.Unacknowledged);
        Assert.Equal(new Rotate(slowRotation.CommandId, probe, 170), unacknowledged.Command);
        Assert.Null(unacknowledged.Acknowledgement);

        // Orphelin : au premier contact, la sonde se signale sans qu'on lui ait rien demandé.
        var orphan = Assert.Single(report, l => l.Category == ReportCategory.Orphan);
        Assert.Null(orphan.Command);
        var signal = Assert.IsType<Acknowledged>(orphan.Acknowledgement!.Value.Value);
        Assert.DoesNotContain(report, l => l.Command?.Id == signal.CommandId);
    }

    [Fact]
    public async Task Commands_to_a_lost_probe_have_no_acknowledgement()
    {
        var probe = await RegisterProbeAsync();
        await SendAsync(new SelfDestruct(Guid.Empty, probe, probe));
        var unanswered = await SendAsync(new Thrust(Guid.Empty, probe, DeltaV: 1));

        var report = await GetReportAsync(probe);

        var line = Assert.Single(report, l => l.Command?.Id == unanswered.CommandId);
        Assert.Equal(ReportCategory.Unacknowledged, line.Category);
    }

    [Fact]
    public async Task Probe_signals_itself_only_once()
    {
        var probe = await RegisterProbeAsync();
        await SendAsync(new CapturePhoto(Guid.Empty, probe, "Io"));
        await SendAsync(new CapturePhoto(Guid.Empty, probe, "Callisto"));

        var report = await GetReportAsync(probe);

        Assert.Single(report, l => l.Category == ReportCategory.Orphan);
    }

    [Fact]
    public async Task Report_categories_are_written_in_plain_words()
    {
        var probe = await RegisterProbeAsync();
        await SendAsync(new Rotate(Guid.Empty, probe, Attitude: 170));

        var json = await _client.GetStringAsync("/mission/report", Ct);

        Assert.Contains("\"category\":\"matched\"", json);
        Assert.Contains("\"category\":\"unacknowledged\"", json);
        Assert.Contains("\"category\":\"orphan\"", json);
    }

    private async Task<ReportLine[]> GetReportAsync(string probe)
    {
        var report = await _client.GetFromJsonAsync<ReportLine[]>("/mission/report", Ct);
        return [.. report!.Where(l => l.ProbeName == probe)];
    }

    private async Task<CommandResult> SendAsync(ProbeCommand command)
    {
        var response = await _client.PostAsJsonAsync("/commands", command, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<CommandResult>(Ct);
    }

    // Chaque test enregistre sa propre sonde, sur sa propre fenêtre de lancement.
    private static int s_nextWindow;

    private async Task<string> RegisterProbeAsync()
    {
        var name = $"Sonde-{Guid.NewGuid():N}";
        var opensAt = new DateTime(2041, 1, 1).AddDays(7 * Interlocked.Increment(ref s_nextWindow));

        var response = await _client.PostAsJsonAsync("/probes", new { name, LaunchWindowOpensAt = opensAt, LaunchWindowClosesAt = opensAt.AddHours(6) }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return name;
    }
}
