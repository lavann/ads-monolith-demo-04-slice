# ADR-001: Use SQL Server with LocalDB for Development

## Status
Accepted

## Context
The Retail Monolith application requires a relational database to store products, inventory, carts, and orders. The development team needs a solution that:
- Works on Windows development machines without additional setup
- Supports Entity Framework Core for ORM
- Provides production-grade capabilities for eventual deployment
- Requires minimal configuration for new developers

## Decision
Use **SQL Server** as the primary database, with **LocalDB** for local development environments.

**Connection String** (Development):
```
Server=(localdb)\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true
```

**Implementation**:
- Entity Framework Core 9.0.9 with SQL Server provider
- `DesignTimeDbContextFactory` for CLI tooling (migrations)
- Auto-migration on application startup via `db.Database.MigrateAsync()`
- Configurable connection string via `appsettings.json` for production

## Consequences

### Positive
- **Zero-config for developers**: LocalDB installs with Visual Studio
- **Feature parity**: LocalDB uses same SQL Server engine as production
- **Familiar tooling**: SQL Server Management Studio, Azure Data Studio
- **Azure readiness**: Easy migration to Azure SQL Database
- **Relational integrity**: Foreign keys, indexes, transactions built-in
- **EF Core integration**: Full support for migrations and LINQ queries

### Negative
- **Windows-only for LocalDB**: Mac/Linux developers need Docker or remote SQL Server
- **Licensing**: SQL Server requires licenses in production (though Azure SQL offers alternatives)
- **Overkill for demo**: A lightweight option like SQLite could suffice for a simple demo
- **Startup dependency**: Application requires database to be available
- **Migration on startup risk**: Auto-migration in production environments is dangerous

### Alternatives Considered

#### SQLite
- **Pros**: Cross-platform, zero-config, file-based
- **Cons**: Limited concurrency, fewer features, different SQL dialect
- **Why rejected**: SQL Server is assumed production target

#### PostgreSQL
- **Pros**: Open-source, cross-platform, feature-rich
- **Cons**: Requires separate installation, different tooling
- **Why rejected**: Team familiarity with SQL Server ecosystem

#### In-memory database
- **Pros**: Fast, no persistence
- **Cons**: Data lost on restart, not suitable for demo app
- **Why rejected**: Need persistent storage

## Notes
- The choice of SQL Server reflects a typical enterprise .NET stack
- LocalDB is a SQL Server feature, not a separate product
- For non-Windows environments, connection string can be overridden to point to Docker container or remote instance
- Future ADRs should address production database configuration and migration strategy
