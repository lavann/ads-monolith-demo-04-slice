# ADR-002: Service Layer Pattern with Dependency Injection

## Status
Accepted

## Context
The Retail Monolith needs to separate business logic from presentation layer (Razor Pages) and data access layer (EF Core DbContext). The application should support:
- Testability through isolation of business logic
- Potential future extraction of services into microservices
- Clear separation of concerns
- Consistent patterns across the application

## Decision
Implement a **service layer pattern** with interface-based abstractions and constructor dependency injection.

**Interfaces Defined**:
- `ICartService` - Shopping cart operations
- `ICheckoutService` - Order checkout and payment processing
- `IPaymentGateway` - Payment provider abstraction

**Implementations**:
- `CartService` - Implements cart business logic
- `CheckoutService` - Orchestrates checkout flow
- `MockPaymentGateway` - Simulates payment processing

**Dependency Injection** (in `Program.cs`):
```csharp
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<ICartService, CartService>();
```

**Service Lifetime**: `Scoped` - One instance per HTTP request

## Consequences

### Positive
- **Testability**: Services can be mocked/stubbed in unit tests
- **Separation of concerns**: Business logic isolated from UI and data access
- **Flexibility**: Implementations can be swapped without changing consumers
- **Future extraction**: Service interfaces define natural boundaries for microservices
- **Dependency inversion**: High-level modules don't depend on low-level details
- **Reusability**: Services can be consumed by Razor Pages and Minimal APIs

### Negative
- **Indirection**: Extra layer between pages and database
- **Incomplete adoption**: Some pages still access `AppDbContext` directly (e.g., Products page)
- **Boilerplate**: Interfaces add code ceremony for simple operations
- **Shared DbContext**: Services still coupled through shared database context
- **No transaction management**: Service boundaries don't align with transaction boundaries

### Current Inconsistencies

#### Mixed Data Access Patterns
1. **Cart operations**: Go through `ICartService` ✓
2. **Checkout operations**: Go through `ICheckoutService` ✓
3. **Product listing**: Products page accesses `AppDbContext` directly ✗
4. **Order retrieval**: Minimal API accesses `AppDbContext` directly ✗

**Implication**: Pattern not consistently applied across all domain boundaries

#### Products Page Duplication
```csharp
// Products/Index.cshtml.cs - OnPostAsync
cart.Lines.Add(new CartLine { ... });           // Manual manipulation
await _cartService.AddToCartAsync(...);         // Service call
```
**Issue**: Page both injects `ICartService` and `AppDbContext`, leading to redundant logic

## Alternatives Considered

#### Direct DbContext Access Everywhere
- **Pros**: Simpler, less code, no abstraction overhead
- **Cons**: Tight coupling, hard to test, no service boundaries
- **Why rejected**: Doesn't support future decomposition goals

#### Repository Pattern
- **Pros**: Additional abstraction over DbContext, unit-of-work pattern
- **Cons**: Overkill for EF Core (already implements repository/UoW)
- **Why rejected**: EF Core DbSet is effectively a repository

#### CQRS (Command Query Responsibility Segregation)
- **Pros**: Clear separation of reads and writes, scalability
- **Cons**: Significant complexity for monolith, overkill for current needs
- **Why rejected**: Over-engineering for demo application

#### Domain-Driven Design (Aggregate Roots, Domain Services)
- **Pros**: Rich domain models, encapsulation
- **Cons**: Steep learning curve, requires significant refactoring
- **Why rejected**: Anemic domain model already established

## Recommendations for Improvement

1. **Consistency**: Apply service pattern to all domain operations
   - Create `IProductService` for product operations
   - Create `IOrderService` for order operations
   - Remove direct `AppDbContext` injection from Razor Pages

2. **Fix redundancy**: Remove dual add-to-cart logic in Products page
   - Either use service OR direct DbContext, not both

3. **Transaction boundaries**: Make services manage their own transactions
   - Consider unit-of-work pattern if cross-service transactions needed

4. **Validation**: Add input validation within services
   - Guard clauses for null/invalid parameters
   - Business rule validation before persistence

## Notes
- Service pattern chosen anticipates future microservices decomposition
- Interface abstractions enable API versioning and backward compatibility
- Current implementation is a starting point, not fully realized
- Comment in `CheckoutService` mentions future event publishing, indicating event-driven architecture consideration
