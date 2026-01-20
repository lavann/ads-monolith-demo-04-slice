using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RetailMonolith.Data;
using RetailMonolith.Models;
using RetailMonolith.Tests.Integration.Helpers;
using System.Net;
using System.Text.Json;

namespace RetailMonolith.Tests.Integration.API;

public class OrdersApiTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public OrdersApiTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetOrder_ExistingOrder_ReturnsOrder()
    {
        // Arrange - Create an order in the database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = new Order
        {
            CustomerId = "test-customer",
            Status = "Paid",
            Total = 100.00m,
            Lines = new List<OrderLine>
            {
                new OrderLine
                {
                    Sku = "TEST-001",
                    Name = "Test Product",
                    UnitPrice = 50.00m,
                    Quantity = 2
                }
            }
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/orders/{order.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<OrderResponse>(content, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
        result.CustomerId.Should().Be("test-customer");
        result.Status.Should().Be("Paid");
        result.Total.Should().Be(100.00m);
        result.Lines.Should().HaveCount(1);
        result.Lines[0].Sku.Should().Be("TEST-001");
    }

    [Fact]
    public async Task GetOrder_NonExistentOrder_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/orders/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private class OrderResponse
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public List<OrderLineResponse> Lines { get; set; } = new();
    }

    private class OrderLineResponse
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }
}
