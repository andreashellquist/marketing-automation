# Marketing Automation Platform

AI-native marketing automation: email, SMS and push campaigns, real-time
segmentation, and a durable journey engine — built as an API-first .NET 9
modular monolith with a React frontend.

## Status

Specification phase. See [docs/SPECIFICATION.md](docs/SPECIFICATION.md) for the
full architecture and build order.

## Stack

- **Backend**: ASP.NET Core (.NET 9), modular monolith, CQRS (MediatR), EF Core
- **Database**: PostgreSQL (+ Redis, RabbitMQ via MassTransit)
- **Frontend**: React + TypeScript + Vite (backend-agnostic, REST only)
- **AI**: Claude API — natural-language segment builder, journey copilot,
  content assistant, insights

## Local development (planned)

```bash
docker compose up -d   # PostgreSQL, Redis, RabbitMQ, Seq, Mailpit
dotnet run --project src/MarketingAutomation.Api
npm run dev --prefix src/web
```
