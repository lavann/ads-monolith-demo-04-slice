using FluentAssertions;
using RetailMonolith.Services;

namespace RetailMonolith.Tests.Unit.Services;

public class MockPaymentGatewayTests
{
    [Fact]
    public async Task ChargeAsync_AlwaysSucceeds()
    {
        // Arrange
        var gateway = new MockPaymentGateway();
        var request = new PaymentRequest(100.00m, "GBP", "tok_test");

        // Act
        var result = await gateway.ChargeAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ChargeAsync_GeneratesMockReference()
    {
        // Arrange
        var gateway = new MockPaymentGateway();
        var request = new PaymentRequest(50.00m, "GBP", "tok_test");

        // Act
        var result = await gateway.ChargeAsync(request, CancellationToken.None);

        // Assert
        result.ProviderRef.Should().NotBeNullOrEmpty();
        result.ProviderRef.Should().StartWith("MOCK-");
    }

    [Fact]
    public async Task ChargeAsync_DifferentAmounts_AllSucceed()
    {
        // Arrange
        var gateway = new MockPaymentGateway();

        // Act & Assert
        var result1 = await gateway.ChargeAsync(new PaymentRequest(10.00m, "GBP", "tok_test"), CancellationToken.None);
        result1.Succeeded.Should().BeTrue();

        var result2 = await gateway.ChargeAsync(new PaymentRequest(1000.00m, "USD", "tok_test"), CancellationToken.None);
        result2.Succeeded.Should().BeTrue();

        var result3 = await gateway.ChargeAsync(new PaymentRequest(0.01m, "EUR", "tok_test"), CancellationToken.None);
        result3.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ChargeAsync_GeneratesUniqueReferences()
    {
        // Arrange
        var gateway = new MockPaymentGateway();
        var request = new PaymentRequest(100.00m, "GBP", "tok_test");

        // Act
        var result1 = await gateway.ChargeAsync(request, CancellationToken.None);
        var result2 = await gateway.ChargeAsync(request, CancellationToken.None);

        // Assert
        result1.ProviderRef.Should().NotBe(result2.ProviderRef, "each charge should have unique reference");
    }
}
