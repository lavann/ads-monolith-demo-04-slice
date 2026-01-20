using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RetailMonolith.Data;
using RetailMonolith.Services;
using RetailMonolith.Tests.Integration.Helpers;
using System.Net;
using System.Text.Json;

namespace RetailMonolith.Tests.Integration.Flows;

public class CompleteCheckoutFlowTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public CompleteCheckoutFlowTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CompleteFlow_AddToCart_Checkout_GetOrder_Success()
    {
        // Arrange - Setup database and services
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cartService = scope.ServiceProvider.GetRequiredService<ICartService>();

        // Seed the database
        await AppDbContext.SeedAsync(db);

        // Get initial inventory quantity
        var product = await db.Products.FirstAsync(p => p.IsActive);
        var initialInventory = await db.Inventory.FirstAsync(i => i.Sku == product.Sku);
        var initialQuantity = initialInventory.Quantity;

        // Step 1: Add items to cart
        await cartService.AddToCartAsync("guest", product.Id, quantity: 3);

        // Verify cart has items
        var cart = await cartService.GetCartWithLinesAsync("guest");
        cart.Lines.Should().HaveCount(1);
        cart.Lines[0].Quantity.Should().Be(3);

        // Step 2: Checkout
        var checkoutResponse = await _client.PostAsync("/api/checkout", null);
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var checkoutContent = await checkoutResponse.Content.ReadAsStringAsync();
        var checkoutResult = JsonSerializer.Deserialize<CheckoutResponse>(checkoutContent, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        checkoutResult.Should().NotBeNull();
        checkoutResult!.Id.Should().BeGreaterThan(0);
        checkoutResult.Status.Should().Be("Paid");

        // Step 3: Verify order was created
        var orderResponse = await _client.GetAsync($"/api/orders/{checkoutResult.Id}");
        orderResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var orderContent = await orderResponse.Content.ReadAsStringAsync();
        var order = JsonSerializer.Deserialize<OrderResponse>(orderContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        order.Should().NotBeNull();
        order!.Id.Should().Be(checkoutResult.Id);
        order.Status.Should().Be("Paid");
        order.Lines.Should().HaveCount(1);
        order.Lines[0].Quantity.Should().Be(3);

        // Step 4: Verify cart was cleared
        var clearedCart = await cartService.GetCartWithLinesAsync("guest");
        clearedCart.Lines.Should().BeEmpty("cart should be cleared after checkout");

        // Step 5: Verify inventory was decremented
        var updatedInventory = await db.Inventory.FirstAsync(i => i.Sku == product.Sku);
        updatedInventory.Quantity.Should().Be(initialQuantity - 3, "inventory should be decremented by order quantity");
    }

    [Fact]
    public async Task CompleteFlow_MultipleProducts_CheckoutSuccess()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cartService = scope.ServiceProvider.GetRequiredService<ICartService>();

        await AppDbContext.SeedAsync(db);

        var products = await db.Products.Where(p => p.IsActive).Take(2).ToListAsync();
        products.Should().HaveCountGreaterOrEqualTo(2);

        // Step 1: Add multiple products to cart
        await cartService.AddToCartAsync("guest", products[0].Id, quantity: 2);
        await cartService.AddToCartAsync("guest", products[1].Id, quantity: 1);

        // Step 2: Checkout
        var checkoutResponse = await _client.PostAsync("/api/checkout", null);
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var checkoutContent = await checkoutResponse.Content.ReadAsStringAsync();
        var checkoutResult = JsonSerializer.Deserialize<CheckoutResponse>(checkoutContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Step 3: Verify order has all products
        var orderResponse = await _client.GetAsync($"/api/orders/{checkoutResult!.Id}");
        var orderContent = await orderResponse.Content.ReadAsStringAsync();
        var order = JsonSerializer.Deserialize<OrderResponse>(orderContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        order!.Lines.Should().HaveCount(2);
        order.Lines.Should().Contain(l => l.Quantity == 2);
        order.Lines.Should().Contain(l => l.Quantity == 1);
    }

    private class CheckoutResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
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
