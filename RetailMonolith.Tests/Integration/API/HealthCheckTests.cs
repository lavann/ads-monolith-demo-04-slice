using FluentAssertions;
using RetailMonolith.Tests.Integration.Helpers;
using System.Net;

namespace RetailMonolith.Tests.Integration.API;

public class HealthCheckTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(IntegrationTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }
}
