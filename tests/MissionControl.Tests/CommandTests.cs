using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MissionControl.Api.Commands;

namespace MissionControl.Tests;

public sealed class CommandTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Moderate_thrust_is_acknowledged()
    {
        var probe = await RegisterProbeAsync();

        var result = await SendAsync(new Thrust(Guid.Empty, probe, DeltaV: 12.5));

        var acknowledged = Assert.IsType<Acknowledged>(result.Value);
        Assert.NotEqual(Guid.Empty, acknowledged.CommandId);
    }

    [Fact]
    public async Task Thrust_beyond_the_fuel_reserve_is_rejected_in_french()
    {
        var probe = await RegisterProbeAsync();

        var result = await SendAsync(new Thrust(Guid.Empty, probe, DeltaV: 500));

        var rejected = Assert.IsType<Rejected>(result.Value);
        Assert.Equal("Poussée supérieure à la réserve de carburant (100 m/s).", rejected.Reason);
    }

    [Fact]
    public async Task Wide_rotation_times_out()
    {
        var probe = await RegisterProbeAsync();

        var result = await SendAsync(new Rotate(Guid.Empty, probe, Attitude: 170));

        var timedOut = Assert.IsType<TimedOut>(result.Value);
        Assert.Equal(TimeSpan.FromSeconds(30), timedOut.After);
    }

    [Fact]
    public async Task Every_command_kind_can_be_sent_to_a_registered_probe()
    {
        var probe = await RegisterProbeAsync();

        ProbeCommand[] commands =
        [
            new Thrust(Guid.Empty, probe, DeltaV: 3),
            new Rotate(Guid.Empty, probe, Attitude: 45),
            new CapturePhoto(Guid.Empty, probe, Target: "Europe"),
            new SelfDestruct(Guid.Empty, probe, ConfirmationCode: probe),
        ];

        foreach (var command in commands)
        {
            Assert.IsType<Acknowledged>((await SendAsync(command)).Value);
        }
    }

    [Fact]
    public async Task Self_destruct_with_a_wrong_confirmation_code_is_rejected()
    {
        var probe = await RegisterProbeAsync();

        var result = await SendAsync(new SelfDestruct(Guid.Empty, probe, ConfirmationCode: "oups"));

        Assert.IsType<Rejected>(result.Value);
    }

    [Fact]
    public async Task Probe_is_lost_after_its_self_destruction()
    {
        var probe = await RegisterProbeAsync();
        var farewell = Assert.IsType<Acknowledged>((await SendAsync(new SelfDestruct(Guid.Empty, probe, probe))).Value);

        var result = await SendAsync(new CapturePhoto(Guid.Empty, probe, Target: "Débris"));

        var lost = Assert.IsType<ProbeLost>(result.Value);
        Assert.Equal(farewell.ProbeTime, lost.LastContact);
    }

    [Fact]
    public async Task Command_to_an_unknown_probe_is_refused_in_french()
    {
        var response = await PostCommandAsync(new Thrust(Guid.Empty, "Sonde-fantôme", DeltaV: 1));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(Ct);
        Assert.Equal("Sonde inconnue : « Sonde-fantôme ».", problem!.Detail);
    }

    [Fact]
    public async Task A_command_id_already_used_is_refused_in_french()
    {
        var probe = await RegisterProbeAsync();
        var id = Guid.CreateVersion7();
        Assert.IsType<Acknowledged>((await SendAsync(new Thrust(id, probe, DeltaV: 5))).Value);

        var response = await PostCommandAsync(new Rotate(id, probe, Attitude: 10));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(Ct);
        Assert.Equal($"Une commande d'identifiant {id} a déjà été envoyée.", problem!.Detail);
        var history = await _client.GetFromJsonAsync<CommandLogEntry[]>("/commands", Ct);
        Assert.Single(history!, e => e.Command.Id == id);
    }

    [Fact]
    public async Task Command_result_json_has_neither_envelope_nor_type_discriminator()
    {
        var probe = await RegisterProbeAsync();

        var response = await PostCommandAsync(new Thrust(Guid.Empty, probe, DeltaV: 500));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(
            ["commandId", "reason"],
            json.RootElement.EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Sent_commands_and_their_results_are_kept_in_the_history()
    {
        var probe = await RegisterProbeAsync();
        var acknowledged = Assert.IsType<Acknowledged>((await SendAsync(new CapturePhoto(Guid.Empty, probe, "Titan"))).Value);
        var rejected = Assert.IsType<Rejected>((await SendAsync(new Thrust(Guid.Empty, probe, DeltaV: 900))).Value);

        var history = await _client.GetFromJsonAsync<CommandLogEntry[]>("/commands", Ct);

        var photo = Assert.Single(history!, e => e.Command.Id == acknowledged.CommandId);
        Assert.Equal(new CapturePhoto(acknowledged.CommandId, probe, "Titan"), photo.Command);
        Assert.Equal(acknowledged, photo.Result.Value);
        var thrust = Assert.Single(history!, e => e.Command.Id == rejected.CommandId);
        Assert.Equal(rejected, thrust.Result.Value);
    }

    [Fact]
    public async Task Openapi_describes_the_command_result_as_any_of_its_cases()
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json", Ct));

        var response = document.RootElement
            .GetProperty("paths").GetProperty("/commands").GetProperty("post")
            .GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal("#/components/schemas/CommandResult", response.GetProperty("$ref").GetString());

        var cases = document.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("CommandResult")
            .GetProperty("anyOf").EnumerateArray()
            .Select(c => c.GetProperty("$ref").GetString());
        Assert.Equal(
            [
                "#/components/schemas/Acknowledged", "#/components/schemas/Rejected",
                "#/components/schemas/TimedOut", "#/components/schemas/ProbeLost",
            ],
            cases);
    }

    private async Task<CommandResult> SendAsync(ProbeCommand command)
    {
        var response = await PostCommandAsync(command);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Aller-retour côté client : le JSON nu est reclassé dans le bon cas de l'union.
        return await response.Content.ReadFromJsonAsync<CommandResult>(Ct);
    }

    private Task<HttpResponseMessage> PostCommandAsync(ProbeCommand command) =>
        _client.PostAsJsonAsync("/commands", command, Ct);

    // Chaque test enregistre sa propre sonde, sur sa propre fenêtre de lancement.
    private static int s_nextWindow;

    private async Task<string> RegisterProbeAsync()
    {
        var name = $"Sonde-{Guid.NewGuid():N}";
        var opensAt = new DateTime(2040, 1, 1).AddDays(7 * Interlocked.Increment(ref s_nextWindow));

        var response = await _client.PostAsJsonAsync("/probes", new { name, LaunchWindowOpensAt = opensAt, LaunchWindowClosesAt = opensAt.AddHours(6) }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return name;
    }

    private sealed record ProblemDto(string? Detail);
}
