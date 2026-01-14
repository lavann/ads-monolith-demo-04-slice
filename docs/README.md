# Documentation Overview

This directory contains comprehensive documentation for the Retail Monolith application.

## Documents

### [High-Level Design (HLD.md)](./HLD.md)
System architecture and design overview:
- System architecture and technology stack
- Domain boundaries (Products, Cart, Orders, Checkout)
- Data stores and database schema
- External dependencies
- Runtime environment and deployment

### [Low-Level Design (LLD.md)](./LLD.md)
Detailed technical implementation:
- Class-by-class breakdown of all modules
- Request flow diagrams
- Database access patterns
- **10 identified coupling hotspots and technical issues**
- Dependency graphs

### [Runbook (Runbook.md)](./Runbook.md)
Operational guide for developers:
- Prerequisites and setup instructions
- Build, run, and deployment commands
- Configuration management
- Troubleshooting common issues
- **11 documented known issues and tech debt items**
- Database queries and maintenance

### [Architecture Decision Records (ADR/)](./ADR/)
Historical context for key design decisions:

- **[ADR-001](./ADR/001-sql-server-localdb.md)**: SQL Server with LocalDB for Development
- **[ADR-002](./ADR/002-service-layer-pattern.md)**: Service Layer Pattern with Dependency Injection
- **[ADR-003](./ADR/003-mock-payment-gateway.md)**: Mock Payment Gateway for Development
- **[ADR-004](./ADR/004-auto-migrations-on-startup.md)**: Automatic EF Core Migrations on Startup

Each ADR documents the context, decision, consequences (positive and negative), and alternatives considered.

## Reading Guide

**For New Developers**:
1. Start with [Runbook.md](./Runbook.md) to get the application running
2. Read [HLD.md](./HLD.md) to understand the system architecture
3. Review [LLD.md](./LLD.md) when diving into specific code areas

**For Architects/Tech Leads**:
1. Review [HLD.md](./HLD.md) for system overview
2. Read all [ADRs](./ADR/) to understand design rationale
3. Study "Coupling Hotspots" section in [LLD.md](./LLD.md) for refactoring priorities

**For Operations/DevOps**:
1. Focus on [Runbook.md](./Runbook.md) for deployment and troubleshooting
2. Review database operations and configuration sections
3. Check "Known Issues" for operational concerns

## Documentation Principles

All documentation in this directory reflects the **current state** of the codebase as-is:
- ✅ Accurate representation of existing implementation
- ✅ Identifies actual technical debt and issues
- ✅ No speculative or future designs
- ✅ Based on direct code inspection

## Maintenance

This documentation should be updated whenever:
- Architecture changes significantly
- New modules or services are added
- Design decisions are made (add new ADRs)
- Known issues are resolved (update Runbook)
- Major refactoring occurs (update LLD)

---

**Last Updated**: 2026-01-14
**Application Version**: Initial Documentation Pass
