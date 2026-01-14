# Low-Level Design (LLD) - Retail Monolith

## Overview

This document details the internal structure, key classes, request flows, and technical implementation of the Retail Monolith application.

## Module Breakdown

### 1. Data Layer (`/Data`)

#### `AppDbContext.cs`
**Purpose**: Central database context for Entity Framework Core

**Key Responsibilities**:
- Defines DbSet properties for all entities
- Configures entity relationships and constraints
- Handles database seeding

**DbSets**:
- `Products` - Product catalog
- `Inventory` - Stock levels
- `Carts` - Shopping carts
- `CartLines` - Cart line items
- `Orders` - Customer orders
- `OrderLines` - Order line items

**Configuration**:
```csharp
protected override void OnModelCreating(ModelBuilder b)
{
    b.Entity<Product>().HasIndex(p => p.Sku).IsUnique();
    b.Entity<InventoryItem>().HasIndex(i => i.Sku).IsUnique();
}
```
- Unique constraint on Product.Sku
- Unique constraint on InventoryItem.Sku

**Seeding Logic** (`SeedAsync`):
- Checks if products exist; if not, generates 50 sample products
- Random categories: Apparel, Footwear, Accessories, Electronics, Home, Beauty
- Prices: £5-£105 (random)
- Inventory: 10-200 units per SKU (random)
- Currency: GBP

#### `DesignTimeDbContextFactory.cs`
**Purpose**: Enables EF Core CLI tools (migrations, scaffolding)

**Connection String**: 
```
Server=(localdb)\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true
```

---

### 2. Models Layer (`/Models`)

#### `Product.cs`
```csharp
public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; }           // Unique identifier
    public string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; }
    public bool IsActive { get; set; }
    public string? Category { get; set; }
}
```

#### `InventoryItem.cs`
```csharp
public class InventoryItem
{
    public int Id { get; set; }
    public string Sku { get; set; }          // Links to Product.Sku
    public int Quantity { get; set; }
}
```

#### `Cart.cs` and `CartLine.cs`
```csharp
public class Cart
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = "guest";
    public List<CartLine> Lines { get; set; } = new();
}

public class CartLine
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public Cart? Cart { get; set; }         // Navigation property
    public string Sku { get; set; }
    public string Name { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
```
**Relationship**: One-to-many (Cart → CartLines)

#### `Order.cs` and `OrderLine.cs`
```csharp
public class Order
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string CustomerId { get; set; } = "guest";
    public string Status { get; set; } = "Created";  // Created|Paid|Failed|Shipped
    public decimal Total { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }       // Navigation property
    public string Sku { get; set; }
    public string Name { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
```
**Relationship**: One-to-many (Order → OrderLines)

---

### 3. Services Layer (`/Services`)

#### `ICartService.cs` / `CartService.cs`
**Purpose**: Encapsulates shopping cart business logic

**Methods**:
1. `GetOrCreateCartAsync(customerId)` - Retrieves or creates cart
2. `AddToCartAsync(customerId, productId, quantity)` - Adds product to cart
3. `GetCartWithLinesAsync(customerId)` - Gets cart with line items
4. `ClearCartAsync(customerId)` - Removes cart and all lines

**Key Logic**:
- **Add to Cart**: 
  - Fetches product by ID
  - Checks if product already in cart
  - If exists: increments quantity
  - If new: adds new CartLine
  - Saves changes to database

**Dependencies**: `AppDbContext`

#### `ICheckoutService.cs` / `CheckoutService.cs`
**Purpose**: Orchestrates checkout process

**Method**:
- `CheckoutAsync(customerId, paymentToken)` → Returns `Order`

**Checkout Flow**:
1. **Fetch Cart**: Load cart with lines or throw exception
2. **Calculate Total**: Sum of `UnitPrice * Quantity` for all lines
3. **Reserve Inventory**: 
   - For each cart line, find inventory by SKU
   - Check if sufficient quantity available
   - Decrement inventory (optimistic locking)
   - Throws exception if out of stock
4. **Process Payment**: Call `IPaymentGateway.ChargeAsync()`
5. **Create Order**:
   - Status = "Paid" if payment succeeded, "Failed" otherwise
   - Copy cart lines to order lines
6. **Clear Cart**: Remove all cart lines
7. **Save Changes**: Persist order and inventory updates

**Dependencies**: `AppDbContext`, `IPaymentGateway`

**Note**: All operations in single transaction (implicit via DbContext)

#### `IPaymentGateway.cs` / `MockPaymentGateway.cs`
**Purpose**: Abstraction for payment processing

**Interface**:
```csharp
public record PaymentRequest(decimal Amount, string Currency, string Token);
public record PaymentResult(bool Succeeded, string? ProviderRef, string? Error);

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest req, CancellationToken ct);
}
```

**Mock Implementation**:
- Always returns success
- Generates mock reference: `MOCK-{Guid}`
- No actual payment processing

---

### 4. Pages Layer (`/Pages`)

#### Razor Pages Pattern
Each page consists of:
- `.cshtml` - View markup (HTML + Razor syntax)
- `.cshtml.cs` - Page model (code-behind)

#### `Products/Index.cshtml.cs`
**Purpose**: Product catalog page

**Properties**:
- `IList<Product> Products` - List of active products

**Handlers**:
- `OnGetAsync()` - Loads active products from database
- `OnPostAsync(int productId)` - Handles "Add to Cart" button
  - Fetches product by ID
  - Gets or creates cart for "guest"
  - Adds CartLine to cart
  - **Issue**: Redundant logic - adds CartLine directly AND calls CartService
  - Redirects to `/Cart`

**Dependencies**: `AppDbContext`, `ICartService`

#### `Cart/Index.cshtml.cs`
**Purpose**: Shopping cart view

**Properties**:
- `List<(string Name, int Quantity, decimal Price)> Lines` - Cart line items
- `decimal Total` - Computed property: `Sum(Price * Quantity)`

**Handlers**:
- `OnGetAsync()` - Loads cart for "guest" and maps lines to tuples

**Dependencies**: `ICartService`

**Note**: Uses value tuple for view model instead of dedicated DTO

#### `Checkout/Index.cshtml.cs`
**Purpose**: Checkout page (placeholder)

**Status**: Minimal implementation - no checkout logic in UI
- Only has empty `OnGet()` method
- View shows "Hello World Checkout"
- Actual checkout happens via API endpoint

#### `Orders/Index.cshtml.cs`
**Purpose**: Orders listing page (placeholder)

**Status**: Empty implementation
- Only has empty `OnGet()` method
- No order display logic

---

### 5. Program.cs (Application Entry Point)

#### Service Registration
```csharp
builder.Services.AddDbContext<AppDbContext>(...)
builder.Services.AddRazorPages();
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddHealthChecks();
```

#### Startup Logic
1. Auto-migration: `await db.Database.MigrateAsync();`
2. Auto-seeding: `await AppDbContext.SeedAsync(db);`

#### Middleware Pipeline
- Exception handler (non-dev): `/Error`
- HSTS (non-dev)
- HTTPS redirection
- Static files
- Routing
- Authorization (placeholder - no auth implemented)
- Razor Pages endpoint mapping

#### Minimal API Endpoints
```csharp
// Checkout API
POST /api/checkout
- Hardcoded: customerId="guest", paymentToken="tok_test"
- Calls CheckoutService.CheckoutAsync()
- Returns: { Id, Status, Total }

// Order retrieval API
GET /api/orders/{id}
- Queries Order by ID with OrderLines included
- Returns: Full order object or 404
```

---

## Key Request Flows

### Flow 1: Browse Products → Add to Cart

1. **User visits** `/Products`
2. `Products/Index.OnGetAsync()` executes
   - Query: `_db.Products.Where(p => p.IsActive).ToListAsync()`
3. View renders product table with "Add to Cart" buttons
4. **User clicks** "Add to Cart" for a product
5. `Products/Index.OnPostAsync(productId)` executes
   - Fetches product from database
   - Gets or creates cart for "guest"
   - **BUG**: Adds CartLine both manually and via service call
   - Calls `_cartService.AddToCartAsync("guest", productId)`
6. Redirects to `/Cart`

### Flow 2: View Cart

1. **User visits** `/Cart`
2. `Cart/Index.OnGetAsync()` executes
   - Calls `_cartService.GetCartWithLinesAsync("guest")`
   - Service queries: `_db.Carts.Include(c => c.Lines).FirstOrDefaultAsync(...)`
3. Maps CartLines to tuple list: `(Name, Quantity, UnitPrice)`
4. View renders cart table with total
5. "Proceed to Checkout" button links to `/Checkout`

### Flow 3: Checkout via API

1. **Client calls** `POST /api/checkout`
2. Minimal API handler invokes `CheckoutService.CheckoutAsync("guest", "tok_test")`
3. **CheckoutService.CheckoutAsync()** executes:
   ```
   a. Load cart with lines (or throw exception)
   b. Calculate total
   c. FOR EACH cart line:
       - Find inventory by SKU
       - Check quantity available
       - Decrement inventory quantity
   d. Call PaymentGateway.ChargeAsync()
   e. Create Order with status based on payment result
   f. Copy cart lines to order lines
   g. Delete cart lines
   h. SaveChangesAsync()
   ```
4. Returns order: `{ Id, Status, Total }`

### Flow 4: Retrieve Order

1. **Client calls** `GET /api/orders/{id}`
2. Minimal API handler queries:
   ```csharp
   await db.Orders.Include(o => o.Lines)
       .SingleOrDefaultAsync(o => o.Id == id);
   ```
3. Returns order with lines or 404

---

## Areas of Coupling / Hotspots

### 1. Hardcoded "guest" Customer ID
**Location**: Throughout application
- `Cart.CustomerId` default value
- `Order.CustomerId` default value
- All service calls and page handlers

**Impact**: 
- No multi-user support
- Cannot identify individual customers
- All users share same cart and orders

**Risk**: High - blocks real-world usage

---

### 2. Shared AppDbContext Across All Services
**Location**: Services layer

**Impact**:
- All services operate on same database transaction context
- Tight coupling to database schema
- Cannot easily extract services to separate deployments

**Risk**: Medium - hinders decomposition to microservices

---

### 3. Synchronous Inventory Management in Checkout
**Location**: `CheckoutService.CheckoutAsync()`

**Issue**:
- Inventory decremented inline during checkout
- Optimistic concurrency (no locking)
- Potential race condition under concurrent checkouts

**Risk**: High - overselling possible under load

---

### 4. Redundant Cart Logic in Products Page
**Location**: `Pages/Products/Index.cshtml.cs` - `OnPostAsync()`

**Issue**:
```csharp
// Manually creates cart and adds line
cart.Lines.Add(new CartLine { ... });

// Also calls service
await _cartService.AddToCartAsync("guest", productId);
```

**Impact**: 
- Same product added twice to cart
- Duplicate logic between page and service

**Risk**: High - functional bug

---

### 5. Mock Payment Gateway Always Succeeds
**Location**: `MockPaymentGateway.ChargeAsync()`

**Impact**:
- No payment validation
- Cannot test failure scenarios
- Orders always marked as "Paid"

**Risk**: Low for demo, High for production

---

### 6. Auto-Migration on Startup
**Location**: `Program.cs` - startup logic

**Issue**:
```csharp
await db.Database.MigrateAsync();
await AppDbContext.SeedAsync(db);
```

**Impact**:
- Database schema changes applied automatically
- No rollback strategy
- Risky in production environments
- Startup delays if migrations are slow

**Risk**: Medium - deployment risk

---

### 7. No Validation Layer
**Location**: Throughout application

**Missing**:
- No input validation on page handlers
- No model validation attributes
- No guard clauses in services

**Examples**:
- Negative quantities not prevented
- Invalid product IDs only throw generic exceptions
- No currency validation

**Risk**: Medium - data integrity issues

---

### 8. Placeholder Checkout UI
**Location**: `Pages/Checkout/Index.cshtml`

**Issue**:
- UI page exists but does nothing
- Actual checkout only via API endpoint
- Disconnect between UI flow and API

**Risk**: Low - feature gap, not a defect

---

### 9. No Logging in Business Logic
**Location**: Services layer

**Missing**:
- No ILogger injection in services
- No audit trail for checkout
- No payment failure logging
- No inventory change tracking

**Risk**: Medium - observability gap

---

### 10. Single Database Transaction Boundary
**Location**: `CheckoutService.CheckoutAsync()`

**Issue**:
- Entire checkout in one transaction
- Cart deletion, inventory update, order creation all-or-nothing
- Long transaction holds locks

**Impact**:
- Good for consistency
- Bad for scalability
- Blocks eventual consistency patterns

**Risk**: Low now, High for scale

---

## Technical Debt Observations

### Compiler Warnings (Build Output)
```
CS8618: Non-nullable property 'Sku' must contain a non-null value when exiting constructor
CS8618: Non-nullable property 'Name' must contain a non-null value
CS8618: Non-nullable property 'Currency' must contain a non-null value
```
**Location**: `Product.cs`, `InventoryItem.cs`

**Reason**: Properties should be marked as `required` or nullable

---

### Unused Usings
**Location**: `Pages/Products/Index.cshtml.cs`
```csharp
using Microsoft.AspNetCore.Cors.Infrastructure;  // Not used
```

---

### Comments Indicating Future Work
**Location**: `CheckoutService.cs` - line 54
```csharp
// (future) publish events here: OrderCreated / PaymentProcessed / InventoryReserved
```

**Interpretation**: Code anticipates event-driven architecture

---

## Class Diagram Summary

```
[AppDbContext]
    ↓ injected into
[CartService] ← [ICartService]
[CheckoutService] ← [ICheckoutService]
    ↓ uses
[MockPaymentGateway] ← [IPaymentGateway]

[Razor Pages]
    ↓ depend on
[Services + AppDbContext]

[Minimal APIs]
    ↓ depend on
[CheckoutService + AppDbContext]
```

---

## Database Access Patterns

### Direct DbContext Usage
- Minimal API endpoints access `AppDbContext` directly
- Some Razor Pages access `AppDbContext` directly (Products page)

### Service Layer Usage
- Cart operations go through `ICartService`
- Checkout operations go through `ICheckoutService`

**Inconsistency**: Mixed patterns - sometimes direct, sometimes through services

---

## Dependency Graph

```
Program.cs
 ├─ AppDbContext
 ├─ CartService (implements ICartService)
 │   └─ requires AppDbContext
 ├─ CheckoutService (implements ICheckoutService)
 │   ├─ requires AppDbContext
 │   └─ requires IPaymentGateway
 ├─ MockPaymentGateway (implements IPaymentGateway)
 ├─ Products/Index Page
 │   ├─ requires AppDbContext
 │   └─ requires ICartService
 ├─ Cart/Index Page
 │   └─ requires ICartService
 └─ Minimal APIs
     ├─ requires ICheckoutService
     └─ requires AppDbContext
```

---

## Summary

The application follows a layered monolithic architecture with:
- **Data models** defining the domain
- **Services** encapsulating business logic (partially)
- **Razor Pages** handling UI requests
- **Minimal APIs** providing RESTful endpoints

Key characteristics:
- Service interfaces enable testability and future extraction
- Mixed data access patterns (direct vs. service-mediated)
- Significant coupling through shared database context
- Several hotspots with functional bugs and tech debt
- Foundation for decomposition exists but not fully realized
