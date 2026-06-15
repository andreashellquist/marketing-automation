# Marketing Automation Platform

AI-native marketing automation: email, SMS and push campaigns, real-time
segmentation, and a durable journey engine — built as an API-first .NET 9
modular monolith with a React frontend.

## Status

**Phases 1–2 in progress.** Foundation, the Contacts CDP, and Event ingestion are
built and tested. See [docs/SPECIFICATION.md](docs/SPECIFICATION.md) for the full
architecture and build order.

What's built:
- Modular-monolith solution: 10 module projects + shared kernel + API host
- `SharedKernel`: Guid v7 entities, domain/integration event contracts, tenant context,
  reusable `ModuleDbContext` base (tenant filter, soft delete, audit, outbox flush),
  MediatR validation behavior, `Channel`/`IdentifierType` primitives
- `Platform`: multi-tenant `DbContext`, **transactional outbox** + background relay
  draining every module's outbox, tenant-resolution middleware
- `Contacts` (CDP): unified profiles with JSONB custom attributes, **deterministic
  identity resolution** with anonymous→known merge, append-only **consent ledger**,
  cross-channel **suppression** (revoking marketing consent suppresses automatically)
- `Events`: idempotent **event ingestion** (single + batch) deduped by message id,
  publishing `EventIngested` for downstream fan-out — with no dependency on Contacts
- MassTransit/RabbitMQ, Serilog → Seq, health checks, OpenAPI, ProblemDetails
- EF Core migrations (PostgreSQL) for `platform`, `contacts`, `events` schemas
- **36 tests**: 20 arch-boundary + 5 platform + 6 contacts + 5 events (SQLite in-memory)
- GitHub Actions CI (build + test on every push/PR)

## Stack

- **Backend**: ASP.NET Core (.NET 9), modular monolith, CQRS (MediatR), EF Core
- **Database**: PostgreSQL (+ Redis, RabbitMQ via MassTransit)
- **Frontend**: React + TypeScript + Vite (backend-agnostic, REST only)
- **AI**: Claude API — natural-language segment builder, journey copilot,
  content assistant, insights

## Local development

```bash
docker compose up -d        # PostgreSQL, Redis, RabbitMQ, Seq, Mailpit
dotnet run --project src/MarketingAutomation.Api
```

The API applies EF migrations automatically in Development. Supporting UIs:
RabbitMQ `:15672`, Seq `:5341`, Mailpit `:8025`.

```bash
dotnet test                 # run all tests
dotnet ef migrations add <Name> \
  --project src/MarketingAutomation.Modules.Platform \
  --startup-project src/MarketingAutomation.Api \
  --output-dir Infrastructure/Migrations
```

## Solution layout

```
src/
  MarketingAutomation.Api/             # Composition root, minimal APIs
  MarketingAutomation.Modules.*/       # One project per bounded module
  MarketingAutomation.SharedKernel/    # Base entities, event contracts, tenant context
tests/
  MarketingAutomation.ArchitectureTests/   # Enforces module isolation
  MarketingAutomation.Platform.Tests/      # Tenancy + outbox behaviour
```
