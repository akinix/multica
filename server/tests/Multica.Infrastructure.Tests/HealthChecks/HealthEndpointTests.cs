using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Multica.Infrastructure.Tests.HealthChecks;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RootEndpointReturnsMulticaApi()
    {
        var response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Multica API");
    }
}
