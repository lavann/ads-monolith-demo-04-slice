# Target Architecture - Retail Microservices

## Executive Summary

This document defines the target architecture for decomposing the Retail Monolith into a microservices-based system. The architecture follows a strangler fig pattern, enabling incremental migration while maintaining system stability. The target state consists of independently deployable services with clear boundaries, containerized deployment, and API-based communication.

## Design Principles

### Core Tenets
1. **Domain-Driven Decomposition**: Service boundaries follow business domains
2. **Independent Deployability**: Each service can be deployed without affecting others
3. **Database per Service**: Each service owns its data and schema
4. **API-First Communication**: Services communicate via HTTP/REST APIs
5. **Backward Compatibility**: Maintain existing functionality throughout migration
6. **Observability**: Built-in health checks, logging, and monitoring
7. **Container-Native**: All components run in containers for portability

### Migration Principles
- **Incremental extraction**: One service at a time
- **No big-bang rewrite**: Monolith and services coexist
- **Risk mitigation**: Each phase is independently reversible
- **Feature parity**: New services must match monolith behavior
- **Data consistency**: Handle distributed data challenges explicitly

---

## Proposed Service Boundaries

### 1. Products Catalog Service
**Domain Responsibility**: Product information management

**Capabilities**:
- Product listing and search
- Product details retrieval
- Product catalog management (future: admin operations)
- Category management

**Data Ownership**:
- `Products` table (Sku, Name, Description, Price, Currency, Category, IsActive)

**API Endpoints**:
- `GET /api/products` - List all active products
- `GET /api/products/{id}` - Get product by ID
- `GET /api/products/by-sku/{sku}` - Get product by SKU
- `GET /api/products/categories` - List categories

**Rationale for First Slice**:
- **Read-heavy**: Minimal write operations reduce migration risk
- **Low coupling**: No dependencies on other domains (cart/orders reference it by SKU)
- **Clear boundary**: Product is a well-defined aggregate
- **High value**: Powers product browsing, the entry point for customers
- **Minimal complexity**: No transaction coordination needed

**Technology Stack**:
- ASP.NET Core 8 Web API
- Entity Framework Core
- SQL Server (initially shared, later dedicated database)

---

### 2. Inventory Service
**Domain Responsibility**: Stock level management

**Capabilities**:
- Query inventory levels by SKU
- Reserve inventory during checkout
- Release reserved inventory (on checkout failure)
- Update inventory after fulfillment

**Data Ownership**:
- `Inventory` table (Sku, Quantity, Reserved)

**API Endpoints**:
- `GET /api/inventory/{sku}` - Get stock level
- `POST /api/inventory/reserve` - Reserve quantity for checkout
- `POST /api/inventory/release` - Release reserved quantity
- `POST /api/inventory/commit` - Commit reservation (deduct stock)

**Integration Points**:
- Called by Checkout Service during order processing
- Provides inventory status to Cart Service for availability display

**Technology Stack**:
- ASP.NET Core 8 Web API
- Entity Framework Core
- SQL Server with optimistic concurrency

**Considerations**:
- **Consistency**: Implement saga pattern or 2-phase commit for distributed transactions
- **Concurrency**: Handle race conditions on stock reservation
- **Idempotency**: Support retry-safe operations

---

### 3. Cart Service
**Domain Responsibility**: Shopping cart management

**Capabilities**:
- Create/retrieve customer cart
- Add items to cart
- Update item quantities
- Remove items from cart
- Clear cart
- Calculate cart totals

**Data Ownership**:
- `Carts` table (Id, CustomerId)
- `CartLines` table (Id, CartId, Sku, Name, UnitPrice, Quantity)

**API Endpoints**:
- `GET /api/carts/{customerId}` - Get cart for customer
- `POST /api/carts/{customerId}/items` - Add item to cart
- `PUT /api/carts/{customerId}/items/{sku}` - Update item quantity
- `DELETE /api/carts/{customerId}/items/{sku}` - Remove item
- `DELETE /api/carts/{customerId}` - Clear cart

**Integration Points**:
- Queries Products Service for product details (name, price)
- Queries Inventory Service for stock availability (optional enhancement)
- Called by Checkout Service to retrieve cart contents

**Technology Stack**:
- ASP.NET Core 8 Web API
- Entity Framework Core
- SQL Server (candidate for Redis in future)

---

### 4. Orders & Checkout Service
**Domain Responsibility**: Order processing and payment

**Capabilities**:
- Process checkout for customer
- Create orders from cart
- Process payments via Payment Gateway
- Track order status
- Retrieve order history

**Data Ownership**:
- `Orders` table (Id, CustomerId, CreatedUtc, Status, Total)
- `OrderLines` table (Id, OrderId, Sku, Name, UnitPrice, Quantity)

**API Endpoints**:
- `POST /api/checkout` - Process checkout
- `GET /api/orders/{id}` - Get order details
- `GET /api/orders/customer/{customerId}` - Get customer orders
- `PUT /api/orders/{id}/status` - Update order status (fulfillment)

**Integration Points**:
- Calls Cart Service to retrieve cart
- Calls Inventory Service to reserve stock
- Calls Payment Gateway to process payment
- Publishes `OrderCreated` event (future)

**Technology Stack**:
- ASP.NET Core 8 Web API
- Entity Framework Core
- SQL Server

**Orchestration Pattern**:
- Implements saga pattern for distributed transaction
- Coordinates: Cart → Inventory → Payment → Order creation

---

### 5. Frontend / BFF (Backend for Frontend)
**Domain Responsibility**: User interface and experience

**Capabilities**:
- Razor Pages UI for customer-facing flows
- Aggregates calls to backend services
- Session management
- Client-side cart state (optional)

**API Integration**:
- Calls Products Service for catalog display
- Calls Cart Service for cart operations
- Calls Checkout Service for order processing
- Calls Orders Service for order history

**Technology Stack**:
- ASP.NET Core 8 Razor Pages
- HTTP Client for service calls
- Session state management

**Responsibilities**:
- View rendering
- User input validation
- Error handling and user feedback
- Composition of data from multiple services

---

### 6. API Gateway (Routing Layer)
**Domain Responsibility**: External API entry point and routing

**Capabilities**:
- Route requests to appropriate backend services
- API versioning support
- Rate limiting (future)
- Authentication/Authorization (future)
- Request/response logging

**Routing Rules**:
```
/api/products/*      → Products Service
/api/inventory/*     → Inventory Service
/api/carts/*         → Cart Service
/api/orders/*        → Orders Service
/api/checkout        → Checkout Service
```

**Technology Options**:
- **Option 1**: YARP (Yet Another Reverse Proxy) - .NET native
- **Option 2**: Nginx with reverse proxy configuration
- **Option 3**: Traefik for container-native routing
- **Decision**: See ADR-006

---

## Target Deployment Architecture

### Container Deployment Model

```
┌──────────────────────────────────────────────────────────────┐
│                        Load Balancer                         │
│                     (HTTPS Termination)                      │
└────────────────────────┬─────────────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         │                               │
    ┌────▼─────┐                   ┌────▼────────┐
    │   API    │                   │   Frontend  │
    │  Gateway │                   │  (Razor UI) │
    └────┬─────┘                   └────┬────────┘
         │                               │
    ┌────┴─────────────────────────┬────┴─────┬──────────┬──────────┐
    │                              │          │          │          │
┌───▼──────────┐  ┌────────────┐ ┌▼─────┐ ┌──▼────┐ ┌──▼───────┐ │
│   Products   │  │  Inventory │ │ Cart │ │ Orders│ │ Monolith │ │
│   Service    │  │   Service  │ │ Svc  │ │  Svc  │ │ (Compat) │ │
└───┬──────────┘  └────┬───────┘ └┬─────┘ └──┬────┘ └──┬───────┘ │
    │                  │           │          │         │         │
┌───▼──────────┐  ┌────▼───────┐ ┌▼─────┐ ┌──▼────┐ ┌──▼───────┐ │
│  Products DB │  │ Inventory  │ │ Cart │ │Orders │ │ Monolith │ │
│              │  │     DB     │ │  DB  │ │  DB   │ │    DB    │ │
└──────────────┘  └────────────┘ └──────┘ └───────┘ └──────────┘ │
                                                                   │
┌──────────────────────────────────────────────────────────────┐ │
│              External Dependencies                            │ │
│  ┌─────────────────┐          ┌─────────────────┐           │ │
│  │ Payment Gateway │          │   (Future)      │           │ │
│  │  (Mock/Stripe)  │          │ Message Queue   │           │ │
│  └─────────────────┘          └─────────────────┘           │ │
└──────────────────────────────────────────────────────────────┘ │
```

### Container Specifications

Each service runs as a Docker container with:

**Base Image**: `mcr.microsoft.com/dotnet/aspnet:8.0`

**Resource Limits** (initial):
- CPU: 0.5 core (burstable to 1)
- Memory: 512 MB (limit 1 GB)

**Environment Variables**:
```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<service-specific>
ServiceDiscovery__ProductsService=http://products-service:5000
ServiceDiscovery__InventoryService=http://inventory-service:5000
ServiceDiscovery__CartService=http://cart-service:5000
ServiceDiscovery__OrdersService=http://orders-service:5000
```

**Health Checks**:
- Liveness: `GET /health`
- Readiness: `GET /health/ready`

**Logging**:
- Structured logging to stdout
- JSON format for log aggregation
- Log level configurable via environment

**Example Dockerfile** (Products Service):
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ProductsService/ProductsService.csproj", "ProductsService/"]
RUN dotnet restore "ProductsService/ProductsService.csproj"
COPY . .
WORKDIR "/src/ProductsService"
RUN dotnet build "ProductsService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ProductsService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProductsService.dll"]
```

---

### Orchestration Options

#### Option 1: Docker Compose (Development & Demo)
**Pros**:
- Simple setup for local development
- Easy to understand and debug
- No cluster management overhead
- Suitable for demo environments

**Cons**:
- No production-grade orchestration
- Limited scaling capabilities
- Manual high-availability setup

**Use Case**: Development, testing, and proof-of-concept

**Example `docker-compose.yml`**:
```yaml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      SA_PASSWORD: "YourStrong@Passw0rd"
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql

  products-service:
    build:
      context: ./ProductsService
    ports:
      - "5001:5000"
    environment:
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=Products;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True"
    depends_on:
      - sqlserver
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  api-gateway:
    build:
      context: ./ApiGateway
    ports:
      - "5000:5000"
    environment:
      ServiceDiscovery__ProductsService: "http://products-service:5000"
    depends_on:
      - products-service

  frontend:
    build:
      context: ./Frontend
    ports:
      - "8080:5000"
    environment:
      ApiGateway__BaseUrl: "http://api-gateway:5000"
    depends_on:
      - api-gateway

volumes:
  sqldata:
```

---

#### Option 2: Kubernetes (Production)
**Pros**:
- Production-grade orchestration
- Auto-scaling and self-healing
- Rolling deployments
- Service discovery built-in
- Load balancing

**Cons**:
- Complex setup and learning curve
- Requires cluster infrastructure
- Overkill for small deployments

**Use Case**: Production environments, high availability

**Example Deployment** (Products Service):
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: products-service
spec:
  replicas: 3
  selector:
    matchLabels:
      app: products-service
  template:
    metadata:
      labels:
        app: products-service
    spec:
      containers:
      - name: products-service
        image: retail/products-service:latest
        ports:
        - containerPort: 5000
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: products-db-secret
              key: connectionString
        livenessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 5
        resources:
          requests:
            cpu: "500m"
            memory: "512Mi"
          limits:
            cpu: "1000m"
            memory: "1Gi"

---
apiVersion: v1
kind: Service
metadata:
  name: products-service
spec:
  selector:
    app: products-service
  ports:
  - protocol: TCP
    port: 80
    targetPort: 5000
  type: ClusterIP
```

**Decision**: For initial migration, use **Docker Compose**. See ADR-005.

---

## Communication Patterns

### 1. Synchronous HTTP/REST
**Use Case**: Request-response operations

**Pattern**: RESTful APIs with JSON payloads

**Examples**:
- Frontend queries Products Service for catalog
- Checkout Service calls Inventory Service to reserve stock
- Cart Service queries Products Service for current pricing

**Advantages**:
- Simple to implement and understand
- Built-in .NET support (HttpClient)
- Wide tooling support
- Request-response semantics match use cases

**Disadvantages**:
- Tight coupling at runtime
- Cascading failures if service unavailable
- Increased latency for multi-service calls

**Mitigation Strategies**:
- Circuit breaker pattern (Polly library)
- Retry with exponential backoff
- Timeout configuration
- Fallback responses

**Implementation**:
```csharp
// In Checkout Service
public class InventoryServiceClient
{
    private readonly HttpClient _httpClient;
    
    public InventoryServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<bool> ReserveStock(string sku, int quantity)
    {
        var request = new { Sku = sku, Quantity = quantity };
        var response = await _httpClient.PostAsJsonAsync("/api/inventory/reserve", request);
        return response.IsSuccessStatusCode;
    }
}

// In Program.cs
builder.Services.AddHttpClient<InventoryServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceDiscovery:InventoryService"]);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(500)))
.AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
```

---

### 2. Asynchronous Messaging (Future)
**Use Case**: Event-driven notifications, eventual consistency

**Pattern**: Publish-subscribe with message broker

**Examples** (future enhancements):
- `OrderCreated` event published by Checkout Service
- `InventoryReserved` event for audit trail
- `PaymentProcessed` event for downstream systems

**Technology Options**:
- Azure Service Bus
- RabbitMQ
- Apache Kafka
- AWS SQS/SNS

**Advantages**:
- Decouples services
- Resilient to temporary service unavailability
- Enables event sourcing
- Better scalability

**Disadvantages**:
- Adds complexity
- Requires message broker infrastructure
- Eventual consistency requires careful design
- Debugging distributed flows is harder

**Decision**: Defer to Phase 3+ of migration. Not required for initial extraction.

---

### 3. Data Access Approach

#### Phase 1: Shared Database (Transition)
During initial extraction, services access the same SQL Server database but only their designated tables.

**Isolation Mechanism**:
- Each service DbContext only includes its tables
- Database views or stored procedures for cross-domain queries
- Foreign keys remain but are treated as loose references

**Example** (Products Service):
```csharp
public class ProductsDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    
    // Does NOT include Inventory, Cart, Orders
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Product>().ToTable("Products");
    }
}
```

**Connection String**: Points to shared `RetailMonolith` database initially

---

#### Phase 2: Database per Service (Target)
Each service owns a dedicated database for full isolation.

**Products Service**: `ProductsCatalog` database
**Inventory Service**: `Inventory` database
**Cart Service**: `Cart` database
**Orders Service**: `Orders` database

**Data Synchronization**:
- **SKU as Foreign Key**: Services reference Product SKU (string), not Product ID
- **Caching**: Cart/Orders cache product name and price at time of action
- **Data Duplication**: Accept that product name/price may be denormalized

**Migration Steps**:
1. Change service connection strings to point to new databases
2. Export data from monolith tables
3. Import into service-specific databases
4. Verify data integrity
5. Switch service to new database
6. Drop tables from monolith (after validation period)

**Example**:
```sql
-- Before: All in RetailMonolith DB
RetailMonolith.dbo.Products
RetailMonolith.dbo.Inventory
RetailMonolith.dbo.Carts
RetailMonolith.dbo.Orders

-- After: Separate databases
ProductsCatalog.dbo.Products
Inventory.dbo.InventoryItems
CartService.dbo.Carts
CartService.dbo.CartLines
OrdersService.dbo.Orders
OrdersService.dbo.OrderLines
```

**Cross-Service Data Access**:
- **Read**: Call service API (e.g., Cart Service calls Products API for price)
- **Write**: Never write to another service's data
- **Consistency**: Accept eventual consistency or implement saga pattern

---

### 4. API Contracts and Versioning

**Versioning Strategy**: URL-based versioning

```
/api/v1/products
/api/v2/products
```

**Backward Compatibility**:
- Maintain old versions during transition
- Deprecate old versions with 6-month notice
- Frontend calls versioned endpoints explicitly

**Contract Definition**:
- OpenAPI/Swagger for API documentation
- Shared DTOs in a common contracts library (initially)
- Evolve to independent contracts per service

**Example DTO**:
```csharp
// Contracts/ProductDto.cs
public record ProductDto(
    int Id,
    string Sku,
    string Name,
    decimal Price,
    string Currency,
    string? Category
);
```

---

## Configuration Management

### Service Discovery
**Approach**: Configuration-based URLs (simple)

Each service registers its dependencies via `appsettings.json`:

```json
{
  "ServiceDiscovery": {
    "ProductsService": "http://products-service:5000",
    "InventoryService": "http://inventory-service:5000",
    "CartService": "http://cart-service:5000",
    "OrdersService": "http://orders-service:5000"
  }
}
```

**Future Enhancement**: Consul or Kubernetes DNS for dynamic discovery

---

### Secrets Management
**Development**: `appsettings.Development.json` or User Secrets

**Production Options**:
- Azure Key Vault
- Kubernetes Secrets
- HashiCorp Vault

**Database Connection Strings**:
- Stored as secrets
- Injected as environment variables
- Never committed to source control

---

### Feature Flags
**Purpose**: Enable/disable new service routing during migration

```json
{
  "FeatureFlags": {
    "UseProductsService": true,
    "UseInventoryService": false,
    "UseCartService": false
  }
}
```

**Implementation**:
```csharp
if (_config.GetValue<bool>("FeatureFlags:UseProductsService"))
{
    // Call Products Service
    return await _productsClient.GetProducts();
}
else
{
    // Fallback to monolith
    return await _db.Products.ToListAsync();
}
```

**Benefit**: Allows gradual rollout and instant rollback

---

## Observability and Monitoring

### Health Checks
**Standard Endpoints**:
- `/health` - Liveness (is service running?)
- `/health/ready` - Readiness (can service handle requests?)

**Implementation**:
```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ProductsDbContext>()
    .AddUrlGroup(new Uri("http://downstream-service/health"), "Downstream Service");

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
```

---

### Logging
**Approach**: Structured logging with correlation IDs

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddJsonConsole(); // For log aggregation
});
```

**Correlation ID Middleware**:
```csharp
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
                        ?? Guid.NewGuid().ToString();
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers.Add("X-Correlation-ID", correlationId);
    
    using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        await next();
    }
});
```

---

### Metrics (Future)
- Prometheus exporters for each service
- Metrics: request count, latency, error rate
- Grafana dashboards for visualization

---

### Distributed Tracing (Future)
- OpenTelemetry integration
- Trace requests across service boundaries
- Tools: Jaeger or Azure Application Insights

---

## Security Considerations

### Authentication & Authorization (Future)
**Current State**: Hardcoded "guest" user

**Target State**:
- OAuth 2.0 / OpenID Connect
- Identity Provider (Azure AD, Auth0, Keycloak)
- JWT tokens passed between services

**API Gateway Role**:
- Validates JWT tokens
- Extracts user claims
- Forwards identity to backend services

---

### Network Security
**Container Network**:
- Services communicate on private network
- Only API Gateway and Frontend exposed publicly

**TLS/HTTPS**:
- TLS termination at load balancer or API Gateway
- Internal service-to-service communication over HTTP (within container network)
- Consider mTLS for production

---

### Secrets Rotation
- Implement automated secret rotation
- Use managed identities where available (Azure, AWS)

---

## Data Migration Strategy

### Cross-Cutting Concern: Customer IDs
**Problem**: "guest" hardcoded everywhere

**Solution**: 
1. Introduce `CustomerId` as a string parameter in all APIs
2. Frontend passes customer ID from session/JWT
3. Services treat customer ID as opaque identifier

**Backward Compatibility**:
- Default to "guest" if not provided
- Gradually enforce authenticated customer IDs

---

### SKU as Natural Key
**Decision**: Use `Sku` (string) as cross-service reference, not database IDs

**Rationale**:
- SKU is business identifier
- Stable across systems
- Database IDs are internal implementation details

**Impact**:
- CartLines and OrderLines store SKU instead of Product foreign key
- Products Service is source of truth for SKU → Product mapping

---

## Rollback Strategy

Each migration phase must support instant rollback via:

1. **Feature Flags**: Turn off new service routing
2. **Dual Write**: Write to both monolith and new service temporarily
3. **Monolith Fallback**: Keep monolith running for all features
4. **Blue-Green Deployment**: Run both versions simultaneously

**Example Rollback** (Products Service extraction):
- Disable feature flag: `UseProductsService = false`
- Traffic immediately reverts to monolith
- No data loss (Products table still in monolith DB)
- New service can be debugged offline

---

## Non-Functional Requirements

### Performance
- **Latency Target**: < 500ms for API calls (p95)
- **Throughput**: Support 100 requests/second per service (initial)
- **Caching**: Implement response caching for read-heavy operations

### Scalability
- **Horizontal Scaling**: Services can scale independently
- **Database Connection Pooling**: Limit connections per instance
- **Stateless Services**: No in-memory session state

### Availability
- **Target**: 99.9% uptime for production
- **Health Checks**: Automated container restarts on failure
- **Circuit Breakers**: Fail fast and degrade gracefully

### Disaster Recovery
- **Database Backups**: Automated daily backups with 30-day retention
- **Point-in-Time Recovery**: 7-day window for SQL Server
- **Backup Testing**: Quarterly restore tests

---

## Technology Stack Summary

| Component | Technology | Version |
|-----------|-----------|---------|
| Services | ASP.NET Core Web API | 8.0 |
| ORM | Entity Framework Core | 9.0+ |
| Database | SQL Server | 2022+ |
| Frontend | ASP.NET Core Razor Pages | 8.0 |
| API Gateway | YARP (initial) | 2.0+ |
| Containerization | Docker | 24.0+ |
| Orchestration (dev) | Docker Compose | 2.20+ |
| Orchestration (prod) | Kubernetes (optional) | 1.28+ |
| HTTP Client | .NET HttpClient + Polly | Built-in + 8.0+ |
| Logging | ASP.NET Core Logging | Built-in |
| Health Checks | ASP.NET Core Health Checks | Built-in |

---

## Success Criteria

### Technical Metrics
- ✅ Each service deployable independently
- ✅ API response times < 500ms (p95)
- ✅ Services can scale independently (CPU < 70%)
- ✅ Zero downtime during service updates
- ✅ Health checks pass for all services

### Business Metrics
- ✅ Feature parity with monolith
- ✅ No customer-facing regressions
- ✅ Same or better checkout success rate
- ✅ Order accuracy maintained (100%)

### Migration Metrics
- ✅ Each phase completable within 2-week sprint
- ✅ Rollback time < 5 minutes
- ✅ Data consistency verified after each phase

---

## Future Enhancements (Post-Migration)

### Phase N+1
1. **Event-Driven Architecture**: Replace synchronous calls with events
2. **CQRS**: Separate read/write models for scalability
3. **Event Sourcing**: Audit trail and time-travel debugging
4. **API Versioning**: Support multiple API versions simultaneously
5. **GraphQL Gateway**: Unified query interface for frontend
6. **Service Mesh**: Istio/Linkerd for advanced traffic management
7. **Chaos Engineering**: Test resilience with deliberate failures

### Alternative Data Stores
- **Redis**: For Cart Service (session-based carts)
- **Elasticsearch**: For Products search and filtering
- **CosmosDB/MongoDB**: For Orders if document model is better

---

## Conclusion

This target architecture defines a pragmatic path from monolith to microservices. The design emphasizes:

- **Incremental migration** with low risk
- **Container-native** deployment for portability
- **Clear service boundaries** based on domain
- **API-first communication** for interoperability
- **Backward compatibility** to preserve existing functionality

The architecture supports the migration plan defined in `/docs/Migration-Plan.md`, with Products Catalog Service as the first extraction candidate.

---

## References
- `/docs/HLD.md` - Current monolith architecture
- `/docs/LLD.md` - Current implementation details
- `/docs/Migration-Plan.md` - Step-by-step migration plan
- `/docs/ADR/001-sql-server-localdb.md` - Database technology choice
- `/docs/ADR/002-service-layer-pattern.md` - Service abstraction rationale
- `/docs/ADR/005-container-orchestration.md` - Deployment orchestration decision
- `/docs/ADR/006-api-gateway.md` - API Gateway technology choice
- `/docs/ADR/007-strangler-fig-pattern.md` - Migration pattern decision
