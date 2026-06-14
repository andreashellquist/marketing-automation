# Marketing Automation Platform

AI-native marketing automation: email, SMS and push campaigns, real-time
segmentation, and a durable journey engine — built as an API-first .NET 9
modular monolith with a React frontend.

## Status

**Phase 1 (foundation) — in progress.** Solution scaffolded; tenancy, transactional
outbox, messaging bus, observability and CI are in place. See
[docs/SPECIFICATION.md](docs/SPECIFICATION.md) for the full architecture and build order.

What's built:
- Modular-monolith solution: 10 module projects + shared kernel + API host
- `SharedKernel`: Guid v7 entities, domain/integration event contracts, tenant context
- `Platform` module: multi-tenant `DbContext` (global query filters, soft delete,
  audit fields), **transactional outbox** + relay to the bus, tenant-resolution middleware
- MassTransit/RabbitMQ wiring, Serilog → Seq, health checks, OpenAPI
- Initial EF Core migration (PostgreSQL, `platform` schema)
- Tests: 20 NetArchTest module-boundary tests + 4 tenancy/outbox tests (SQLite in-memory)
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
