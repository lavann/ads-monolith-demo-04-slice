# Runbook - Retail Monolith Application

## Overview
Operational guide for building, running, and troubleshooting the Retail Monolith application.

---

## Prerequisites

### Required Software
| Component | Version | Download |
|-----------|---------|----------|
| .NET SDK | 8.0 | https://dotnet.microsoft.com/download/dotnet/8.0 |
| SQL Server LocalDB | 2019+ | Included with Visual Studio |

### Optional Tools
- **Visual Studio 2022** - Full IDE with LocalDB
- **Visual Studio Code** - Lightweight editor
- **SQL Server Management Studio (SSMS)** - Database management
- **Azure Data Studio** - Cross-platform database tool

### Verify Installation
```bash
# Check .NET SDK version
dotnet --version
# Expected: 8.0.x

# Check LocalDB (Windows only)
sqllocaldb info
# Expected: MSSQLLocalDB listed
```

---

## First-Time Setup

### 1. Clone Repository
```bash
git clone https://github.com/lavann/ads-monolith-demo-04-slice.git
cd ads-monolith-demo-04-slice
```

### 2. Restore Dependencies
```bash
dotnet restore
```
**Output**: Should restore packages including:
- Microsoft.EntityFrameworkCore.SqlServer 9.0.9
- Microsoft.EntityFrameworkCore.Design 9.0.9
- Microsoft.AspNetCore.Diagnostics.HealthChecks 2.2.0

### 3. Build Application
```bash
dotnet build
```
**Expected**: Build succeeds with 4 warnings (nullable reference warnings - known tech debt)

### 4. Run Application
```bash
dotnet run
```
**Expected Output**:
```
Building...
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (...)
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

**First Run**: 
- Database will be created automatically
- Migrations applied
- 50 sample products seeded

**Access Application**: 
- HTTPS: https://localhost:5001
- HTTP: http://localhost:5000

---

## Common Commands

### Building

```bash
# Clean build
dotnet clean
dotnet build

# Build in Release mode
dotnet build -c Release

# Restore and build
dotnet build
```

### Running

```bash
# Run with default settings
dotnet run

# Run with specific environment
dotnet run --environment Production

# Run with hot reload (watches for file changes)
dotnet watch run

# Run without building first
dotnet run --no-build
```

### Database Operations

#### View Database
```bash
# Connect to LocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -d RetailMonolith -E
```

#### Reset Database
```bash
# Drop database (forces recreation on next run)
dotnet ef database drop -f

# Run application to recreate
dotnet run
```

#### Create New Migration
```bash
# After modifying model classes
dotnet ef migrations add <MigrationName>

# Example
dotnet ef migrations add AddProductCategory
```

#### Apply Migrations Manually
```bash
# Update to latest migration
dotnet ef database update

# Update to specific migration
dotnet ef database update <MigrationName>

# Rollback to previous migration
dotnet ef database update <PreviousMigrationName>
```

#### View Migrations
```bash
# List all migrations
dotnet ef migrations list

# View pending migrations
dotnet ef migrations has-pending-model-changes
```

#### Remove Last Migration (if not applied)
```bash
dotnet ef migrations remove
```

### Testing

```bash
# Run all tests (none currently exist)
dotnet test

# Run tests with detailed output
dotnet test --verbosity detailed
```

### Publishing

```bash
# Publish self-contained application
dotnet publish -c Release -o ./publish

# Publish for specific OS
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
```

---

## Application Endpoints

### Razor Pages (UI)
| URL | Description | Method |
|-----|-------------|--------|
| `/` | Home page | GET |
| `/Products` | Product listing | GET |
| `/Products` | Add to cart | POST |
| `/Cart` | View shopping cart | GET |
| `/Checkout` | Checkout page (placeholder) | GET |
| `/Orders` | Orders page (placeholder) | GET |
| `/Privacy` | Privacy policy | GET |
| `/Error` | Error page | GET |

### Minimal APIs
| URL | Description | Method | Request Body |
|-----|-------------|--------|--------------|
| `/api/checkout` | Process checkout | POST | None (uses hardcoded values) |
| `/api/orders/{id}` | Get order by ID | GET | N/A |

### Health Check
| URL | Description | Method |
|-----|-------------|--------|
| `/health` | Application health | GET |

---

## Configuration

### Connection String
**File**: `appsettings.json` or `appsettings.Development.json`

**Default** (hardcoded in `Program.cs`):
```
Server=(localdb)\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true
```

**Override via appsettings.json**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=myserver;Database=RetailMonolith;User Id=myuser;Password=mypass;"
  }
}
```

**Override via Environment Variable**:
```bash
# Windows
set ConnectionStrings__DefaultConnection="Server=...;Database=...;"
dotnet run

# Linux/Mac
export ConnectionStrings__DefaultConnection="Server=...;Database=...;"
dotnet run
```

### Logging Configuration
**File**: `appsettings.json`
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

**Enable SQL logging** (see generated queries):
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

---

## Troubleshooting

### Issue: "Cannot open database" Error

**Symptoms**:
```
A network-related or instance-specific error occurred while establishing a connection to SQL Server.
```

**Cause**: LocalDB not started or database doesn't exist

**Solution**:
```bash
# Start LocalDB
sqllocaldb start MSSQLLocalDB

# Verify it's running
sqllocaldb info MSSQLLocalDB

# Let application create database
dotnet run
```

---

### Issue: Build Warnings About Nullable Properties

**Symptoms**:
```
warning CS8618: Non-nullable property 'Sku' must contain a non-null value when exiting constructor
```

**Cause**: Known tech debt - model properties should be marked `required` or nullable

**Impact**: Warnings only, doesn't prevent execution

**Solution** (if needed):
Option 1 - Mark properties as required:
```csharp
public required string Sku { get; set; }
```

Option 2 - Mark properties as nullable:
```csharp
public string? Sku { get; set; }
```

Option 3 - Suppress warnings (not recommended):
```xml
<PropertyGroup>
  <NoWarn>CS8618</NoWarn>
</PropertyGroup>
```

---

### Issue: "Migrations Already Applied" Error

**Symptoms**: Can't remove migration because it's already applied to database

**Solution**:
```bash
# Rollback to previous migration first
dotnet ef database update <PreviousMigration>

# Then remove migration
dotnet ef migrations remove
```

---

### Issue: Duplicate Products in Cart

**Symptoms**: Same product added twice when clicking "Add to Cart" once

**Cause**: Bug in `Products/Index.cshtml.cs` - `OnPostAsync` has duplicate logic

**Code Location**: Line 32-49 in `Pages/Products/Index.cshtml.cs`

**Workaround**: Clear cart before testing

**Fix Required**: Remove manual cart manipulation, use only `CartService`

---

### Issue: Port Already in Use

**Symptoms**:
```
System.IO.IOException: Failed to bind to address https://127.0.0.1:5001
```

**Cause**: Another instance running or port occupied

**Solution**:
```bash
# Windows - Find process
netstat -ano | findstr :5001

# Kill process by PID
taskkill /PID <process_id> /F

# Linux/Mac - Find and kill
lsof -ti:5001 | xargs kill
```

**Alternative**: Change port in `Properties/launchSettings.json`

---

### Issue: SSL Certificate Error in Browser

**Symptoms**: "Your connection is not private" or "NET::ERR_CERT_AUTHORITY_INVALID"

**Cause**: Development SSL certificate not trusted

**Solution**:
```bash
# Trust development certificate
dotnet dev-certs https --trust
```

**Note**: Windows/Mac only. Linux users must manually trust certificate.

---

### Issue: No Products Showing

**Symptoms**: Products page is empty

**Cause**: 
1. Database seeding didn't run
2. Database reset but application didn't restart

**Solution**:
```bash
# Drop and recreate database
dotnet ef database drop -f
dotnet run
```

**Verify Seeding**:
```sql
-- Connect to database
SELECT COUNT(*) FROM Products;
-- Should return 50
```

---

### Issue: Checkout API Returns 500 Error

**Symptoms**: `POST /api/checkout` fails with internal server error

**Causes**:
1. **No cart exists for "guest" user** - Cart must be populated first
2. **Out of stock** - Inventory quantity insufficient
3. **Database error** - Connection or constraint violation

**Solution**:
1. Add products to cart via UI first
2. Check logs for specific error:
   ```bash
   dotnet run --environment Development
   # Check console output for exception details
   ```

---

### Issue: Auto-Migration Slows Startup

**Symptoms**: Application takes 10+ seconds to start

**Cause**: Large migrations or slow database connection

**Solution**:
Option 1 - Apply migrations manually:
```bash
dotnet ef database update
```
Then comment out auto-migration in `Program.cs` (lines 24-29)

Option 2 - Use faster database (local Docker container instead of LocalDB)

---

## Known Issues / Tech Debt

### 1. Hardcoded "guest" User
- **Impact**: Multi-user support impossible
- **Location**: Throughout application (Models, Services, Pages)
- **Workaround**: None - single-user demo only
- **Fix Required**: Implement authentication/authorization

### 2. Duplicate Add-to-Cart Logic
- **Impact**: Products added twice to cart
- **Location**: `Pages/Products/Index.cshtml.cs` line 32-50
- **Workaround**: Clear cart before testing
- **Fix Required**: Remove manual cart manipulation

### 3. Nullable Reference Warnings
- **Impact**: Build warnings (4 warnings)
- **Location**: `Models/Product.cs`, `Models/InventoryItem.cs`
- **Workaround**: Ignore warnings
- **Fix Required**: Add `required` modifier or make nullable

### 4. Mock Payment Always Succeeds
- **Impact**: Cannot test payment failure scenarios
- **Location**: `Services/MockPaymentGateway.cs`
- **Workaround**: None - always returns success
- **Fix Required**: Add failure simulation or use real gateway

### 5. Checkout UI is Placeholder
- **Impact**: No checkout flow in UI
- **Location**: `Pages/Checkout/Index.cshtml`
- **Workaround**: Use API endpoint directly
- **Fix Required**: Build checkout form

### 6. No Order Listing UI
- **Impact**: Orders page is empty
- **Location**: `Pages/Orders/Index.cshtml`
- **Workaround**: Query database directly or use API
- **Fix Required**: Implement order list view

### 7. No Inventory UI
- **Impact**: Cannot view or manage stock levels
- **Location**: Missing entirely
- **Workaround**: Query database directly
- **Fix Required**: Build inventory management page

### 8. Auto-Migration on Startup
- **Impact**: Risky for production, can slow startup
- **Location**: `Program.cs` line 24-29
- **Workaround**: Disable for production
- **Fix Required**: Conditional based on environment

### 9. No Input Validation
- **Impact**: Can add negative quantities, invalid data
- **Location**: Throughout application
- **Workaround**: Enter valid data only
- **Fix Required**: Add validation attributes and guard clauses

### 10. No Logging in Services
- **Impact**: Difficult to diagnose issues
- **Location**: `Services/*`
- **Workaround**: Add breakpoints or debug statements
- **Fix Required**: Inject `ILogger` and add logging

### 11. Optimistic Concurrency for Inventory
- **Impact**: Race condition - overselling possible
- **Location**: `CheckoutService.CheckoutAsync()`
- **Workaround**: Don't run load tests
- **Fix Required**: Add locking or use pessimistic concurrency

---

## Performance Notes

### Database
- **LocalDB limitations**: Not designed for high concurrency
- **Connection pooling**: Enabled by default
- **Indexing**: Only SKU fields indexed

### Application
- **No caching**: All data fetched from database on every request
- **Synchronous operations**: No async/await optimization opportunities missed
- **No connection resilience**: No retry policies configured

---

## Useful Database Queries

### View All Products
```sql
SELECT * FROM Products WHERE IsActive = 1;
```

### View Cart Contents
```sql
SELECT c.Id, c.CustomerId, cl.Name, cl.Quantity, cl.UnitPrice
FROM Carts c
JOIN CartLines cl ON c.Id = cl.CartId
WHERE c.CustomerId = 'guest';
```

### View Inventory Levels
```sql
SELECT i.Sku, p.Name, i.Quantity
FROM Inventory i
JOIN Products p ON i.Sku = p.Sku
ORDER BY i.Quantity;
```

### View All Orders
```sql
SELECT o.Id, o.CreatedUtc, o.Status, o.Total, COUNT(ol.Id) as LineCount
FROM Orders o
LEFT JOIN OrderLines ol ON o.Id = ol.OrderId
GROUP BY o.Id, o.CreatedUtc, o.Status, o.Total
ORDER BY o.CreatedUtc DESC;
```

### View Order Details
```sql
SELECT ol.Name, ol.Quantity, ol.UnitPrice, (ol.Quantity * ol.UnitPrice) as LineTotal
FROM Orders o
JOIN OrderLines ol ON o.Id = ol.OrderId
WHERE o.Id = 1;  -- Replace with actual order ID
```

---

## Security Considerations

### Current State (Demo)
- ❌ No authentication
- ❌ No authorization
- ❌ No CSRF protection on forms
- ❌ Hardcoded user identity
- ❌ No input sanitization
- ❌ Auto-migration exposes DDL permissions
- ✅ HTTPS enforced
- ✅ HSTS enabled (non-dev)

### Before Production
- Implement ASP.NET Core Identity or external auth
- Add `[ValidateAntiForgeryToken]` to POST handlers
- Validate and sanitize all user inputs
- Disable auto-migration
- Use least-privilege database credentials
- Add rate limiting
- Implement logging and monitoring

---

## Next Steps

This runbook documents the application **as it exists today**. For planned improvements and architectural evolution, refer to future documentation and ADRs.
