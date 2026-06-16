# Marketing Automation Platform

AI-native marketing automation: email, SMS and push campaigns, real-time
segmentation, and a durable journey engine — built as an API-first .NET 9
modular monolith with a React frontend.

## Status

**Phases 1–6 in progress.** Foundation, the Contacts CDP, Event ingestion, the
Messaging pipeline, Campaigns, Segments (with the AI natural-language builder), and the
durable Journey engine are built and tested. See
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
- `Segments`: a JSON-AST audience builder (standard fields, custom attributes, behavioral
  events, AND/OR/NOT nesting), CRUD + live preview, and an evaluator that finally makes
  `IAudienceResolver` resolve a real `segmentId`. The AST and evaluation cross module
  boundaries via SharedKernel contracts: Contacts implements `ISegmentEvaluator`, Events
  implements `IEventAudienceQuery` (e.g. "bought twice in 90 days"), Segments composes them.
- `Ai`: **natural-language → segment** via the Claude API (official `Anthropic` .NET SDK,
  `claude-opus-4-8`) behind the `ISegmentAiBuilder` contract — `POST /segments/from-text`
  returns a draft AST for confirmation in the visual builder. AI output is always a draft.
- `Journeys`: a **durable journey engine** — a versioned graph (send / wait / wait-for-event
  / A-B split / exit) executed as a table-driven state machine. Runs are persisted state with
  **wake-ups on disk** (not in-memory timers), so a restart loses nothing: a background
  scheduler re-discovers due time-waits and incoming events resume parked runs (or take the
  timeout branch). At-most-once sends per (run, node); re-entry policy; version pinned per run.
  Sends through the Messaging pipeline via `IMessageSender` — references no other module.
- MassTransit/RabbitMQ, Serilog → Seq, health checks, OpenAPI, ProblemDetails
- EF Core migrations (PostgreSQL) for `platform`, `contacts`, `events`, `messaging`,
  `campaigns`, `segments`, `journeys` schemas
- **71 tests**: 20 arch-boundary + 5 platform + 6 contacts + 5 events + 12 messaging
  + 7 campaigns + 10 segments + 6 journeys
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
