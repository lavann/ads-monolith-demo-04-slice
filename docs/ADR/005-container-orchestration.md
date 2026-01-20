# ADR-005: Docker Compose for Container Orchestration (Initial Phase)

## Status
Accepted

## Context
The target microservices architecture requires container orchestration to manage multiple services (Products, Inventory, Cart, Orders) along with their dependencies (databases, API gateway). We need a deployment solution that:

- Supports local development and testing
- Enables easy demonstration of the architecture
- Handles service dependencies and networking
- Provides a path to production-grade orchestration later
- Has minimal learning curve for the team
- Works across development environments

### Orchestration Options Considered

#### 1. Docker Compose
- **Description**: YAML-based multi-container orchestration tool
- **Maturity**: Stable, version 2.x standard
- **Learning Curve**: Low (simple YAML syntax)

#### 2. Kubernetes
- **Description**: Industry-standard container orchestration platform
- **Maturity**: Production-ready, CNCF graduated project
- **Learning Curve**: High (complex concepts: pods, deployments, services, ingress)

#### 3. Docker Swarm
- **Description**: Docker's native clustering solution
- **Maturity**: Maintenance mode, less active development
- **Learning Curve**: Medium

#### 4. AWS ECS / Azure Container Apps / Google Cloud Run
- **Description**: Cloud-managed container services
- **Maturity**: Production-ready
- **Learning Curve**: Medium, requires cloud account

## Decision
Use **Docker Compose** for initial migration phases (Phases 0-5), with an option to migrate to **Kubernetes** for production deployments if needed.

**Rationale**:
1. **Simplicity**: Docker Compose is the simplest orchestration tool, ideal for development and demos
2. **Local Development**: Developers can run entire microservices stack on their machines with `docker-compose up`
3. **Quick Iteration**: Fast feedback loop during service extraction and testing
4. **No Infrastructure**: No cluster setup required (unlike Kubernetes)
5. **Sufficient for Demo**: The application is a demo/proof-of-concept, not a high-scale production system
6. **Migration Path**: Docker Compose definitions can be translated to Kubernetes manifests when needed

## Implementation

### Example docker-compose.yml Structure

```yaml
version: '3.8'

services:
  # Database
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      SA_PASSWORD: "${SQL_SA_PASSWORD}"
      MSSQL_PID: "Developer"
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "${SQL_SA_PASSWORD}" -Q "SELECT 1" || exit 1
      interval: 10s
      timeout: 3s
      retries: 10
      start_period: 10s

  # Products Service (Phase 1)
  products-service:
    build:
      context: ./services/ProductsService
      dockerfile: Dockerfile
    ports:
      - "5001:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=RetailMonolith;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=True"
    depends_on:
      sqlserver:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s

  # Inventory Service (Phase 2)
  inventory-service:
    build:
      context: ./services/InventoryService
      dockerfile: Dockerfile
    ports:
      - "5002:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=RetailMonolith;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=True"
    depends_on:
      sqlserver:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s

  # Cart Service (Phase 3)
  cart-service:
    build:
      context: ./services/CartService
      dockerfile: Dockerfile
    ports:
      - "5003:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=RetailMonolith;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=True"
      ServiceDiscovery__ProductsService: "http://products-service:5000"
    depends_on:
      sqlserver:
        condition: service_healthy
      products-service:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s

  # Orders Service (Phase 5)
  orders-service:
    build:
      context: ./services/OrdersService
      dockerfile: Dockerfile
    ports:
      - "5004:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=RetailMonolith;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=True"
      ServiceDiscovery__CartService: "http://cart-service:5000"
      ServiceDiscovery__InventoryService: "http://inventory-service:5000"
      ServiceDiscovery__ProductsService: "http://products-service:5000"
    depends_on:
      sqlserver:
        condition: service_healthy
      cart-service:
        condition: service_healthy
      inventory-service:
        condition: service_healthy
      products-service:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s

  # API Gateway (Phase 0)
  api-gateway:
    build:
      context: ./gateway
      dockerfile: Dockerfile
    ports:
      - "5000:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      ServiceDiscovery__ProductsService: "http://products-service:5000"
      ServiceDiscovery__InventoryService: "http://inventory-service:5000"
      ServiceDiscovery__CartService: "http://cart-service:5000"
      ServiceDiscovery__OrdersService: "http://orders-service:5000"
      ServiceDiscovery__Monolith: "http://monolith:5000"
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

  # Frontend (Razor Pages UI)
  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    ports:
      - "8080:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      ApiGateway__BaseUrl: "http://api-gateway:5000"
      ServiceDiscovery__ProductsService: "http://products-service:5000"
      ServiceDiscovery__CartService: "http://cart-service:5000"
      ServiceDiscovery__OrdersService: "http://orders-service:5000"
      FeatureFlags__UseProductsService: "true"
      FeatureFlags__UseInventoryService: "false"
      FeatureFlags__UseCartService: "false"
      FeatureFlags__UseCheckoutService: "false"
    depends_on:
      - api-gateway
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  # Monolith (Compatibility Layer)
  monolith:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "5005:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=RetailMonolith;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=True"
    depends_on:
      sqlserver:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s

volumes:
  sqldata:
    driver: local

networks:
  default:
    name: retail-network
```

### Environment Variables (.env file)

```env
SQL_SA_PASSWORD=YourStrong@Passw0rd
ASPNETCORE_ENVIRONMENT=Development
```

### Commands

**Start all services**:
```bash
docker-compose up -d
```

**View logs**:
```bash
docker-compose logs -f products-service
```

**Stop all services**:
```bash
docker-compose down
```

**Rebuild specific service**:
```bash
docker-compose up -d --build products-service
```

**Scale a service** (Phase 5+):
```bash
docker-compose up -d --scale products-service=3
```

## Consequences

### Positive
- **Fast onboarding**: New developers can run entire stack with single command
- **Isolated environment**: Each service runs in its own container with predictable dependencies
- **Service networking**: Automatic DNS resolution between services (e.g., `http://products-service:5000`)
- **Development/production parity**: Containers run the same locally and in deployment
- **Easy debugging**: Logs accessible via `docker-compose logs`
- **Health checks**: Built-in support for readiness/liveness probes
- **Volume persistence**: Database data persists across container restarts
- **Low resource usage**: Lightweight compared to Kubernetes

### Negative
- **Not production-grade**: Docker Compose lacks auto-scaling, self-healing, and high availability
- **Single-host limitation**: All containers run on one machine (no distributed cluster)
- **No load balancing**: Built-in load balancing is basic (round-robin only)
- **Manual scaling**: Requires explicit `--scale` commands
- **Limited monitoring**: No built-in observability beyond logs
- **Secret management**: Secrets in .env files are not secure for production

### Mitigation for Production
When production requirements exceed Docker Compose capabilities:

1. **Translate to Kubernetes**: Use Kompose tool or manually write K8s manifests
   ```bash
   kompose convert -f docker-compose.yml -o k8s/
   ```

2. **Use Managed Services**: Deploy to Azure Container Apps, AWS ECS, or Google Cloud Run
   - Minimal configuration changes
   - Managed infrastructure
   - Auto-scaling built-in

3. **Helm Charts**: Package microservices as Helm charts for Kubernetes deployments

## Alternatives Considered

### Kubernetes (Immediate Adoption)
**Pros**:
- Production-ready from day one
- Auto-scaling, self-healing, rolling updates
- Industry standard for microservices

**Cons**:
- Steep learning curve for team
- Requires Minikube/Kind/Docker Desktop for local development
- Overkill for demo application
- Complex YAML configuration (Deployments, Services, Ingress, ConfigMaps, Secrets)
- Slower iteration during development

**Why Rejected**: Too complex for initial migration phases. Can adopt later if needed.

---

### Docker Swarm
**Pros**:
- Native Docker clustering
- Simpler than Kubernetes
- Built-in load balancing

**Cons**:
- Less active development (maintenance mode)
- Smaller ecosystem compared to Kubernetes
- Fewer features than Kubernetes

**Why Rejected**: If we need clustering, Kubernetes is the better long-term choice.

---

### Cloud-Managed Services (ECS/Container Apps/Cloud Run)
**Pros**:
- Fully managed, no infrastructure maintenance
- Auto-scaling and load balancing
- Integrates with cloud provider ecosystem

**Cons**:
- Requires cloud account and costs
- Vendor lock-in
- Less portable for local development

**Why Rejected**: Demo app should run locally without cloud dependencies. Can deploy to cloud later.

---

### No Orchestration (Manual Docker Commands)
**Pros**:
- Maximum control
- No orchestration layer to learn

**Cons**:
- Tedious to manage multiple services manually
- No automatic networking or dependency management
- Error-prone

**Why Rejected**: Unmanageable for 5+ services.

## Migration Path to Kubernetes (Optional)

If production deployment requires Kubernetes later:

### Step 1: Generate Kubernetes Manifests
```bash
kompose convert -f docker-compose.yml
```

### Step 2: Customize Manifests
- Add resource limits/requests
- Configure Ingress for API Gateway
- Set up ConfigMaps and Secrets
- Define Horizontal Pod Autoscalers

### Step 3: Deploy to Kubernetes
```bash
kubectl apply -f k8s/
```

### Step 4: Verify
```bash
kubectl get pods
kubectl get services
kubectl logs -f deployment/products-service
```

## Notes
- Docker Compose is ideal for **Phases 0-5** of the migration plan
- Transition to Kubernetes is **optional** and depends on production requirements
- For demo purposes, Docker Compose is **sufficient** and recommended
- Health checks ensure services start in correct order (dependencies)
- Container networking eliminates need for IP address management
- Volumes ensure database data persists across restarts

## Decision Justification

**For this demo/modernization project**, Docker Compose is the right choice because:
1. ✅ Team familiarity with Docker
2. ✅ Fast development iteration
3. ✅ Easy to demonstrate to stakeholders
4. ✅ Sufficient for non-production workloads
5. ✅ Can evolve to Kubernetes if needed

**Kubernetes would be premature optimization** given:
- Demo/proof-of-concept nature of the application
- Small team size
- No immediate need for high availability or auto-scaling

## Related ADRs
- ADR-006: API Gateway with YARP
- ADR-007: Strangler Fig Migration Pattern
- Migration Plan Phase 0: Foundation

## Review Date
Re-evaluate if production deployment requires:
- Multi-node clustering
- Auto-scaling based on metrics
- Advanced traffic routing (canary, blue-green)
- Service mesh (Istio, Linkerd)

At that point, consider migrating to Kubernetes.
