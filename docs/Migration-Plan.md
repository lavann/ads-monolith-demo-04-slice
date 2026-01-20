# Migration Plan - Monolith to Microservices

## Executive Summary

This document outlines a safe, incremental migration from the Retail Monolith to a microservices architecture using the strangler fig pattern. The plan consists of discrete phases, each extracting a single service while maintaining full system functionality. The **Products Catalog Service** is chosen as the first slice due to its low risk profile and clear boundaries.

## Migration Strategy

### Strangler Fig Pattern
The strangler fig pattern allows gradual migration by:
1. Creating new services alongside the monolith
2. Routing select traffic to new services via feature flags
3. Validating new services against monolith behavior
4. Gradually increasing traffic to new services
5. Decommissioning monolith components only when fully replaced

**Key Principle**: The monolith and microservices coexist, never a big-bang cutover.

### Guiding Principles
- ✅ **Incremental**: One service at a time
- ✅ **Reversible**: Each phase can be rolled back instantly
- ✅ **Validated**: New services validated against monolith before full cutover
- ✅ **No Downtime**: Zero customer-facing disruption
- ✅ **Data Safety**: No data loss during migration
- ✅ **Feature Parity**: New services match monolith functionality exactly

---

## Phase 0: Foundation (Preparation)

**Objective**: Establish infrastructure and patterns before extracting services

**Duration**: 1-2 weeks

### Tasks

#### 0.1 Set Up Containerization
- [ ] Create Dockerfile for monolith (baseline)
- [ ] Create docker-compose.yml with monolith + SQL Server
- [ ] Verify monolith runs in container
- [ ] Document container build and run process

**Success Criteria**: Monolith runs in Docker with LocalDB replaced by SQL Server container

**Rollback**: Continue running monolith locally

---

#### 0.2 Establish API Gateway (Routing Layer)
- [ ] Set up YARP reverse proxy project (see ADR-006)
- [ ] Configure routes to monolith as baseline
  ```
  /api/* → Monolith
  /Products → Monolith
  /Cart → Monolith
  /Checkout → Monolith
  /Orders → Monolith
  ```
- [ ] Add health check endpoint to gateway
- [ ] Test routing matches existing behavior

**Success Criteria**: All requests route through gateway to monolith with no functional changes

**Rollback**: Direct traffic back to monolith, remove gateway

---

#### 0.3 Implement Feature Flags
- [ ] Add feature flag configuration to `appsettings.json`
  ```json
  {
    "FeatureFlags": {
      "UseProductsService": false,
      "UseInventoryService": false,
      "UseCartService": false,
      "UseCheckoutService": false
    }
  }
  ```
- [ ] Create `FeatureFlagService` to check flags at runtime
- [ ] Integrate flags into routing logic (conditionals for service calls)

**Success Criteria**: Feature flags control routing without code deployment

**Rollback**: N/A (flags default to false = monolith)

---

#### 0.4 Add Correlation ID Middleware
- [ ] Implement `X-Correlation-ID` header propagation
- [ ] Add correlation ID to all log entries
- [ ] Test correlation ID flows through monolith

**Success Criteria**: Logs contain correlation IDs for request tracing

**Rollback**: Remove middleware (non-breaking)

---

#### 0.5 Fix Existing Bugs
Before extracting services, fix known issues in monolith:

- [ ] **Fix duplicate cart add logic** in `Products/Index.cshtml.cs` (LLD line 397-402)
  - Remove manual CartLine.Add, keep only service call
- [ ] **Fix nullable warnings** in models (LLD line 507-510)
  - Mark properties as `required` or nullable

**Success Criteria**: Build succeeds with zero warnings, cart operations work correctly

**Rollback**: N/A (bug fixes improve stability)

---

#### 0.6 Add Integration Tests for Monolith
- [ ] Create end-to-end tests for key flows:
  - Browse products → Add to cart → View cart
  - Checkout API → Order creation → Inventory deduction
- [ ] Tests become regression suite for validating services

**Success Criteria**: Test suite passes against monolith

**Rollback**: N/A (tests are non-invasive)

---

### Phase 0 Deliverables
- ✅ Monolith running in Docker
- ✅ API Gateway routing to monolith
- ✅ Feature flags infrastructure ready
- ✅ Correlation ID tracing enabled
- ✅ Bug fixes applied
- ✅ Integration test suite baseline

### Phase 0 Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Docker setup complexity | Medium | Use simple compose file, test locally first |
| Gateway introduces latency | Low | Benchmark before/after, YARP is fast |
| Feature flags add code complexity | Low | Keep logic simple, use helper methods |

---

## Phase 1: Extract Products Catalog Service

**Objective**: Create independent Products Service and route read traffic to it

**Duration**: 2-3 weeks

### Why Products First?

**Rationale**:
1. **Read-Heavy**: Minimal write operations, low risk
2. **No Transaction Coordination**: Products don't participate in checkout transactions
3. **Clear Boundary**: Product is a well-defined aggregate
4. **High Value**: Powers product discovery, key customer touchpoint
5. **Low Coupling**: Cart and Orders reference products by SKU only
6. **Testable**: Easy to compare service output vs monolith

**Alternative Considered**: Inventory Service
- **Rejected**: Inventory has complex concurrency requirements (reservations, commits)
- **Better as Phase 2**: After patterns established with Products

---

### Phase 1 Tasks

#### 1.1 Create Products Service Project
- [ ] Create new ASP.NET Core Web API project: `ProductsService`
- [ ] Add Entity Framework Core with `ProductsDbContext`
- [ ] Define `Product` model (copy from monolith)
- [ ] Create Dockerfile for Products Service
- [ ] Add Products Service to `docker-compose.yml`

**Directory Structure**:
```
/services/ProductsService/
  ProductsService.csproj
  Program.cs
  Controllers/
    ProductsController.cs
  Models/
    Product.cs
  Data/
    ProductsDbContext.cs
  Dockerfile
```

**Success Criteria**: Products Service builds and runs in container

---

#### 1.2 Implement Products API Endpoints
- [ ] `GET /api/products` - List all active products
  - Filter: `IsActive == true`
  - Returns: `List<ProductDto>`
- [ ] `GET /api/products/{id}` - Get product by ID
  - Returns: `ProductDto` or 404
- [ ] `GET /api/products/by-sku/{sku}` - Get product by SKU
  - Returns: `ProductDto` or 404
- [ ] Add health check: `GET /health`

**ProductDto**:
```csharp
public record ProductDto(
    int Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    string? Category
);
```

**Success Criteria**: API endpoints return correct data from database

---

#### 1.3 Connect Service to Shared Database
**Initial Approach**: Products Service connects to same `RetailMonolith` database

**Connection String**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver;Database=RetailMonolith;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True"
  }
}
```

**DbContext Scope**: Only map `Products` table
```csharp
public class ProductsDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Only include Products table
        builder.Entity<Product>().ToTable("Products");
    }
}
```

**Success Criteria**: Products Service reads from shared DB without affecting monolith

**Note**: This is a transitional state. Phase 4 will extract Products to dedicated DB.

---

#### 1.4 Create Products Service Client in Monolith
- [ ] Add `IProductsServiceClient` interface
- [ ] Implement `ProductsServiceClient` using HttpClient
- [ ] Configure HttpClient with Polly policies:
  - Retry: 3 attempts with exponential backoff
  - Circuit breaker: Open after 5 failures, 30s break
  - Timeout: 10 seconds

**Example**:
```csharp
public interface IProductsServiceClient
{
    Task<List<ProductDto>> GetProductsAsync();
    Task<ProductDto?> GetProductByIdAsync(int id);
    Task<ProductDto?> GetProductBySkuAsync(string sku);
}

// In Program.cs
builder.Services.AddHttpClient<IProductsServiceClient, ProductsServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceDiscovery:ProductsService"]);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(500)))
.AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
```

**Success Criteria**: Monolith can call Products Service successfully

---

#### 1.5 Implement Dual-Read Pattern
Modify `Products/Index.cshtml.cs` to support both monolith DB and Products Service:

```csharp
public async Task OnGetAsync()
{
    if (_config.GetValue<bool>("FeatureFlags:UseProductsService"))
    {
        // Call Products Service
        Products = await _productsClient.GetProductsAsync();
    }
    else
    {
        // Fallback to monolith DB
        Products = await _db.Products.Where(p => p.IsActive).ToListAsync();
    }
}
```

**Success Criteria**: Razor Page works with both monolith and service

---

#### 1.6 Configure API Gateway Routing
Add conditional routing in API Gateway:

```json
{
  "ReverseProxy": {
    "Routes": {
      "products-service-route": {
        "ClusterId": "products-cluster",
        "Match": {
          "Path": "/api/products/{**catch-all}"
        }
      },
      "monolith-fallback": {
        "ClusterId": "monolith-cluster",
        "Match": {
          "Path": "/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "products-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "http://products-service:5000"
          }
        }
      },
      "monolith-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "http://monolith:5000"
          }
        }
      }
    }
  }
}
```

**Success Criteria**: `/api/products` routes to Products Service when enabled

---

#### 1.7 Validation Phase (Shadow Mode)
Run both monolith and Products Service in parallel:

**Test Plan**:
- [ ] Products Service returns same data as monolith DB query
- [ ] Compare response times (should be similar)
- [ ] Test with feature flag ON and OFF
- [ ] Verify health checks pass
- [ ] Check logs for errors or timeouts
- [ ] Load test: 100 req/sec for 5 minutes

**Comparison Tool**:
```csharp
// Compare outputs
var monolithProducts = await _db.Products.Where(p => p.IsActive).ToListAsync();
var serviceProducts = await _productsClient.GetProductsAsync();

var match = monolithProducts.Count == serviceProducts.Count &&
            monolithProducts.All(m => serviceProducts.Any(s => s.Sku == m.Sku && s.Price == m.Price));

if (!match)
{
    _logger.LogWarning("Products Service output diverges from monolith!");
}
```

**Success Criteria**: 100% output parity between service and monolith

---

#### 1.8 Gradual Traffic Shift
Enable Products Service progressively:

**Week 1**: 
- Feature flag: `UseProductsService = false`
- Monitor metrics, no traffic to service

**Week 2**: 
- Feature flag: `UseProductsService = true` (dev environment only)
- Dev team tests, monitor logs

**Week 3**: 
- Feature flag: `UseProductsService = true` (staging/QA)
- QA team tests, validate integration

**Week 4**: 
- Feature flag: `UseProductsService = true` (10% production traffic)
- Monitor error rates, latency, availability

**Week 5**: 
- Increase to 50% production traffic
- Continue monitoring

**Week 6**: 
- Increase to 100% production traffic
- Products Service is primary

**Success Criteria**: No customer-facing issues, metrics within SLA

---

#### 1.9 Decommission Monolith Product Code (Optional)
Once Products Service handles 100% traffic:

- [ ] Remove Products DbSet from monolith AppDbContext (after validation period)
- [ ] Remove Products Razor Page from monolith (redirect to frontend)
- [ ] Keep Products table in monolith DB (for Phase 4 data extraction)

**Success Criteria**: Monolith no longer serves product data

**Note**: This step is optional. Keeping monolith code as fallback is acceptable.

---

### Phase 1 Deliverables
- ✅ Products Service deployed and running
- ✅ API Gateway routes `/api/products` to service
- ✅ Feature flag controls traffic routing
- ✅ 100% traffic to Products Service
- ✅ Monolith product code decommissioned (optional)
- ✅ Integration tests updated for Products Service

---

### Phase 1 Risks & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Products Service fails on startup | High | Low | Health checks prevent traffic routing; monolith fallback |
| Database connection issues | High | Medium | Connection pooling, retry policies, shared DB minimizes risk |
| Service returns stale data | Medium | Low | Both read from same DB in Phase 1 |
| Performance degradation | Medium | Low | Benchmark before/after; YARP adds ~5ms latency |
| Circuit breaker trips frequently | Medium | Medium | Tune thresholds; monitor failure rates; improve service reliability |
| Feature flag misconfiguration | High | Low | Default to false (monolith); test flag toggling in dev first |

---

### Phase 1 Rollback Plan

**Scenario**: Products Service exhibits unexpected behavior in production

**Rollback Steps** (< 5 minutes):
1. Set feature flag: `UseProductsService = false`
2. Restart monolith (to reload config) or update config service
3. Verify traffic routes to monolith
4. Monitor monolith metrics to confirm stability
5. Investigate Products Service offline

**Data Implications**: None. Both service and monolith read from same DB.

**Communication**: Notify team via Slack/email, update status page if customer-facing

---

## Phase 2: Extract Inventory Service

**Objective**: Create Inventory Service for stock management

**Duration**: 3-4 weeks

**Rationale**: After Products, Inventory is the next logical extraction. It has clear boundaries but introduces complexity around concurrency and transactions.

### Phase 2 Tasks

#### 2.1 Create Inventory Service Project
- [ ] Create ASP.NET Core Web API: `InventoryService`
- [ ] Define `InventoryItem` model
- [ ] Implement `InventoryDbContext` (maps to `Inventory` table)
- [ ] Add to docker-compose

---

#### 2.2 Implement Inventory API Endpoints
- [ ] `GET /api/inventory/{sku}` - Get stock level
- [ ] `POST /api/inventory/reserve` - Reserve quantity
  - Input: `{ Sku, Quantity, ReservationId }`
  - Output: `{ Success, Available }`
- [ ] `POST /api/inventory/release` - Release reservation
  - Input: `{ ReservationId }`
- [ ] `POST /api/inventory/commit` - Commit reservation (deduct stock)
  - Input: `{ ReservationId }`
- [ ] Add health check

---

#### 2.3 Implement Saga Pattern in Checkout Service
Modify `CheckoutService` to use Inventory Service APIs:

```csharp
// 1. Reserve inventory
var reservationId = Guid.NewGuid().ToString();
foreach (var line in cart.Lines)
{
    var reserved = await _inventoryClient.ReserveStock(line.Sku, line.Quantity, reservationId);
    if (!reserved)
    {
        // Rollback: Release all previous reservations
        await _inventoryClient.ReleaseReservation(reservationId);
        throw new Exception("Insufficient stock");
    }
}

// 2. Process payment
var paymentResult = await _paymentGateway.ChargeAsync(...);

if (paymentResult.Succeeded)
{
    // 3. Commit reservation
    await _inventoryClient.CommitReservation(reservationId);
}
else
{
    // 4. Rollback: Release reservation
    await _inventoryClient.ReleaseReservation(reservationId);
    throw new Exception("Payment failed");
}
```

**Success Criteria**: Checkout coordinates with Inventory Service

---

#### 2.4 Test Concurrency Scenarios
- [ ] Test: Two checkouts for same product simultaneously
  - Expected: One succeeds, one fails with "out of stock"
- [ ] Test: Reserve → timeout → release
  - Expected: Stock returned to available pool
- [ ] Load test: 50 concurrent checkouts

**Success Criteria**: No overselling, inventory counts accurate

---

#### 2.5 Enable Feature Flag and Validate
- [ ] Set `UseInventoryService = true` in dev
- [ ] Compare inventory updates: monolith vs service
- [ ] Gradual rollout (10% → 50% → 100%)

**Success Criteria**: Inventory operations match monolith behavior

---

### Phase 2 Deliverables
- ✅ Inventory Service deployed
- ✅ Checkout Service uses Inventory API
- ✅ Saga pattern handles reservations and rollbacks
- ✅ 100% traffic to Inventory Service

---

### Phase 2 Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Race condition on stock reservation | High | Optimistic concurrency control (EF Core), unique reservation IDs |
| Orphaned reservations (payment timeout) | Medium | Implement reservation expiry (TTL), background cleanup job |
| Distributed transaction failure | High | Saga pattern with explicit compensating transactions |
| Inventory Service downtime | High | Circuit breaker, fallback to monolith, retry logic |

---

### Phase 2 Rollback Plan
1. Set `UseInventoryService = false`
2. Checkout reverts to monolith's inline inventory logic
3. Monitor for double-reservation issues (unlikely if rollback is fast)

---

## Phase 3: Extract Cart Service

**Objective**: Create Cart Service for shopping cart management

**Duration**: 2-3 weeks

**Rationale**: Cart is stateful and session-based. Extracting it enables future optimizations (e.g., Redis for caching).

### Phase 3 Tasks

#### 3.1 Create Cart Service Project
- [ ] Create ASP.NET Core Web API: `CartService`
- [ ] Define `Cart` and `CartLine` models
- [ ] Implement `CartDbContext`
- [ ] Add to docker-compose

---

#### 3.2 Implement Cart API Endpoints
- [ ] `GET /api/carts/{customerId}` - Get or create cart
- [ ] `POST /api/carts/{customerId}/items` - Add item
  - Input: `{ ProductId, Quantity }`
  - Fetch product details from Products Service
- [ ] `PUT /api/carts/{customerId}/items/{sku}` - Update quantity
- [ ] `DELETE /api/carts/{customerId}/items/{sku}` - Remove item
- [ ] `DELETE /api/carts/{customerId}` - Clear cart
- [ ] Add health check

---

#### 3.3 Update Frontend to Use Cart Service
Modify `Cart/Index.cshtml.cs`:

```csharp
if (_config.GetValue<bool>("FeatureFlags:UseCartService"))
{
    var cart = await _cartClient.GetCartAsync("guest");
    Lines = cart.Lines.Select(l => (l.Name, l.Quantity, l.UnitPrice)).ToList();
}
else
{
    var cart = await _cartService.GetCartWithLinesAsync("guest");
    Lines = cart.Lines.Select(l => (l.Name, l.Quantity, l.UnitPrice)).ToList();
}
```

---

#### 3.4 Update Checkout Service to Use Cart Service
Modify `CheckoutService` to call Cart API instead of querying DB:

```csharp
var cart = await _cartClient.GetCartAsync(customerId);
if (cart == null || !cart.Lines.Any())
    throw new Exception("Cart is empty");

// Proceed with checkout...
```

---

#### 3.5 Enable Feature Flag and Validate
- [ ] Set `UseCartService = true` in dev
- [ ] Test add to cart, update quantity, remove, clear
- [ ] Gradual rollout

**Success Criteria**: Cart operations match monolith behavior

---

### Phase 3 Deliverables
- ✅ Cart Service deployed
- ✅ Frontend and Checkout use Cart API
- ✅ 100% traffic to Cart Service

---

### Phase 3 Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Session loss (cart disappears) | High | Persist carts to DB, not in-memory; test cart persistence |
| Stale product prices in cart | Medium | Accept temporary inconsistency; cart refreshes price on checkout |
| Cart Service downtime | High | Circuit breaker, fallback to monolith |

---

### Phase 3 Rollback Plan
1. Set `UseCartService = false`
2. Frontend and Checkout revert to monolith cart logic
3. Carts created in service remain in DB (no data loss)

---

## Phase 4: Data Isolation (Database per Service)

**Objective**: Move each service to its own dedicated database

**Duration**: 2-3 weeks

**Rationale**: Complete service independence. Each service owns its data and schema, enabling true decoupling.

### Phase 4 Tasks

#### 4.1 Create Dedicated Databases
- [ ] Create `ProductsCatalog` database
- [ ] Create `Inventory` database
- [ ] Create `CartService` database
- [ ] Update SQL Server container to include all databases

```sql
CREATE DATABASE ProductsCatalog;
CREATE DATABASE Inventory;
CREATE DATABASE CartService;
```

---

#### 4.2 Data Migration: Products
- [ ] Export `Products` table from `RetailMonolith` DB
  ```sql
  SELECT * INTO ProductsCatalog.dbo.Products FROM RetailMonolith.dbo.Products;
  ```
- [ ] Update Products Service connection string to point to `ProductsCatalog`
- [ ] Verify data integrity (row counts match)
- [ ] Test Products Service APIs
- [ ] Drop `Products` table from `RetailMonolith` (after validation)

**Rollback**: Revert connection string to `RetailMonolith` DB

---

#### 4.3 Data Migration: Inventory
- [ ] Export `Inventory` table
- [ ] Update Inventory Service connection string
- [ ] Test inventory operations
- [ ] Drop `Inventory` table from `RetailMonolith`

---

#### 4.4 Data Migration: Cart
- [ ] Export `Carts` and `CartLines` tables
- [ ] Update Cart Service connection string
- [ ] Test cart operations
- [ ] Drop cart tables from `RetailMonolith`

---

#### 4.5 Handle Cross-Service Data Access
**Challenge**: Cart and Orders store product name/price, but Products now in separate DB

**Solution**: Accept data denormalization
- Cart and Orders cache product name/price at time of adding/checkout
- Products Service is source of truth for current price
- Historical orders show price at time of purchase (correct behavior)

**No Action Needed**: Current code already captures price in CartLine/OrderLine

---

### Phase 4 Deliverables
- ✅ Products Service uses dedicated `ProductsCatalog` DB
- ✅ Inventory Service uses dedicated `Inventory` DB
- ✅ Cart Service uses dedicated `CartService` DB
- ✅ Data integrity verified
- ✅ Monolith DB cleaned up (optional)

---

### Phase 4 Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Data export corruption | High | Verify checksums, row counts; test exports in dev first |
| Schema drift during migration | Medium | Freeze schema changes; coordinate deployments |
| Connection string misconfiguration | High | Test in dev first; gradual rollout |
| Cross-database joins break | Low | Already avoided by using SKU as reference |

---

### Phase 4 Rollback Plan
1. Revert connection strings to `RetailMonolith` DB
2. Data remains in both places (old and new DB) during transition
3. Drop new databases after confirmed rollback

---

## Phase 5: Extract Checkout & Orders Service (Optional)

**Objective**: Create Orders Service for order management and checkout orchestration

**Duration**: 3-4 weeks

**Rationale**: Complete service extraction. Monolith is fully strangled.

### Phase 5 Tasks

#### 5.1 Create Orders Service
- [ ] Create ASP.NET Core Web API: `OrdersService`
- [ ] Define `Order` and `OrderLine` models
- [ ] Implement `OrdersDbContext`
- [ ] Move `CheckoutService` logic into Orders Service

---

#### 5.2 Implement Orders API Endpoints
- [ ] `POST /api/checkout` - Process checkout (move from monolith)
- [ ] `GET /api/orders/{id}` - Get order details
- [ ] `GET /api/orders/customer/{customerId}` - Get customer orders
- [ ] `PUT /api/orders/{id}/status` - Update order status
- [ ] Add health check

---

#### 5.3 Orchestrate Saga in Orders Service
Orders Service coordinates:
1. Call Cart Service to get cart
2. Call Inventory Service to reserve stock
3. Call Payment Gateway to charge
4. Create order in Orders DB
5. Call Cart Service to clear cart

**Success Criteria**: Checkout works end-to-end through Orders Service

---

#### 5.4 Enable Feature Flag and Validate
- [ ] Set `UseCheckoutService = true`
- [ ] Test checkout flow
- [ ] Gradual rollout

---

#### 5.5 Data Migration: Orders
- [ ] Export `Orders` and `OrderLines` tables
- [ ] Update Orders Service connection string to `OrdersService` DB
- [ ] Test order retrieval and creation
- [ ] Drop order tables from monolith

---

### Phase 5 Deliverables
- ✅ Orders Service deployed
- ✅ Checkout orchestration in Orders Service
- ✅ Orders Service uses dedicated `OrdersService` DB
- ✅ 100% traffic to Orders Service
- ✅ Monolith fully strangled (no business logic remaining)

---

### Phase 5 Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Saga failure leaves inconsistent state | High | Implement compensating transactions, idempotency, retry logic |
| Payment processed but order not created | High | Persist payment result before creating order; retry order creation |
| Checkout latency increases | Medium | Optimize service-to-service calls; use async where possible |

---

### Phase 5 Rollback Plan
1. Set `UseCheckoutService = false`
2. Monolith handles checkout again
3. Orders created in service remain in DB

---

## Phase 6: Decommission Monolith (Optional)

**Objective**: Fully retire the monolith application

**Duration**: 1-2 weeks

**Rationale**: Simplify architecture, reduce operational overhead

### Phase 6 Tasks

#### 6.1 Verify All Traffic Routed to Services
- [ ] Confirm feature flags: All = true
- [ ] Monitor for any monolith API calls (should be zero)
- [ ] Review logs for monolith activity

---

#### 6.2 Archive Monolith Code
- [ ] Tag monolith code in Git: `monolith-final`
- [ ] Move monolith to `/archive` directory
- [ ] Remove monolith from docker-compose

---

#### 6.3 Drop Monolith Database (After Extended Validation)
- [ ] Backup `RetailMonolith` database (retain for 1 year)
- [ ] Drop database after 3-month validation period
- [ ] Document data locations for compliance

---

### Phase 6 Deliverables
- ✅ Monolith application removed from deployment
- ✅ Monolith DB archived and dropped
- ✅ Microservices fully operational
- ✅ Architecture documented

---

## Post-Migration Enhancements

After successful migration, consider these improvements:

### 7.1 Introduce Event-Driven Communication
- Replace synchronous service calls with asynchronous events
- Implement message broker (RabbitMQ, Azure Service Bus)
- Publish events: `OrderCreated`, `InventoryReserved`, `PaymentProcessed`

---

### 7.2 Implement CQRS (Command Query Responsibility Segregation)
- Separate read and write models for scalability
- Use read replicas for queries
- Optimize read models for UI needs

---

### 7.3 Add Authentication & Authorization
- Replace "guest" hardcoded user with real authentication
- Integrate OAuth 2.0 / OpenID Connect
- Implement JWT token validation in API Gateway

---

### 7.4 Optimize Data Stores
- **Cart Service**: Migrate to Redis for session-based carts
- **Products Service**: Add Elasticsearch for search and filtering
- **Orders Service**: Consider document DB (CosmosDB) for flexible schema

---

### 7.5 Implement Distributed Tracing
- Add OpenTelemetry to all services
- Visualize request flows across services
- Use Jaeger or Azure Application Insights

---

### 7.6 Add API Versioning
- Support `/api/v1/products` and `/api/v2/products` simultaneously
- Deprecate old versions gracefully
- Document version lifecycle

---

## Success Metrics

### Technical Metrics
- **Deployment Frequency**: Each service deployable independently
- **Lead Time**: Changes deployed within 1 hour
- **MTTR**: Mean time to recovery < 15 minutes (feature flag rollback)
- **Change Failure Rate**: < 5% of deployments require rollback

### Performance Metrics
- **API Latency**: p95 < 500ms (gateway → service → response)
- **Service Availability**: 99.9% uptime per service
- **Database Connection Pool**: < 70% utilization

### Business Metrics
- **Zero Downtime**: No customer-facing outages during migration
- **Feature Parity**: 100% of monolith features available in services
- **Order Success Rate**: Maintains baseline (e.g., 98%)

---

## Risk Summary

### Overall Risk Assessment

| Phase | Risk Level | Reason |
|-------|-----------|---------|
| Phase 0: Foundation | Low | Infrastructure setup, non-invasive |
| Phase 1: Products Service | Low | Read-only, easy rollback |
| Phase 2: Inventory Service | Medium | Concurrency challenges |
| Phase 3: Cart Service | Low | Stateful but simple |
| Phase 4: Data Isolation | Medium | Data migration complexity |
| Phase 5: Orders Service | High | Orchestration complexity |
| Phase 6: Decommission | Low | Cleanup activity |

### Global Mitigation Strategies
- Feature flags enable instant rollback
- Shadow mode validates services before cutover
- Gradual rollout (10% → 50% → 100%) reduces blast radius
- Shared database in early phases minimizes data migration risk
- Saga pattern ensures transactional consistency across services

---

## Timeline Summary

| Phase | Duration | Dependencies |
|-------|----------|--------------|
| Phase 0: Foundation | 1-2 weeks | None |
| Phase 1: Products Service | 2-3 weeks | Phase 0 complete |
| Phase 2: Inventory Service | 3-4 weeks | Phase 1 complete |
| Phase 3: Cart Service | 2-3 weeks | Phase 1 complete |
| Phase 4: Data Isolation | 2-3 weeks | Phases 1-3 complete |
| Phase 5: Orders Service | 3-4 weeks | Phase 4 complete |
| Phase 6: Decommission | 1-2 weeks | Phase 5 complete |
| **Total** | **14-21 weeks** | **3.5 - 5 months** |

**Note**: Phases 1-3 can partially overlap (e.g., start Cart while Products is in gradual rollout)

---

## Conclusion

This migration plan provides a safe, incremental path from monolith to microservices. By following the strangler fig pattern and starting with the low-risk Products Catalog Service, we establish patterns and infrastructure that de-risk later phases.

Key success factors:
- ✅ **Products as First Slice**: Low risk, high learning value
- ✅ **Feature Flags**: Instant rollback capability
- ✅ **Shared Database Initially**: Minimizes data migration complexity early
- ✅ **Gradual Rollout**: Reduces blast radius of issues
- ✅ **Database per Service Eventually**: Achieves full service independence

Each phase is independently valuable and reversible, ensuring the migration can proceed at a sustainable pace without disrupting business operations.

---

## References
- `/docs/Target-Architecture.md` - Detailed target architecture
- `/docs/HLD.md` - Current monolith high-level design
- `/docs/LLD.md` - Current monolith implementation details
- `/docs/ADR/005-container-orchestration.md` - Orchestration decision
- `/docs/ADR/006-api-gateway.md` - Gateway technology choice
- `/docs/ADR/007-strangler-fig-pattern.md` - Migration pattern decision
