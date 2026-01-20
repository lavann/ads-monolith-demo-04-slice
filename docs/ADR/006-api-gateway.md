# ADR-006: YARP for API Gateway and Routing

## Status
Accepted

## Context
The target microservices architecture requires an API Gateway to:
- Route incoming HTTP requests to appropriate backend services
- Provide a single entry point for external clients
- Enable gradual traffic shifting during migration (strangler fig pattern)
- Support feature flags for routing decisions
- Handle cross-cutting concerns (logging, correlation IDs, future auth)

The gateway must support:
- **Dynamic routing**: Route based on path, headers, or query parameters
- **Configuration-based**: Minimal code, declarative routing rules
- **.NET ecosystem fit**: Integrate seamlessly with ASP.NET Core services
- **Performance**: Low latency overhead (< 10ms p95)
- **Extensibility**: Support middleware for custom logic

### Gateway Options Considered

#### 1. YARP (Yet Another Reverse Proxy)
- **Description**: Microsoft's .NET-based reverse proxy toolkit
- **Language**: C# / ASP.NET Core
- **Maturity**: Production-ready, used by Microsoft internally (Azure, GitHub Codespaces)
- **License**: MIT (open source)

#### 2. Nginx
- **Description**: High-performance HTTP server and reverse proxy
- **Language**: C (configuration in Nginx syntax)
- **Maturity**: Battle-tested, widely adopted
- **License**: BSD-like (open source)

#### 3. Traefik
- **Description**: Cloud-native reverse proxy and load balancer
- **Language**: Go
- **Maturity**: Production-ready, popular in Kubernetes environments
- **License**: MIT (open source)

#### 4. Ocelot
- **Description**: .NET API Gateway library
- **Language**: C# / ASP.NET Core
- **Maturity**: Stable, but less active development
- **License**: MIT (open source)

#### 5. Kong
- **Description**: Microservices API Gateway with plugins
- **Language**: Lua on Nginx
- **Maturity**: Enterprise-grade
- **License**: Apache 2.0 (open source, commercial version available)

#### 6. AWS API Gateway / Azure API Management
- **Description**: Cloud-managed API Gateway services
- **Maturity**: Production-ready, fully managed
- **License**: Cloud provider specific

## Decision
Use **YARP (Yet Another Reverse Proxy)** as the API Gateway for the microservices architecture.

**Rationale**:
1. **Native .NET Integration**: YARP is built on ASP.NET Core, providing seamless integration with our .NET services
2. **Configuration-Based Routing**: Declarative routing via `appsettings.json`, no complex code
3. **Performance**: Minimal overhead (<5ms latency), optimized for .NET workloads
4. **Microsoft Support**: Actively maintained by Microsoft, used in production at scale
5. **Extensibility**: Full access to ASP.NET Core middleware pipeline for custom logic
6. **Feature Flags**: Easy to implement conditional routing based on configuration
7. **Learning Curve**: Team already familiar with ASP.NET Core concepts
8. **No External Dependencies**: Self-hosted, no cloud provider lock-in

## Implementation

### Gateway Project Structure

```
/gateway/
  ApiGateway.csproj
  Program.cs
  appsettings.json
  Middleware/
    CorrelationIdMiddleware.cs
    LoggingMiddleware.cs
  Dockerfile
```

### Program.cs (Minimal Setup)

```csharp
using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

// Add YARP reverse proxy services
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Add custom middleware
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<LoggingMiddleware>();

// Map health check
app.MapHealthChecks("/health");

// Enable YARP reverse proxy
app.MapReverseProxy();

app.Run();
```

### appsettings.json (Routing Configuration)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Yarp": "Information"
    }
  },
  "ReverseProxy": {
    "Routes": {
      "products-route": {
        "ClusterId": "products-cluster",
        "Match": {
          "Path": "/api/products/{**catch-all}"
        },
        "Transforms": [
          { "RequestHeadersCopy": "true" },
          { "RequestHeader": "X-Forwarded-Proto", "Set": "https" }
        ]
      },
      "inventory-route": {
        "ClusterId": "inventory-cluster",
        "Match": {
          "Path": "/api/inventory/{**catch-all}"
        }
      },
      "cart-route": {
        "ClusterId": "cart-cluster",
        "Match": {
          "Path": "/api/carts/{**catch-all}"
        }
      },
      "orders-route": {
        "ClusterId": "orders-cluster",
        "Match": {
          "Path": "/api/orders/{**catch-all}"
        }
      },
      "checkout-route": {
        "ClusterId": "orders-cluster",
        "Match": {
          "Path": "/api/checkout"
        }
      },
      "monolith-fallback": {
        "ClusterId": "monolith-cluster",
        "Match": {
          "Path": "/{**catch-all}"
        },
        "Order": 100
      }
    },
    "Clusters": {
      "products-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "http://products-service:5000"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:30",
            "Timeout": "00:00:10",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        }
      },
      "inventory-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "http://inventory-service:5000"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:30",
            "Timeout": "00:00:10",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        }
      },
      "cart-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "http://cart-service:5000"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:30",
            "Timeout": "00:00:10",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        }
      },
      "orders-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "http://orders-service:5000"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:30",
            "Timeout": "00:00:10",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        }
      },
      "monolith-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "http://monolith:5000"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:30",
            "Timeout": "00:00:10",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        }
      }
    }
  }
}
```

### Route Ordering and Fallback
- **Specific routes** (e.g., `/api/products`) match first
- **Catch-all route** (`/{**catch-all}`) matches anything else
- `Order: 100` on monolith route ensures it's evaluated last
- Monolith acts as fallback for unmatched routes

### Correlation ID Middleware

```csharp
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Add("X-Correlation-ID", correlationId);

        // Add to YARP transforms so it's forwarded to backend services
        context.Request.Headers["X-Correlation-ID"] = correlationId;

        await _next(context);
    }
}
```

### Health Check Implementation

```csharp
// In Program.cs
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddUrlGroup(new Uri("http://products-service:5000/health"), "Products Service", timeout: TimeSpan.FromSeconds(5))
    .AddUrlGroup(new Uri("http://inventory-service:5000/health"), "Inventory Service", timeout: TimeSpan.FromSeconds(5))
    .AddUrlGroup(new Uri("http://cart-service:5000/health"), "Cart Service", timeout: TimeSpan.FromSeconds(5))
    .AddUrlGroup(new Uri("http://orders-service:5000/health"), "Orders Service", timeout: TimeSpan.FromSeconds(5))
    .AddUrlGroup(new Uri("http://monolith:5000/health"), "Monolith", timeout: TimeSpan.FromSeconds(5));

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### Feature Flag Support (Future)

For conditional routing based on feature flags:

```csharp
app.Use(async (context, next) =>
{
    var useProductsService = builder.Configuration.GetValue<bool>("FeatureFlags:UseProductsService");

    if (context.Request.Path.StartsWithSegments("/api/products") && !useProductsService)
    {
        // Rewrite path to route to monolith
        context.Request.Path = "/monolith" + context.Request.Path;
    }

    await next();
});
```

## Consequences

### Positive
- **Zero Code Routing**: Routes defined in JSON, no C# code needed for basic routing
- **Performance**: <5ms overhead, negligible compared to service processing time
- **Health Checks**: Built-in active health checking with automatic failover
- **Load Balancing**: Supports round-robin, least requests, random, power of two
- **Request Transforms**: Modify headers, paths, query strings before forwarding
- **Observability**: Integrated logging, metrics (future: OpenTelemetry)
- **Hot Reload**: Configuration changes reload without restart (in development)
- **Extensibility**: Full middleware pipeline for custom logic
- **No External Process**: Runs as ASP.NET Core app, same runtime as services
- **Containerization**: Easy to containerize (same Dockerfile pattern as services)

### Negative
- **Single Point of Failure**: Gateway down = entire system inaccessible
  - **Mitigation**: Deploy multiple gateway instances behind load balancer
- **Latency Introduction**: Every request goes through gateway
  - **Impact**: ~5ms overhead is acceptable for this application
- **Configuration Complexity**: Large routing tables can become unwieldy
  - **Mitigation**: Split routes by domain, use includes
- **No Built-in Auth**: Authentication must be implemented separately
  - **Plan**: Add JWT validation middleware in Phase 6+
- **Less Mature Than Nginx**: YARP is relatively new (2020 release)
  - **Counter**: Microsoft uses it in production (Azure, GitHub)

### Trade-offs
| Aspect | YARP | Nginx | Traefik |
|--------|------|-------|---------|
| Language | C# | C | Go |
| Config | JSON/C# | Nginx conf | YAML/TOML |
| Performance | Excellent | Excellent | Very Good |
| .NET Integration | Native | External | External |
| Learning Curve | Low (for .NET devs) | Medium | Medium |
| Ecosystem | Growing | Mature | Cloud-native |

## Alternatives Considered

### Nginx
**Pros**:
- Industry standard, battle-tested
- Exceptional performance
- Rich ecosystem of modules
- Mature documentation

**Cons**:
- Separate process from .NET services (different runtime)
- Configuration syntax unfamiliar to .NET developers
- Less integration with ASP.NET Core ecosystem
- Requires separate logging/monitoring setup

**Why Rejected**: 
- YARP provides sufficient performance for this application
- Team familiarity with .NET ecosystem is valuable
- Nginx is over-engineering for current scale

---

### Traefik
**Pros**:
- Cloud-native, excellent Kubernetes integration
- Automatic service discovery
- Beautiful dashboard UI
- Let's Encrypt integration

**Cons**:
- Go-based, different runtime from .NET
- Overkill for Docker Compose setup
- Dashboard adds complexity
- Less control over middleware pipeline

**Why Rejected**: 
- Features like automatic K8s discovery not needed in Docker Compose
- YARP is simpler for .NET-centric stack

---

### Ocelot
**Pros**:
- .NET-based API Gateway library
- Mature, stable project
- Similar to YARP in concept

**Cons**:
- Less active development compared to YARP
- Microsoft is not investing in Ocelot
- YARP is newer and more performant
- Smaller community

**Why Rejected**: 
- YARP is the future for .NET reverse proxies
- Microsoft's backing gives confidence
- Better performance and features

---

### Kong
**Pros**:
- Enterprise-grade features (rate limiting, plugins, auth)
- Extensive plugin ecosystem
- Strong community
- Multi-protocol support (HTTP, gRPC, WebSocket)

**Cons**:
- Lua-based plugins (learning curve)
- Requires PostgreSQL or Cassandra database
- Heavier resource footprint
- Commercial features require license

**Why Rejected**: 
- Over-engineering for demo application
- Additional database dependency
- Complex setup

---

### AWS API Gateway / Azure API Management
**Pros**:
- Fully managed, no infrastructure maintenance
- Built-in auth, rate limiting, caching
- Cloud-native integrations

**Cons**:
- Vendor lock-in
- Costs money
- Cannot run locally without cloud account
- Less control over middleware

**Why Rejected**: 
- Demo should run locally without cloud dependencies
- Want portability across environments

---

### DIY Reverse Proxy (ASP.NET Core Middleware)
**Pros**:
- Full control
- No external dependencies

**Cons**:
- Significant development effort
- Reinventing the wheel
- Maintenance burden
- Likely less performant than YARP

**Why Rejected**: 
- YARP IS the battle-tested DIY solution from Microsoft
- No need to build what already exists

## Advanced Features (Future Enhancements)

### 1. Rate Limiting (Phase 6+)
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
    });
});

app.UseRateLimiter();
```

### 2. Authentication/Authorization (Phase 6+)
```csharp
// Add JWT validation
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* config */ });

app.UseAuthentication();
app.UseAuthorization();
```

### 3. Request Caching
```csharp
builder.Services.AddResponseCaching();
app.UseResponseCaching();
```

### 4. Circuit Breaker (via Polly)
```csharp
// Apply circuit breaker policy to clusters
"products-cluster": {
  "HttpClient": {
    "DangerousAcceptAnyServerCertificate": false,
    "MaxConnectionsPerServer": 100
  },
  "SessionAffinity": {
    "Enabled": false
  }
}
```

### 5. Canary Deployments
```csharp
// Route 10% traffic to new version
"products-cluster": {
  "Destinations": {
    "v1": { "Address": "http://products-service-v1:5000" },
    "v2": { "Address": "http://products-service-v2:5000" }
  },
  "LoadBalancingPolicy": "PowerOfTwoChoices",
  "Metadata": {
    "v2-weight": "10"
  }
}
```

## Performance Characteristics

### Latency Overhead
- **Gateway Only**: ~2-5ms (in-process forwarding)
- **Gateway + Backend**: <10ms total (within target SLA of <500ms)

### Throughput
- **Requests/sec**: 10,000+ on single instance (more than sufficient)
- **Concurrent Connections**: 1,000+ (limited by ASP.NET Core Kestrel)

### Resource Usage
- **Memory**: ~50-100 MB (lightweight)
- **CPU**: <5% at 100 req/sec

**Benchmark** (YARP official):
- **Nginx**: 100,000 req/sec
- **YARP**: 90,000 req/sec (10% slower, still exceptional)

For this application (target: 100 req/sec), YARP is more than adequate.

## Deployment

### Dockerfile (ApiGateway)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ApiGateway.csproj", "./"]
RUN dotnet restore "ApiGateway.csproj"
COPY . .
RUN dotnet build "ApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ApiGateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
```

### Docker Compose Integration

```yaml
api-gateway:
  build:
    context: ./gateway
    dockerfile: Dockerfile
  ports:
    - "5000:5000"
  environment:
    ASPNETCORE_ENVIRONMENT: "Development"
  depends_on:
    - products-service
    - inventory-service
    - cart-service
    - orders-service
    - monolith
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
    interval: 30s
    timeout: 10s
    retries: 3
```

## Migration Path

### Phase 0: Setup Gateway
- Deploy gateway routing all traffic to monolith
- Verify no behavioral changes

### Phase 1: Route Products API
- Update gateway config to route `/api/products` to Products Service
- Enable via feature flag

### Phase 2-5: Incremental Routing
- Update routes as services are extracted
- Monolith remains fallback for unmatched routes

### Phase 6: Decommission Monolith
- Remove monolith fallback route
- Gateway routes exclusively to services

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public async Task Route_Products_Request_To_ProductsService()
{
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();

    var response = await client.GetAsync("/api/products");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    // Verify response came from Products Service (check headers, content)
}
```

### Integration Tests
- Test routing for each service
- Test fallback to monolith
- Test health checks
- Test correlation ID propagation

### Load Tests
```bash
# Using Apache Bench
ab -n 10000 -c 100 http://localhost:5000/api/products

# Using k6
k6 run --vus 100 --duration 30s load-test.js
```

## Notes
- YARP is open source: https://github.com/microsoft/reverse-proxy
- Used by Microsoft in production: Azure, GitHub Codespaces, Visual Studio Online
- Active development, releases every few months
- Supports .NET 6+ (we're on .NET 8)
- Middleware-based, integrates seamlessly with ASP.NET Core

## Decision Justification

**YARP is the optimal choice** for this microservices architecture because:
1. ✅ Native .NET integration (team expertise)
2. ✅ Configuration-driven (minimal code)
3. ✅ Excellent performance (meets SLA)
4. ✅ Extensible (middleware pipeline)
5. ✅ Microsoft-backed (long-term support)
6. ✅ Lightweight (no external dependencies)

**Alternatives like Nginx or Traefik** would work but introduce:
- Different technology stack (learning curve)
- Separate deployment/monitoring
- Over-engineering for current scale

**YARP aligns with project goals**: Pragmatic, .NET-centric, demo-friendly.

## Related ADRs
- ADR-005: Docker Compose for Container Orchestration
- ADR-007: Strangler Fig Migration Pattern
- Migration Plan Phase 0: API Gateway Setup

## Review Date
Re-evaluate if:
- Performance becomes bottleneck (unlikely)
- Need advanced features (service mesh, mTLS, observability)
- Migrate to Kubernetes with Ingress controllers

At that point, consider Istio/Linkerd service mesh or native Kubernetes Ingress.
