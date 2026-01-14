# High-Level Design (HLD) - Retail Monolith

## Overview

The Retail Monolith is an ASP.NET Core 8 web application that implements a simplified e-commerce platform. It demonstrates a traditional monolithic architecture before decomposition into microservices. The application handles product catalog management, shopping cart operations, checkout processing, and order management within a single deployable unit.

## Architecture

### System Type
- **Framework**: ASP.NET Core 8.0
- **Pattern**: Monolithic web application
- **UI Technology**: Razor Pages (server-side rendering)
- **APIs**: Minimal APIs for specific endpoints

### Key Components/Modules

The application is organized into four primary domain boundaries:

#### 1. Products Module
- **Responsibility**: Product catalog management
- **Key Features**:
  - Product listing with SKU, name, description, price, currency, category
  - Active/inactive product states
  - Category-based organization
- **Data**: `Products` table

#### 2. Inventory Module
- **Responsibility**: Stock management
- **Key Features**:
  - Track quantity available per SKU
  - Stock reservation during checkout
  - Optimistic concurrency for inventory updates
- **Data**: `Inventory` table (linked to Products by SKU)

#### 3. Cart Module
- **Responsibility**: Shopping cart management
- **Key Features**:
  - Per-customer cart with line items
  - Add products to cart
  - View cart contents with totals
  - Clear cart functionality
- **Data**: `Carts` and `CartLines` tables
- **Service**: `ICartService` / `CartService`

#### 4. Orders & Checkout Module
- **Responsibility**: Order processing and payment
- **Key Features**:
  - Checkout flow with payment processing
  - Order creation from cart
  - Order status tracking (Created, Paid, Failed, Shipped)
  - Inventory deduction on successful checkout
- **Data**: `Orders` and `OrderLines` tables
- **Services**: 
  - `ICheckoutService` / `CheckoutService`
  - `IPaymentGateway` / `MockPaymentGateway`

## Data Stores

### Primary Database
- **Type**: SQL Server
- **Development**: LocalDB (`(localdb)\MSSQLLocalDB`)
- **Production**: Configurable via connection string in `appsettings.json`
- **Database Name**: `RetailMonolith`
- **ORM**: Entity Framework Core 9.0.9

### Database Schema
All data is stored in a single relational database with the following tables:
- `Products` - Product catalog
- `Inventory` - Stock levels per SKU
- `Carts` - Customer shopping carts
- `CartLines` - Line items in carts
- `Orders` - Completed orders
- `OrderLines` - Line items in orders

### Relationships
- `Cart` 1:N `CartLine` (one cart has many lines)
- `Order` 1:N `OrderLine` (one order has many lines)
- `Product.Sku` ↔ `Inventory.Sku` (unique SKU links products to inventory)
- `CartLine.Sku` and `OrderLine.Sku` reference product SKUs

## External Dependencies

### Payment Gateway
- **Interface**: `IPaymentGateway`
- **Implementation**: `MockPaymentGateway` (always succeeds)
- **Purpose**: Simulates external payment processing
- **Future**: Intended to be replaced with real payment provider integration

### Health Checks
- **Endpoint**: `/health`
- **Framework**: `Microsoft.AspNetCore.Diagnostics.HealthChecks`
- **Purpose**: Application health monitoring

## Runtime Environment

### Hosting
- **Runtime**: .NET 8.0
- **Server**: Kestrel (built-in ASP.NET Core web server)
- **Deployment**: Single process monolith

### Startup Behavior
On application start:
1. Database connection established
2. **Auto-migration**: EF Core migrations applied automatically via `db.Database.MigrateAsync()`
3. **Auto-seeding**: If no products exist, 50 sample products with inventory are seeded
4. Dependency injection container configured
5. Middleware pipeline established
6. Razor Pages and minimal API endpoints registered

### Configuration
- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- Connection string: `ConnectionStrings:DefaultConnection`
- Environment-based configuration via `ASPNETCORE_ENVIRONMENT`

### Key Endpoints

#### Razor Pages (UI)
- `/` - Home page
- `/Products` - Product listing with "Add to Cart"
- `/Cart` - Shopping cart view
- `/Checkout` - Checkout page (placeholder)
- `/Orders` - Orders page (placeholder)

#### Minimal APIs
- `POST /api/checkout` - Process checkout for "guest" user
  - Input: Hardcoded customer ID and payment token
  - Output: Order ID, status, and total
- `GET /api/orders/{id}` - Retrieve order details by ID
  - Output: Order with line items

#### Health Check
- `GET /health` - Application health status

## Deployment Assumptions

### Development
- Runs on LocalDB (comes with Visual Studio)
- HTTPS on port 5001, HTTP on port 5000
- Development SSL certificate required
- Database is created and seeded automatically

### Production Considerations
- SQL Server connection string must be configured
- HSTS enabled in non-development environments
- Exception handling via `/Error` page
- Static files served from `wwwroot`
- HTTPS redirection enforced

## Technology Stack Summary

| Component | Technology | Version |
|-----------|-----------|---------|
| Framework | ASP.NET Core | 8.0 |
| Language | C# | 12.0 (via .NET 8) |
| ORM | Entity Framework Core | 9.0.9 |
| Database | SQL Server | (LocalDB for dev) |
| UI | Razor Pages | Core 8.0 |
| DI Container | Built-in ASP.NET Core | Core 8.0 |
| Health Checks | Microsoft.AspNetCore.Diagnostics.HealthChecks | 2.2.0 |

## Design Principles

### Current State
- **Monolithic architecture**: All functionality in single application
- **Layered structure**: Pages → Services → Data
- **Dependency injection**: Loose coupling via interfaces
- **Separation of concerns**: Distinct service classes for Cart and Checkout

### Coupling Points
- All modules share single `AppDbContext`
- Services directly access database through shared DbContext
- No event-driven communication (noted in code comments as future enhancement)
- Guest user hardcoded throughout (no authentication/authorization)

## Scalability Considerations

### Current Limitations
- Single database limits horizontal scaling
- Synchronous, blocking operations throughout
- No caching layer
- No message queue or async processing
- Optimistic concurrency only (potential race conditions under load)

### Decomposition Readiness
- Service interfaces defined (`ICartService`, `ICheckoutService`, `IPaymentGateway`)
- Domain boundaries identifiable
- Minimal APIs provide foundation for future service extraction
- Comments in code reference future event publishing
