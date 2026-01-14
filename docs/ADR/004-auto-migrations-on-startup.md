# ADR-004: Automatic EF Core Migrations on Startup

## Status
Accepted (with concerns)

## Context
Entity Framework Core uses migrations to evolve database schema. The application must ensure the database exists and is up-to-date before handling requests. Common approaches include:
- Manual migration execution (`dotnet ef database update`)
- CI/CD pipeline database deployment step
- Automatic migration on application startup
- Database initialization scripts (SQL)

For a demo/development application, we need a frictionless experience for developers.

## Decision
**Automatically apply EF Core migrations on application startup**, along with automatic data seeding if the database is empty.

**Implementation** (`Program.cs`):
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();       // Apply pending migrations
    await AppDbContext.SeedAsync(db);       // Seed sample data
}
```

**Execution Timing**: After service registration, before middleware pipeline starts handling requests

**Migration Source**: Migrations in `/Migrations` folder, generated via `dotnet ef migrations add`

## Consequences

### Positive
- **Zero-friction development**: Developers can clone and run immediately
- **Consistent state**: Database always matches code
- **No manual steps**: No need to run CLI commands
- **Container-friendly**: Works well in Docker where database may not exist yet
- **Demo-ready**: Sample data auto-populated for demonstration

### Negative
- **Startup delay**: Application blocks until migrations complete
  - Can be significant for large migrations
  - Affects availability during deployments
- **Production risk**: 
  - Schema changes applied without human verification
  - No rollback strategy if migration fails mid-execution
  - Can cause downtime in high-availability scenarios
- **Concurrency issues**: Multiple instances starting simultaneously may conflict
- **Permission requirements**: Application service account needs DDL permissions (CREATE, ALTER, DROP)
- **Audit concerns**: No approval workflow or change tracking
- **Testing complexity**: Hard to test migration failures

### Failure Scenarios

#### Migration Fails
```csharp
await db.Database.MigrateAsync();  // Throws exception
```
**Result**: Application fails to start, returns 500 errors

**Recovery**: Fix migration or revert code, restart application

#### Multiple Instances
If 3 instances start simultaneously:
- All attempt to apply migrations
- EF Core has built-in locking (`__EFMigrationsHistory` table)
- One succeeds, others wait or fail gracefully
- **Risk**: Startup time triples due to blocking

#### Permission Denied
If application runs with read-only database credentials:
- Migration fails with permission error
- Application cannot start
- **Solution**: Use privileged credentials or migrate out-of-band

## Alternatives Considered

#### Manual Migration in Development
```bash
dotnet ef database update
```
- **Pros**: Explicit control, no surprises
- **Cons**: Developers must remember to run, easy to forget
- **Why rejected**: Friction for demo purposes

#### Migration in CI/CD Pipeline
```yaml
- step: Database Migration
  script: dotnet ef database update
```
- **Pros**: Controlled deployment, audit trail, rollback support
- **Cons**: Requires deployment infrastructure
- **Why rejected**: Appropriate for production, overkill for demo

#### Separate Migration Container (Kubernetes Init Container)
```yaml
initContainers:
  - name: migrate
    image: app-migrator
    command: ["dotnet", "ef", "database", "update"]
```
- **Pros**: Decouples migration from application lifecycle
- **Cons**: Complex orchestration
- **Why rejected**: Not applicable to demo setup

#### Database Initializer Scripts
Pure SQL scripts run by DBA or deployment tool
- **Pros**: Database-agnostic, version controlled
- **Cons**: Bypasses EF Core, manual maintenance
- **Why rejected**: Loses EF Core model-first benefits

#### Conditional Migration Based on Environment
```csharp
if (app.Environment.IsDevelopment())
{
    await db.Database.MigrateAsync();
}
```
- **Pros**: Limits risk to development
- **Cons**: Production deployments require different process
- **Why considered**: Good compromise

## Production Recommendations

### DO NOT use auto-migration in production

Instead:
1. **Manual migration**: DBA reviews and executes during maintenance window
2. **Blue-green deployment**: Migrate on blue environment, test, then switch traffic
3. **Backward-compatible migrations**: Support old and new schema simultaneously
4. **Database CI/CD**: Dedicated pipeline for schema changes

### Safer Alternative for Production
```csharp
var canMigrate = builder.Configuration.GetValue<bool>("Database:AutoMigrate", false);

if (canMigrate && app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await AppDbContext.SeedAsync(db);
}
else
{
    // Verify database is ready
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var canConnect = await db.Database.CanConnectAsync();
    if (!canConnect)
        throw new InvalidOperationException("Database not available. Run migrations separately.");
}
```

### Health Check Enhancement
Add database migration health check:
```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>()
    .AddCheck("migrations", () =>
    {
        var pending = db.Database.GetPendingMigrations();
        return pending.Any() 
            ? HealthCheckResult.Unhealthy("Pending migrations") 
            : HealthCheckResult.Healthy();
    });
```

## Related Concerns

### Seeding Strategy
Current seeding logic:
```csharp
if (!await db.Products.AnyAsync())
{
    // Add 50 products with inventory
}
```

**Issues**:
- Runs on every startup (though checks first)
- Demo data mixed with production data approach
- No separation of required vs. sample data

**Recommendation**: Separate seed methods
```csharp
await AppDbContext.SeedReferenceDataAsync(db);  // Always run (e.g., categories)
if (app.Environment.IsDevelopment())
    await AppDbContext.SeedSampleDataAsync(db);  // Only in dev
```

### Migration Rollback
EF Core doesn't support automatic rollback. Options:
1. `dotnet ef database update <PreviousMigration>` - Manually revert
2. Database backup/restore
3. Write down migrations manually

**Current state**: No rollback capability

## Notes
- Auto-migration is appropriate for:
  - Development environments
  - Personal projects
  - Demos and prototypes
  - Containerized dev databases
- Auto-migration is **NOT** appropriate for:
  - Production databases
  - Shared QA/staging environments
  - High-availability systems
  - Regulated industries with change control

## Decision Justification
For this **demo application**, auto-migration is acceptable because:
- Simplifies onboarding
- Reduces setup documentation
- Reflects common development practices
- Code clearly indicates this is a "hack convenience" (per comment in code)

The trade-off is documented here so future maintainers understand the risk when moving toward production.
