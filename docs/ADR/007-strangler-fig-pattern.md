# ADR-007: Strangler Fig Pattern for Monolith Migration

## Status
Accepted

## Context
We need to migrate from a monolithic architecture to microservices while:
- Maintaining system stability and availability
- Preserving all existing functionality
- Minimizing risk of breaking changes
- Enabling incremental progress (not big-bang rewrite)
- Supporting instant rollback at any stage
- Allowing parallel work on multiple services
- Operating the system during migration

The migration must be safe, gradual, and reversible.

## Decision
Adopt the **Strangler Fig Pattern** (also called "Strangler Application Pattern") for migrating the Retail Monolith to microservices.

**Pattern Overview**:
The strangler fig pattern is named after the strangler fig tree, which grows around an existing tree, eventually replacing it. In software:

1. **New Services Coexist**: Build new microservices alongside the monolith
2. **Gradual Traffic Shift**: Route select traffic to new services via API Gateway
3. **Feature Flags**: Control which requests go to monolith vs. services
4. **Validate in Shadow Mode**: Run services in parallel with monolith to compare outputs
5. **Incremental Cutover**: Gradually increase traffic to services (10% → 50% → 100%)
6. **Decommission Monolith**: Remove monolith components only when fully replaced

**Key Principle**: The monolith and microservices coexist for an extended period. There is never a "big-bang" cutover.

## Why Strangler Fig?

### Problem with Big-Bang Rewrite
A complete rewrite approach would require:
- Stopping all new feature development
- Months/years of parallel development
- High risk of missing requirements
- Difficult to test at scale
- All-or-nothing deployment (no rollback)
- Long period without business value delivery

**Historical Evidence**: Big-bang rewrites often fail or exceed time/budget dramatically.

### Advantages of Strangler Fig
- ✅ **Low Risk**: Each service extraction is small and reversible
- ✅ **Incremental Value**: Each extracted service delivers immediate benefits
- ✅ **Continuous Delivery**: Features can be added to monolith or services during migration
- ✅ **Testable**: Each service validated independently before full cutover
- ✅ **Rollback**: Feature flags enable instant revert to monolith
- ✅ **Parallelizable**: Multiple teams can extract different services simultaneously
- ✅ **Learning**: Early mistakes inform later extractions

## Implementation Strategy

### Phase Structure
Each migration phase follows this template:

1. **Identify Service Boundary**: Choose a domain/module to extract
2. **Build New Service**: Implement microservice with equivalent functionality
3. **Dual-Run Phase**: Operate both monolith and service in parallel
4. **Shadow Mode**: Route traffic to monolith but also call service for validation
5. **Gradual Cutover**: Shift traffic incrementally (10% → 50% → 100%)
6. **Monitor and Validate**: Compare metrics, logs, and business outcomes
7. **Decommission**: Remove monolith code (optional, can keep as backup)

### Routing Mechanism: API Gateway + Feature Flags

**API Gateway (YARP)** routes requests based on configuration:
```json
{
  "ReverseProxy": {
    "Routes": {
      "products-route": {
        "ClusterId": "products-cluster",
        "Match": { "Path": "/api/products/{**catch-all}" }
      },
      "monolith-fallback": {
        "ClusterId": "monolith-cluster",
        "Match": { "Path": "/{**catch-all}" },
        "Order": 100
      }
    }
  }
}
```

**Feature Flags** control logic in application code:
```csharp
if (_config.GetValue<bool>("FeatureFlags:UseProductsService"))
{
    // Call Products Service
    return await _productsClient.GetProductsAsync();
}
else
{
    // Fallback to monolith
    return await _db.Products.Where(p => p.IsActive).ToListAsync();
}
```

**Benefits**:
- **Runtime Toggle**: No code deployment needed to change routing
- **Instant Rollback**: Set flag to `false` and traffic reverts
- **A/B Testing**: Route 50% to service, 50% to monolith
- **Per-User Flags**: Beta test with select users

### Data Strategy: Shared Database Initially

**Phase 1-3**: Services connect to the same database as the monolith
- **Benefit**: No data migration complexity early on
- **Trade-off**: Services are not fully independent (shared schema)

**Phase 4**: Extract data to dedicated databases
- **Benefit**: True service independence
- **Trade-off**: Must handle cross-service data access (APIs, eventual consistency)

**Why Start Shared?**
- Reduces initial risk
- Focuses on service logic, not data migration
- Allows validation against monolith using same data
- Simplifies rollback (no data sync issues)

**Why Separate Later?**
- Enables independent service scaling
- Prevents schema coupling
- Supports different data stores per service (e.g., Redis for Cart)

### Validation Strategy: Shadow Mode

Before fully cutting over to a new service, run it in **shadow mode**:

```csharp
// Execute both monolith and service
var monolithResult = await GetProductsFromMonolith();
var serviceResult = await GetProductsFromService();

// Compare outputs
if (!AreEqual(monolithResult, serviceResult))
{
    _logger.LogWarning("Service output diverges from monolith!");
    // Log discrepancies for investigation
}

// Return monolith result (service is just validating)
return monolithResult;
```

**Benefits**:
- Detects bugs/discrepancies before they affect users
- Builds confidence in service implementation
- Measures performance (service vs. monolith latency)

**When to Use**: For critical services (Checkout, Orders), not necessarily for read-only services

### Gradual Traffic Shift

**Week 1**: 0% (shadow mode only)
**Week 2**: 10% (selected beta users or internal traffic)
**Week 3**: 25%
**Week 4**: 50%
**Week 5**: 75%
**Week 6**: 100%

**Monitoring at Each Step**:
- Error rates (should not increase)
- Latency (should remain within SLA)
- Business metrics (conversion rate, order success)

**Rollback Trigger**: If error rate > 1% or latency > 2x baseline, revert to previous percentage

### Decommissioning Monolith Code (Optional)

Once a service handles 100% traffic for an extended period (e.g., 30 days):
- **Option A**: Remove monolith code for that domain (clean up codebase)
- **Option B**: Keep monolith code as backup (safety net)

**Recommendation**: Keep monolith code for 90 days post-cutover, then archive

## Application to Retail Monolith

### Service Extraction Order (Strangler Fig Sequence)

**Phase 1: Products Catalog Service**
- **Why First?**: Read-heavy, low coupling, minimal transaction coordination
- **Strangler Approach**: 
  - Build Products Service
  - Gateway routes `/api/products` to service
  - Monolith still serves `/Products` Razor Page (calls service or DB based on flag)
  - Gradual rollout over 4-6 weeks
  - Monolith product code remains as fallback

**Phase 2: Inventory Service**
- **Why Second?**: Clear boundary, introduces saga pattern (learning opportunity)
- **Strangler Approach**:
  - Build Inventory Service
  - Checkout Service calls Inventory API (controlled by feature flag)
  - Monolith inventory logic remains as fallback
  - Gradual rollout with heavy testing (concurrency critical)

**Phase 3: Cart Service**
- **Why Third?**: Stateful but isolated, good candidate for future Redis migration
- **Strangler Approach**:
  - Build Cart Service
  - Frontend calls Cart API (controlled by feature flag)
  - Checkout Service calls Cart API
  - Monolith cart logic remains as fallback

**Phase 4: Data Isolation**
- **Why Fourth?**: After service logic validated, extract data
- **Strangler Approach**:
  - Export data to new databases per service
  - Update connection strings
  - No traffic routing changes (services already handling 100%)

**Phase 5: Orders & Checkout Service**
- **Why Last?**: Most complex orchestration (saga across Cart, Inventory, Payment)
- **Strangler Approach**:
  - Build Orders Service with checkout orchestration
  - Gateway routes `/api/checkout` to service
  - Monolith checkout logic remains as ultimate fallback
  - Extended shadow mode (2-3 weeks) due to complexity

**Phase 6: Decommission Monolith**
- **Final Step**: Remove monolith entirely
- **Strangler Complete**: Microservices fully replace monolith

### Timeline
- **Total Duration**: 14-21 weeks (3.5 - 5 months)
- **Per Service**: 2-4 weeks each
- **Overlap**: Phases can partially overlap (e.g., start Cart while Products in rollout)

## Consequences

### Positive
- ✅ **Risk Mitigation**: Each phase is small and reversible
- ✅ **Continuous Operation**: System remains available throughout migration
- ✅ **Incremental Value**: Benefits realized after each phase, not just at the end
- ✅ **Learning Curve**: Early phases inform later ones (patterns, pitfalls)
- ✅ **Business Continuity**: Features can be added to monolith or services during migration
- ✅ **Testing**: Each service tested independently before cutover
- ✅ **Rollback**: Feature flags enable instant revert
- ✅ **Stakeholder Confidence**: Visible progress after each phase

### Negative
- ⚠️ **Longer Timeline**: Takes months instead of weeks (but much safer)
- ⚠️ **Operational Complexity**: Running monolith + services simultaneously
- ⚠️ **Code Duplication**: Some logic exists in both monolith and services temporarily
- ⚠️ **Monitoring Overhead**: Must monitor both monolith and services
- ⚠️ **Testing Overhead**: Integration tests must cover both paths
- ⚠️ **Database Coupling**: Shared database in early phases limits independence

### Mitigations
| Challenge | Mitigation |
|-----------|------------|
| Code duplication | Accept temporary duplication; clean up after cutover |
| Monitoring complexity | Unified logging with correlation IDs |
| Testing overhead | Automated integration tests for both monolith and service paths |
| Database coupling | Phase 4 extracts data; accept coupling early for risk reduction |

## Alternatives Considered

### Big-Bang Rewrite
**Approach**: Build all microservices, test extensively, cutover all at once

**Pros**:
- Clean separation (no hybrid state)
- Faster in theory (no incremental overhead)

**Cons**:
- **High Risk**: All-or-nothing deployment
- **Long Development**: Months without delivering value
- **Hard to Test**: Cannot validate at scale until cutover
- **No Rollback**: If it fails, must roll forward
- **Opportunity Cost**: No new features during rewrite

**Why Rejected**: Too risky. Industry consensus: big-bang rewrites often fail.

---

### Parallel Development (Maintain Both)
**Approach**: Continue developing monolith, build microservices in parallel, cutover when ready

**Pros**:
- Business continues with monolith
- Microservices developed independently

**Cons**:
- **Feature Divergence**: Monolith and services drift apart
- **Double Maintenance**: Every feature must be implemented twice
- **Eventually Must Cutover**: Still faces big-bang cutover risk
- **Resource Drain**: Two codebases to maintain

**Why Rejected**: Unsustainable. Strangler fig allows parallel but with gradual cutover.

---

### Branch by Abstraction
**Approach**: Introduce abstraction layer in monolith, swap implementations behind it

**Pros**:
- Refactoring within monolith
- Gradual replacement

**Cons**:
- **Still Monolithic**: Services not independently deployable
- **Monolith Complexity Increases**: Abstraction layers add indirection
- **Not True Microservices**: Still one deployment unit

**Why Rejected**: Doesn't achieve microservices goal. Better suited for internal refactoring.

---

### Database Replication
**Approach**: Replicate data to service databases from the start

**Pros**:
- Services have independent data immediately

**Cons**:
- **Complexity Early**: Data sync is hard
- **Consistency Challenges**: Eventual consistency from day one
- **Premature Optimization**: Data isolation not needed until services proven

**Why Rejected**: Adds risk early. Strangler fig defers data migration to Phase 4.

## Pattern Variations

### Strangler Fig with Shadow Deployment
Run new service and monolith in parallel, compare outputs:
- **Use Case**: Critical services (Checkout, Orders)
- **Duration**: 1-2 weeks before traffic routing

### Canary Deployment (Subset of Strangler Fig)
Route small percentage of traffic to test service:
- **Use Case**: Initial rollout (10% traffic)
- **Benefit**: Limits blast radius of issues

### Blue-Green Deployment (Alternative Cutover)
Run two environments (blue = monolith, green = services), switch routing:
- **Use Case**: One-time cutover per service
- **Trade-off**: Less gradual than strangler fig

**Our Choice**: Strangler fig with gradual rollout (10% → 50% → 100%) is safer than blue-green's one-time switch.

## Success Criteria

### Per-Phase Criteria
Each service extraction is considered successful when:
- ✅ Service handles 100% traffic for 30 days without rollback
- ✅ Error rate ≤ monolith baseline
- ✅ Latency within SLA (p95 < 500ms)
- ✅ Business metrics unchanged (order success rate, etc.)
- ✅ No customer complaints related to service
- ✅ Team confident in service stability

### Overall Migration Success
The migration is complete when:
- ✅ All services extracted and handling 100% traffic
- ✅ Monolith decommissioned (or kept as archived fallback)
- ✅ Each service independently deployable
- ✅ Database per service (Phase 4 complete)
- ✅ Zero downtime experienced during entire migration
- ✅ Feature parity maintained throughout

## Lessons from Industry

### Case Studies

**1. Shopify**: Migrated from Rails monolith to services over 3+ years
- Used modularization first, then service extraction
- Kept monolith running throughout
- Gradual traffic shifting

**2. Monzo Bank**: Strangled monolith over 2 years
- Feature flags for every service
- Shadow mode for critical financial operations
- Kept monolith as fallback even after cutover

**3. Amazon**: Famously uses strangler fig for all migrations
- "Never big-bang" policy
- Services coexist with monoliths indefinitely
- Some monoliths still running after 10+ years alongside services

### Best Practices Learned
- Start with read-heavy services (low risk)
- Invest in feature flags early (ROI is huge)
- Shadow mode for critical services (worth the overhead)
- Keep monolith longer than you think (safety net)
- Celebrate each service extraction (maintain momentum)

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Service fails in production | High | Medium | Feature flags for instant rollback; keep monolith running |
| Data inconsistency (shared DB) | Medium | Low | Services read same data; transactions still in monolith |
| Performance regression | Medium | Low | Benchmark before/after; A/B test with 10% traffic first |
| Feature flag misconfiguration | High | Low | Default to false (monolith); test in dev/staging first |
| Team fatigue (long migration) | Medium | Medium | Celebrate milestones; deliver value each phase |
| Missed functionality | High | Low | Integration tests; shadow mode catches discrepancies |
| Operational complexity | Medium | High | Invest in monitoring/logging; correlation IDs essential |

## Timeline and Effort

### Per-Service Effort
- **Service Development**: 1-2 weeks
- **Shadow Mode Testing**: 1 week
- **Gradual Rollout**: 2-4 weeks
- **Monitoring/Stabilization**: 1 week
- **Total**: 2-4 weeks per service

### Overall Timeline
- **5 Services**: 14-21 weeks (3.5 - 5 months)
- **Parallelization**: Can reduce to ~4 months if services extracted in parallel

### Resource Requirements
- **2 Engineers**: Can execute migration (with support)
- **Stakeholder Time**: Weekly check-ins, approval gates

## Decision Justification

**Strangler Fig is the optimal migration pattern** because:
1. ✅ **Proven**: Industry standard for monolith-to-microservices migrations
2. ✅ **Low Risk**: Each phase is small, tested, and reversible
3. ✅ **Pragmatic**: Balances speed with safety
4. ✅ **Flexible**: Can pause, reprioritize, or rollback at any phase
5. ✅ **Incremental Value**: Benefits realized continuously, not just at end
6. ✅ **Learnable**: Early phases inform later ones

**Alternatives like big-bang rewrite** are too risky and have high failure rates historically.

**This pattern aligns with project goals**: Safe, incremental, demo-friendly migration.

## Related ADRs
- ADR-005: Docker Compose for Container Orchestration (infrastructure for coexistence)
- ADR-006: YARP for API Gateway (routing mechanism for strangler fig)
- Migration Plan: Step-by-step phases implement strangler fig

## References
- Martin Fowler's "Strangler Fig Application": https://martinfowler.com/bliki/StranglerFigApplication.html
- Sam Newman's "Monolith to Microservices": Chapter on Strangler Pattern
- Shopify, Monzo, Amazon case studies on strangler fig migrations

## Review Date
Pattern applies throughout migration (Phases 0-6). Review after Phase 5 to decide on monolith decommissioning timeline.
