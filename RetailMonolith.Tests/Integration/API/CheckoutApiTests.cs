using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RetailMonolith.Data;
using RetailMonolith.Services;
using RetailMonolith.Tests.Integration.Helpers;
using System.Net;
using System.Text.Json;

namespace RetailMonolith.Tests.Integration.API;

public class CheckoutApiTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public CheckoutApiTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostCheckout_WithValidCart_ReturnsOrder()
    {
        // Arrange - Setup cart with items
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cartService = scope.ServiceProvider.GetRequiredService<ICartService>();

        // Seed the database if needed
        await AppDbContext.SeedAsync(db);

        await cartService.AddToCartAsync("guest", productId: 1, quantity: 2);

        // Act
        var response = await _client.PostAsync("/api/checkout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CheckoutResponse>(content, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.Status.Should().Be("Paid");
        result.Total.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PostCheckout_WithEmptyCart_ReturnsError()
    {
        // Arrange - No cart setup

        // Act
        var response = await _client.PostAsync("/api/checkout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    private class CheckoutResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }
}
