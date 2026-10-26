using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MissionControl.Tests;

public sealed class ProbeRegistrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registered_probe_joins_the_fleet()
    {
        var request = NewRegistration();

        var response = await _client.PostAsJsonAsync("/probes", request, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var fleet = await _client.GetFromJsonAsync<ProbeDto[]>("/probes", Ct);
        Assert.Contains(fleet!, p => p.Name == request.Name);
    }

    [Fact]
    public async Task Name_already_taken_with_another_case_is_refused_in_french()
    {
        var first = NewRegistration();
        await RegisterAsync(first);

        var errors = await PostInvalidAsync(NewRegistration() with { Name = first.Name.ToUpperInvariant() });

        Assert.Equal(["Ce nom de sonde est déjà pris."], errors["Name"]);
    }

    [Theory]
    [InlineData("Voyager")]
    [InlineData("pioneer")]
    public async Task Reserved_name_is_refused_in_french(string reservedName)
    {
        var errors = await PostInvalidAsync(NewRegistration() with { Name = reservedName });

        Assert.Equal(["Ce nom de sonde est déjà pris."], errors["Name"]);
    }

    [Fact]
    public async Task Launch_window_already_booked_at_the_ground_station_is_refused_in_french()
    {
        var first = NewRegistration();
        await RegisterAsync(first);

        var overlapping = NewRegistration() with
        {
            LaunchWindowOpensAt = first.LaunchWindowOpensAt.AddHours(1),
            LaunchWindowClosesAt = first.LaunchWindowClosesAt.AddHours(1),
        };
        var errors = await PostInvalidAsync(overlapping);

        Assert.Equal(
            [Unavailable(overlapping.LaunchWindowOpensAt.AddDays(1), overlapping.LaunchWindowClosesAt.AddDays(1))],
            errors["LaunchWindowOpensAt"]);
    }

    [Fact]
    public async Task Next_free_launch_window_skips_every_day_already_booked()
    {
        var first = NewRegistration();
        await RegisterAsync(first);
        await RegisterAsync(NewRegistration() with
        {
            LaunchWindowOpensAt = first.LaunchWindowOpensAt.AddDays(1),
            LaunchWindowClosesAt = first.LaunchWindowClosesAt.AddDays(1),
        });

        var overlapping = NewRegistration() with
        {
            LaunchWindowOpensAt = first.LaunchWindowOpensAt.AddHours(1),
            LaunchWindowClosesAt = first.LaunchWindowClosesAt.AddHours(1),
        };
        var errors = await PostInvalidAsync(overlapping);

        Assert.Equal(
            [Unavailable(overlapping.LaunchWindowOpensAt.AddDays(2), overlapping.LaunchWindowClosesAt.AddDays(2))],
            errors["LaunchWindowOpensAt"]);
    }

    [Fact]
    public async Task Two_simultaneous_registrations_cannot_book_the_same_launch_window()
    {
        var first = NewRegistration();
        var second = NewRegistration() with { LaunchWindowOpensAt = first.LaunchWindowOpensAt.AddHours(1) };

        // Les deux passent la validation async (latence simulée) avant que l'une ou l'autre ne soit enregistrée.
        var responses = await Task.WhenAll(
            _client.PostAsJsonAsync("/probes", first, Ct),
            _client.PostAsJsonAsync("/probes", second, Ct));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        var refused = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
        var problem = await refused.Content.ReadFromJsonAsync<ValidationProblemDto>(Ct);
        var errors = new Dictionary<string, string[]>(problem!.Errors, StringComparer.OrdinalIgnoreCase);
        var message = Assert.Single(errors["LaunchWindowOpensAt"]);
        Assert.StartsWith("La station sol n'est pas disponible sur cette fenêtre de lancement.", message);
    }

    private static string Unavailable(DateTime nextOpensAt, DateTime nextClosesAt) =>
        $"La station sol n'est pas disponible sur cette fenêtre de lancement. Prochaine fenêtre libre : {nextOpensAt:yyyy-MM-dd HH:mm} → {nextClosesAt:yyyy-MM-dd HH:mm}.";

    [Fact]
    public async Task Missing_name_is_refused_in_french()
    {
        var errors = await PostInvalidAsync(NewRegistration() with { Name = "" });

        Assert.Equal(["Le nom de la sonde est obligatoire."], errors["Name"]);
    }

    private async Task RegisterAsync(RegistrationDto registration)
    {
        var response = await _client.PostAsJsonAsync("/probes", registration, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<Dictionary<string, string[]>> PostInvalidAsync(RegistrationDto registration)
    {
        var response = await _client.PostAsJsonAsync("/probes", registration, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDto>(Ct);
        return new Dictionary<string, string[]>(problem!.Errors, StringComparer.OrdinalIgnoreCase);
    }

    // Chaque test réserve sa propre fenêtre, loin des autres, pour rester indépendant.
    private static int s_nextWindow;

    private static RegistrationDto NewRegistration()
    {
        var opensAt = new DateTime(2030, 1, 1).AddDays(7 * Interlocked.Increment(ref s_nextWindow));
        return new($"Sonde-{Guid.NewGuid():N}", opensAt, opensAt.AddHours(6));
    }

    private sealed record RegistrationDto(string Name, DateTime LaunchWindowOpensAt, DateTime LaunchWindowClosesAt);

    private sealed record ProbeDto(string Name, DateTime LaunchWindowOpensAt, DateTime LaunchWindowClosesAt);

    private sealed record ValidationProblemDto(Dictionary<string, string[]> Errors);
}
