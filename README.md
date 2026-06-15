# Marketing Automation Platform

AI-native marketing automation: email, SMS and push campaigns, real-time
segmentation, and a durable journey engine — built as an API-first .NET 9
modular monolith with a React frontend.

## Status

**Phases 1–4 in progress.** Foundation, the Contacts CDP, Event ingestion, the
Messaging pipeline, and Campaigns are built and tested. See
[docs/SPECIFICATION.md](docs/SPECIFICATION.md) for the full architecture and build order.

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
- `Messaging`: channel-agnostic **send pipeline** with the **policy gate** every message
  passes through at send time — kill switch, suppression, quiet hours (in recipient
  timezone), frequency cap — with transactional traffic exempt; **idempotent sends**
  enforced by a unique dedup-key index; provider abstraction (`IChannelSender`) with a
  dev logging sender; **DLR/bounce webhook** handling that's idempotent and event-emitting.
  Cross-module data (consent/suppression, kill switch) flows through **SharedKernel
  contracts** the owning modules implement — so Messaging references no other module.
- `Campaigns`: full lifecycle — create/update, content sub-resource, **status state
  machine** (Draft→Scheduled→Running→Completed, with pause/cancel/archive), scheduling,
  **batched send** through the Messaging pipeline (one idempotent message per audience
  member), test sends (transactional, QA-only), and **delivery stats**. Drives sends and
  audience via SharedKernel contracts (`IAudienceResolver`, `IMessageSender`,
  `IMessageStatsProvider`) — references no other module.
- MassTransit/RabbitMQ, Serilog → Seq, health checks, OpenAPI, ProblemDetails
- EF Core migrations (PostgreSQL) for `platform`, `contacts`, `events`, `messaging`,
  `campaigns` schemas
- **55 tests**: 20 arch-boundary + 5 platform + 6 contacts + 5 events + 12 messaging
  + 7 campaigns
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
