using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MissionControl.Tests;

public sealed class LaunchTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Home_page_greets_the_crew_in_french()
    {
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("lang=\"fr\"", html);
        Assert.Contains("Bienvenue au centre de contrôle", html);
    }
}
