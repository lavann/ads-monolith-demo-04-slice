using FluentAssertions;
using RetailMonolith.Services;
using RetailMonolith.Tests.Unit.Helpers;

namespace RetailMonolith.Tests.Unit.Services;

public class CheckoutServiceTests
{
    [Fact]
    public async Task CheckoutAsync_SuccessfulFlow_CreatesOrderAndClearsCart()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var cartService = new CartService(context);
        var paymentGateway = new MockPaymentGateway();
        var checkoutService = new CheckoutService(context, paymentGateway);

        var customerId = "test-customer";
        await cartService.AddToCartAsync(customerId, productId: 1, quantity: 2);
        await cartService.AddToCartAsync(customerId, productId: 2, quantity: 1);

        // Act
        var order = await checkoutService.CheckoutAsync(customerId, "tok_test");

        // Assert
        order.Should().NotBeNull();
        order.Id.Should().BeGreaterThan(0);
        order.CustomerId.Should().Be(customerId);
        order.Status.Should().Be("Paid");
        order.Total.Should().Be(40.00m); // (10 * 2) + (20 * 1)
        order.Lines.Should().HaveCount(2);
        order.Lines.Should().Contain(l => l.Sku == "TEST-001" && l.Quantity == 2);
        order.Lines.Should().Contain(l => l.Sku == "TEST-002" && l.Quantity == 1);

        // Verify cart is cleared
        var cart = await cartService.GetCartWithLinesAsync(customerId);
        cart.Lines.Should().BeEmpty("cart should be cleared after checkout");
    }

    [Fact]
    public async Task CheckoutAsync_SuccessfulFlow_DecrementsInventory()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var cartService = new CartService(context);
        var paymentGateway = new MockPaymentGateway();
        var checkoutService = new CheckoutService(context, paymentGateway);

        var customerId = "test-customer";
        await cartService.AddToCartAsync(customerId, productId: 1, quantity: 3);

        var initialInventory = context.Inventory.First(i => i.Sku == "TEST-001").Quantity;

        // Act
        await checkoutService.CheckoutAsync(customerId, "tok_test");

        // Assert
        var updatedInventory = context.Inventory.First(i => i.Sku == "TEST-001");
        updatedInventory.Quantity.Should().Be(initialInventory - 3, "inventory should be decremented");
    }

    [Fact]
    public async Task CheckoutAsync_NoCart_ThrowsException()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var paymentGateway = new MockPaymentGateway();
        var checkoutService = new CheckoutService(context, paymentGateway);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await checkoutService.CheckoutAsync("non-existent-customer", "tok_test")
        );
    }

    [Fact]
    public async Task CheckoutAsync_OutOfStock_ThrowsException()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var cartService = new CartService(context);
        var paymentGateway = new MockPaymentGateway();
        var checkoutService = new CheckoutService(context, paymentGateway);

        var customerId = "test-customer";
        // Try to order more than available (available is 100)
        await cartService.AddToCartAsync(customerId, productId: 1, quantity: 150);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await checkoutService.CheckoutAsync(customerId, "tok_test")
        );
    }

    [Fact]
    public async Task CheckoutAsync_MultipleProducts_CalculatesCorrectTotal()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var cartService = new CartService(context);
        var paymentGateway = new MockPaymentGateway();
        var checkoutService = new CheckoutService(context, paymentGateway);

        var customerId = "test-customer";
        await cartService.AddToCartAsync(customerId, productId: 1, quantity: 5); // 5 * 10 = 50
        await cartService.AddToCartAsync(customerId, productId: 2, quantity: 3); // 3 * 20 = 60

        // Act
        var order = await checkoutService.CheckoutAsync(customerId, "tok_test");

        // Assert
        order.Total.Should().Be(110.00m); // 50 + 60
    }

    [Fact]
    public async Task CheckoutAsync_SuccessfulFlow_CreatesOrderLines()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var cartService = new CartService(context);
        var paymentGateway = new MockPaymentGateway();
        var checkoutService = new CheckoutService(context, paymentGateway);

        var customerId = "test-customer";
        await cartService.AddToCartAsync(customerId, productId: 1, quantity: 2);

        // Act
        var order = await checkoutService.CheckoutAsync(customerId, "tok_test");

        // Assert
        order.Lines.Should().HaveCount(1);
        var orderLine = order.Lines[0];
        orderLine.Sku.Should().Be("TEST-001");
        orderLine.Name.Should().Be("Test Product 1");
        orderLine.UnitPrice.Should().Be(10.00m);
        orderLine.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task CheckoutAsync_SuccessfulFlow_SetsCreatedDate()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateContextWithSeedDataAsync();
        var cartService = new CartService(context);
        var paymentGateway = new MockPaymentGateway();
        var checkoutService = new CheckoutService(context, paymentGateway);

        var customerId = "test-customer";
        await cartService.AddToCartAsync(customerId, productId: 1, quantity: 1);

        var beforeCheckout = DateTime.UtcNow;

        // Act
        var order = await checkoutService.CheckoutAsync(customerId, "tok_test");

        // Assert
        order.CreatedUtc.Should().BeOnOrAfter(beforeCheckout.AddSeconds(-1));
        order.CreatedUtc.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(1));
    }
}
