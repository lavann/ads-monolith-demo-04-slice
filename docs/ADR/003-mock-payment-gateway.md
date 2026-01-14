# ADR-003: Mock Payment Gateway for Development

## Status
Accepted

## Context
The checkout process requires integration with a payment provider to charge customers. Real payment gateways (Stripe, PayPal, Square, etc.) require:
- API credentials and keys
- Sandbox/test environments
- PCI compliance considerations
- Network connectivity to external services
- Handling of webhooks and callbacks

For a demo/development application, we need to simulate payment processing without external dependencies.

## Decision
Implement a **mock payment gateway** that always succeeds, wrapped behind an `IPaymentGateway` interface for future substitution.

**Interface** (`IPaymentGateway.cs`):
```csharp
public record PaymentRequest(decimal Amount, string Currency, string Token);
public record PaymentResult(bool Succeeded, string? ProviderRef, string? Error);

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest req, CancellationToken ct);
}
```

**Implementation** (`MockPaymentGateway.cs`):
```csharp
public class MockPaymentGateway : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(PaymentRequest req, CancellationToken ct)
    {
        // Always succeeds with mock reference
        return Task.FromResult(new PaymentResult(
            Succeeded: true, 
            ProviderRef: $"MOCK-{Guid.NewGuid():N}", 
            Error: null
        ));
    }
}
```

**Registration** (`Program.cs`):
```csharp
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();
```

## Consequences

### Positive
- **No external dependencies**: Application runs offline
- **No credentials required**: No API keys to manage
- **Fast**: No network latency
- **Deterministic**: Predictable behavior for testing
- **Interface abstraction**: Easy to swap for real implementation
- **Development velocity**: Developers can iterate quickly without payment provider account

### Negative
- **Not realistic**: Doesn't test real payment failures
- **Limited scenarios**: Cannot test timeout, retry, or partial authorization
- **No validation**: Accepts any token, amount, currency
- **Production risk**: Must remember to replace before deployment
- **No failure path testing**: "Failed" order status is unreachable with current code

### Risks

#### Forgotten Replacement
If `MockPaymentGateway` is deployed to production:
- All payments will appear successful in application
- No actual charges will occur
- Orders will be fulfilled without payment
- **Mitigation**: Environment-based configuration, deployment checklist

#### Hardcoded Token
Current usage in minimal API:
```csharp
app.MapPost("/api/checkout", async (ICheckoutService svc) =>
{
    var order = await svc.CheckoutAsync("guest", "tok_test");
    // ...
});
```
**Issue**: Token `"tok_test"` hardcoded, not captured from payment form

## Alternatives Considered

#### Use Real Payment Gateway in Sandbox Mode
- **Pros**: Tests actual integration, realistic behavior
- **Cons**: Requires credentials, network dependency, slower, more complex
- **Why rejected**: Unnecessary complexity for demo application

#### Stripe Mock Server
- **Pros**: Realistic Stripe API simulation
- **Cons**: Additional dependency, still requires local server
- **Why rejected**: Over-engineering for current needs

#### Conditional Implementation Based on Environment
```csharp
if (app.Environment.IsDevelopment())
    builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();
else
    builder.Services.AddScoped<IPaymentGateway, StripePaymentGateway>();
```
- **Pros**: Automatic switching
- **Cons**: Production code in dev branch, configuration complexity
- **Why rejected**: Keep it simple for demo; decide later

#### No Payment Abstraction
- **Pros**: Fewer lines of code
- **Cons**: Checkout service directly coupled to payment provider
- **Why rejected**: Hinders testability and future flexibility

## Future Considerations

### When to Replace Mock Implementation

1. **Before production deployment**: Absolutely required
2. **When testing failure scenarios**: Need realistic error handling
3. **When implementing refunds**: Mock doesn't support reversals
4. **When compliance matters**: PCI-DSS requires real validation

### Real Implementation Requirements

A production `IPaymentGateway` implementation should:
- Validate payment tokens
- Handle network failures with retries
- Support idempotency (prevent duplicate charges)
- Return specific error codes
- Support refunds and voids
- Log all payment attempts for audit
- Handle webhooks for async payment confirmation

### Testing Strategy

Even with mock gateway, tests should verify:
- Checkout service handles successful payment
- Order status set to "Paid" on success
- Order status set to "Failed" on failure (requires mock enhancement)
- Inventory reserved before payment
- Cart cleared only after successful payment

## Enhancement Opportunities

### Add Failure Simulation
```csharp
public class MockPaymentGateway : IPaymentGateway
{
    private readonly Random _random = new();
    
    public Task<PaymentResult> ChargeAsync(PaymentRequest req, CancellationToken ct)
    {
        // 10% chance of failure for testing
        if (_random.Next(10) == 0)
        {
            return Task.FromResult(new PaymentResult(
                Succeeded: false,
                ProviderRef: null,
                Error: "Insufficient funds"
            ));
        }
        
        return Task.FromResult(new PaymentResult(
            Succeeded: true,
            ProviderRef: $"MOCK-{Guid.NewGuid():N}",
            Error: null
        ));
    }
}
```

### Configuration-Based Behavior
Allow `appsettings.json` to control mock behavior:
```json
{
  "MockPaymentGateway": {
    "AlwaysSucceed": true,
    "FailureRate": 0.0,
    "SimulatedLatencyMs": 100
  }
}
```

## Notes
- The mock implementation is clearly named and commented as temporary
- Interface design matches common payment gateway patterns
- Using C# 10 record types for request/response reduces boilerplate
- Async signature prepared for real I/O-bound payment calls
- Cancellation token support for timeout handling (though not used in mock)
