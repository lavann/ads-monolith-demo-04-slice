using FluentAssertions;
using RetailMonolith.Services;
using RetailMonolith.Tests.Unit.Helpers;

namespace RetailMonolith.Tests.Unit.Services;

public class CartServiceTests
{
    [Fact]
    public async Task AddToCartAsync_NewCart_CreatesCartAndAddsProduct()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var service = new CartService(context);
        var customerId = "test-customer";

        // Act
        await service.AddToCartAsync(customerId, productId: 1, quantity: 2);

        // Assert
        var cart = await service.GetCartWithLinesAsync(customerId);
        cart.Should().NotBeNull();
        cart.CustomerId.Should().Be(customerId);
        cart.Lines.Should().HaveCount(1);
        cart.Lines[0].Sku.Should().Be("TEST-001");
        cart.Lines[0].Name.Should().Be("Test Product 1");
        cart.Lines[0].UnitPrice.Should().Be(10.00m);
        cart.Lines[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task AddToCartAsync_ExistingCart_AddsNewProduct()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var service = new CartService(context);
        var customerId = "test-customer";

        // Add first product
        await service.AddToCartAsync(customerId, productId: 1, quantity: 1);

        // Act - Add second product
        await service.AddToCartAsync(customerId, productId: 2, quantity: 3);

        // Assert
        var cart = await service.GetCartWithLinesAsync(customerId);
        cart.Lines.Should().HaveCount(2);
        cart.Lines.Should().Contain(l => l.Sku == "TEST-001" && l.Quantity == 1);
        cart.Lines.Should().Contain(l => l.Sku == "TEST-002" && l.Quantity == 3);
    }

    [Fact]
    public async Task AddToCartAsync_DuplicateProduct_IncrementsQuantity()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var service = new CartService(context);
        var customerId = "test-customer";

        // Add product first time
        await service.AddToCartAsync(customerId, productId: 1, quantity: 2);

        // Act - Add same product again
        await service.AddToCartAsync(customerId, productId: 1, quantity: 3);

        // Assert
        var cart = await service.GetCartWithLinesAsync(customerId);
        cart.Lines.Should().HaveCount(1, "duplicate products should be combined");
        cart.Lines[0].Sku.Should().Be("TEST-001");
        cart.Lines[0].Quantity.Should().Be(5, "quantities should be summed (2 + 3)");
    }

    [Fact]
    public async Task AddToCartAsync_InvalidProductId_ThrowsException()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var service = new CartService(context);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.AddToCartAsync("test-customer", productId: 999, quantity: 1)
        );
    }

    [Fact]
    public async Task GetCartWithLinesAsync_NoCart_ReturnsEmptyCart()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var service = new CartService(context);

        // Act
        var cart = await service.GetCartWithLinesAsync("non-existent-customer");

        // Assert
        cart.Should().NotBeNull();
        cart.CustomerId.Should().Be("non-existent-customer");
        cart.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCartWithLinesAsync_ExistingCart_ReturnsCartWithLines()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var service = new CartService(context);
        var customerId = "test-customer";
        await service.AddToCartAsync(customerId, productId: 1, quantity: 2);
        await service.AddToCartAsync(customerId, productId: 2, quantity: 1);

        // Act
        var cart = await service.GetCartWithLinesAsync(customerId);

        // Assert
        cart.Should().NotBeNull();
        cart.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task ClearCartAsync_ExistingCart_RemovesCart()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var service = new CartService(context);
        var customerId = "test-customer";
        await service.AddToCartAsync(customerId, productId: 1, quantity: 2);

        // Act
        await service.ClearCartAsync(customerId);

        // Assert
        var cart = await service.GetCartWithLinesAsync(customerId);
        cart.Lines.Should().BeEmpty("cart should be cleared");
    }

    [Fact]
    public async Task ClearCartAsync_NoCart_DoesNotThrow()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var service = new CartService(context);

        // Act & Assert - Should not throw
        await service.ClearCartAsync("non-existent-customer");
    }

    [Fact]
    public async Task GetOrCreateCartAsync_NoCart_CreatesNewCart()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var service = new CartService(context);
        var customerId = "new-customer";

        // Act
        var cart = await service.GetOrCreateCartAsync(customerId);

        // Assert
        cart.Should().NotBeNull();
        cart.CustomerId.Should().Be(customerId);
        cart.Id.Should().BeGreaterThan(0, "cart should be saved to database");
    }

    [Fact]
    public async Task GetOrCreateCartAsync_ExistingCart_ReturnsExistingCart()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var service = new CartService(context);
        var customerId = "test-customer";
        var firstCart = await service.GetOrCreateCartAsync(customerId);

        // Act
        var secondCart = await service.GetOrCreateCartAsync(customerId);

        // Assert
        secondCart.Id.Should().Be(firstCart.Id, "should return the same cart");
    }
}
