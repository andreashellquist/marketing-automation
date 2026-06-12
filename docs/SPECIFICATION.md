# Marketing Automation Platform — Specification v2

Professional-grade marketing automation competing with Braze / Klaviyo / Customer.io,
with ease-of-use and AI functionality as first-class differentiators.

---

## 1. Product pillars

1. **Reliability first** — never double-send, never send to opted-out contacts, never lose an event.
2. **AI-native** — AI assists in every workflow (copy, segments, journeys, send-time), not bolted on.
3. **Easy to use** — natural-language interfaces wherever a power-user UI would otherwise be required.
4. **API-first** — every UI capability exists as a public, versioned REST API with webhooks.

---

## 2. Architecture overview

**Modular monolith** (single deployable, strict module boundaries) — not microservices.
Split out modules only when scale demands it. Modules:

| Module | Responsibility |
|---|---|
| `Contacts` | CDP: profiles, identity resolution, consent, suppression |
| `Events` | High-throughput event ingestion, event store |
| `Segments` | Segment definitions (AST), evaluation, real-time membership |
| `Campaigns` | One-shot sends, A/B testing, scheduling |
| `Journeys` | Stateful multi-step automation engine |
| `Messaging` | Channel-agnostic send pipeline, providers, delivery tracking |
| `Templates` | Email/SMS/push templates, brand kit, versioning |
| `Analytics` | Reporting, attribution, aggregates |
| `Ai` | AI services consumed by all other modules |
| `Platform` | Tenancy, auth, API keys, webhooks, audit log |

### Tech stack

| Layer | Choice | Rationale |
|---|---|---|
| Backend | ASP.NET Core (.NET 9), modular monolith | — |
| Frontend | React + TypeScript + Vite | Backend-independent, talks REST only |
| Primary DB | **PostgreSQL** + EF Core | JSONB for segment ASTs & event payloads; first-class EF support; no licensing |
| Analytics events | PostgreSQL partitioned tables → ClickHouse when volume demands | Don't add ClickHouse on day one |
| Queue | **MassTransit** abstraction; RabbitMQ locally, Azure Service Bus in prod if desired | No vendor lock-in |
| Cache | Redis | Segment membership counts, rate limits, dedup keys |
| Scheduling | Hangfire (recurring + delayed jobs) | Journey *wakeups*, not journey state |
| IDs | **`Guid.CreateVersion7()`** (.NET 9 native) | Sortable, index-friendly, no ULID dependency |
| Templating | Liquid via **Fluid** | Industry standard (Shopify/Klaviyo-compatible merge syntax) |
| Email | SendGrid behind `IEmailProvider` | Provider-agnostic; add SES as second provider early to prove the abstraction |
| SMS | Twilio + 46elks behind `ISmsProvider` | 46elks for Nordic pricing |
| Push | FCM (HTTP v1) + APNs behind `IPushProvider` | — |
| AI | **Claude API** (`claude-fable-5` for generation, `claude-haiku-4-5` for classification/cheap tasks) | — |
| Auth | ASP.NET Core Identity + OpenIddict (OAuth2/OIDC, PKCE for SPA) | — |
| Observability | OpenTelemetry → Seq locally, Application Insights/Grafana in prod | — |

### Solution structure

```
src/
  MarketingAutomation.Api/             # Composition root, minimal APIs per module
  MarketingAutomation.Modules.Contacts/
  MarketingAutomation.Modules.Events/
  MarketingAutomation.Modules.Segments/
  MarketingAutomation.Modules.Campaigns/
  MarketingAutomation.Modules.Journeys/
  MarketingAutomation.Modules.Messaging/
  MarketingAutomation.Modules.Templates/
  MarketingAutomation.Modules.Analytics/
  MarketingAutomation.Modules.Ai/
  MarketingAutomation.Modules.Platform/
  MarketingAutomation.SharedKernel/    # Base entities, Result types, domain events
  web/                                 # React app (separate from .NET projects)
tests/
  <Module>.Tests/                      # Unit + integration per module (Testcontainers)
  MarketingAutomation.ArchitectureTests/  # Enforce module boundaries (NetArchTest)
```

Each module: `Domain` / `Application` (CQRS, MediatR) / `Infrastructure` / `Endpoints` folders
internally. Modules communicate only via published contracts (MediatR notifications /
integration events on the bus) — enforced by architecture tests.

---

## 3. Reliability — the non-negotiables

These are what separate professional from amateur in this category:

### 3.1 Transactional outbox
Every state change + outgoing message is written in the same DB transaction to an
`outbox` table; a relay publishes to the bus. No message is ever published without
its state change, and vice versa.

### 3.2 Idempotency everywhere
- Send pipeline: dedup key `(campaignId|journeyStepId, contactId)` — a message to a
  contact for a given step is sent **at most once**, enforced by a unique constraint,
  not by Redis alone.
- Public API: `Idempotency-Key` header support on all POSTs.
- Webhook ingestion (DLRs, bounces): provider event IDs deduped.

### 3.3 Suppression is checked at send time, not enqueue time
Opt-outs between scheduling and delivery must be honored. The last step before any
provider call re-checks: consent, suppression list, quiet hours, frequency cap.
This check lives in one place (`Messaging` pipeline) for all channels.

### 3.4 Frequency capping & messaging policy
Per-tenant configurable: max N marketing messages per contact per day/week, across
channels, with channel priorities. Transactional messages exempt.

### 3.5 Kill switch
Per-tenant and global "pause all sending" flag checked in the send pipeline.
When a campaign is sending to 500k contacts and something is wrong, stopping it
must take effect in seconds.

---

## 4. Module designs

### 4.1 Contacts (CDP)

- **Profile**: standard fields + JSONB custom attributes (tenant-defined schema).
- **Identity resolution**: contacts can have multiple identifiers (email, phone,
  deviceId, externalId, anonymousId). Anonymous web events merge into known profiles
  on identify. Merge strategy: deterministic (shared identifier) only; probabilistic
  merging is out of scope.
- **Consent ledger** (append-only): per channel × purpose. Each entry: timestamp,
  source, IP, exact consent text shown, proof. Current consent state is a projection.
- **Suppression lists**: global + per-tenant, synced across channels. Hard bounces,
  spam complaints, STOP replies, manual additions.
- **GDPR**: erasure (anonymize, keep aggregates), export, retention policies per
  data category. Erasure cascades through event store.

### 4.2 Events

- `POST /api/v1/events` (single + batch), API-key auth, designed for high throughput:
  validate → write to ingest queue → ack 202. Consumers project into the event store
  and fan out to segment evaluation and journey triggers.
- Event schema: `name`, `contactIdentifier`, `timestamp`, `properties` (JSONB),
  `messageId` (client-generated, deduped).
- Standard events emitted by the platform itself: `email.delivered`, `email.opened`,
  `email.clicked`, `sms.delivered`, `push.opened`, etc. — usable in segments and
  journey triggers like any custom event.

### 4.3 Segments

- Definition stored as a **JSON AST**: conditions on attributes, events
  (count/recency/properties), segment membership, consent state — combined with
  AND/OR/NOT, arbitrarily nested.
- **Compiled to SQL** for full evaluation; **incrementally maintained** membership
  table updated by event consumers for real-time enter/exit (which trigger journeys).
- Segment preview: live count + sample contacts while building.
- **AI: natural-language segment builder** — "customers who bought twice in the last
  90 days but haven't opened an email in 30" → AST, shown in the visual builder for
  confirmation. This is the flagship ease-of-use feature; the visual builder edits
  the same AST so users can refine AI output.
- RFM scores and engagement scores computed nightly as derived attributes.

### 4.4 Campaigns

API contract as drafted previously (v1 endpoints, status state machine, content
sub-resource, A/B testing, stats) with these amendments:

- A/B testing generalized to **n variants** with optional **AI-generated variants**
  ("generate 3 subject line alternatives") and auto-winner selection with statistical
  significance (sequential testing, not fixed-horizon).
- **Send-time optimization** (AI): per-contact optimal send hour learned from
  engagement history; campaign option "send at each contact's best time within a
  24h window".
- Recipient snapshot at send start (auditable: exactly who was targeted and why).

### 4.5 Journeys — the core engine

A journey is a versioned graph: **triggers** (event, segment enter/exit, schedule,
API call) → **nodes** (send message, wait duration, wait-until event with timeout,
if/else on attributes/events, random split, A/B split, webhook call, update profile,
exit).

**Execution model — durable state machine, not workflow-engine magic:**

- `journey_runs` table: one row per (contact, journey version) with `currentNodeId`,
  `state`, `wakeUpAt`. Advancing a run = transaction: evaluate node, perform action
  via outbox, move pointer.
- Waits are **persisted wake-ups** (Hangfire delayed jobs / scheduled poller), not
  in-memory timers. A server restart loses nothing.
- Event-waits: event consumers match incoming events against waiting runs (indexed
  by `(journeyId, nodeId, contactId)`).
- **Versioning**: editing a live journey creates a new version; in-flight runs finish
  on their version (configurable: migrate or drain).
- Per-journey settings: re-entry policy (never / after exit / after N days),
  concurrency limit, goal event (for conversion reporting + early exit).
- **AI: journey copilot** — describe the flow in natural language → draft journey
  graph in the visual editor. AI review mode: "this wait of 5 minutes after signup
  may race with your welcome email" style lint warnings.

**Build the engine custom.** Evaluated alternatives: Elsa Workflows (heavy,
generic, hard to shape into marketing semantics), Temporal (operational burden,
.NET SDK fine but overkill). The domain is narrow enough that a custom table-driven
state machine is simpler, debuggable, and fully ours.

### 4.6 Messaging (send pipeline)

Single channel-agnostic pipeline; channels are providers behind interfaces.

```
(campaign batcher | journey node) → outbox → queue → send worker:
  1. load contact + render template (Liquid, fallback values mandatory)
  2. policy gate: consent, suppression, quiet hours, frequency cap, kill switch
  3. idempotency check (unique constraint)
  4. provider send (per-provider rate limiting, circuit breaker, retry with backoff)
  5. record message row + emit platform event
provider webhooks (DLR/bounce/open/click) → dedupe → update message status → emit events
```

- Link tracking: redirect service with signed short URLs, per-message click attribution.
- Open tracking pixel (with the caveat in analytics that Apple MPP inflates opens —
  surface click-based engagement as primary metric).
- Per-tenant sending domains: guided SPF/DKIM/DMARC setup with DNS verification UI.
- IP/domain warmup schedules for new tenants (ramped daily volume caps).
- Seed-list / inbox-placement testing hook (integration point, fas 2).

### 4.7 Templates & Email designer

- Unlayer embedded in React for drag & drop; output stored as both design JSON and
  compiled HTML. Server-side re-render with Liquid for personalization.
- **Brand kit** per tenant: logo, colors, fonts, footer/legal blocks auto-injected.
- Template versioning; shared reusable blocks (header/footer) referenced, not copied.
- Plaintext alternative auto-generated, editable.
- Pre-send checks: broken links, missing merge-tag fallbacks, image weight,
  spam-trigger heuristics, missing unsubscribe link (hard block).
- **AI: content assistant** — generate/rewrite copy in brand voice (tenant-configurable
  tone profile), translate, shorten for SMS, suggest preview text, alt-text for images.

### 4.8 Analytics

- Message-level facts + daily aggregates (materialized). Campaign and journey
  reporting from aggregates, drill-down from facts.
- Revenue attribution: last-touch within configurable window, fed by `order.completed`
  events.
- Journey analytics: funnel per node (entered / completed / converted / dropped),
  goal conversion rate per variant.
- Deliverability dashboard: bounce/complaint trends per sending domain.
- **AI: insights digest** — weekly natural-language summary per tenant: anomalies,
  best/worst performers, suggested actions.

### 4.9 AI module (cross-cutting)

- Thin internal service wrapping the Claude API: prompt templates, tenant context
  injection (brand voice, product catalog summary), token budgets per tenant,
  response caching, full audit log of AI generations.
- Model routing: `claude-fable-5` for generation/copilot, `claude-haiku-4-5` for
  classification (e.g. sentiment on inbound SMS replies, spam-risk scoring).
- Every AI output is a **draft requiring human confirmation** before it affects
  sending — no AI-autonomous sends in v1.
- Predictive (fas 2, classical ML not LLM): churn risk, LTV, send-time optimization
  model (start with simple per-contact engagement-hour histogram — works surprisingly
  well — before any ML).

### 4.10 Platform

- **Multi-tenancy from day one**: `TenantId` on every table, EF Core global query
  filters, tenant resolution middleware. Retrofitting tenancy is a rewrite.
- API keys (scoped), OAuth2 for the SPA, role-based access (Admin / Editor / Viewer).
- **Outbound webhooks**: tenant-subscribable events with HMAC signatures and retries.
- Audit log: who changed what, especially around consent and sending.
- Rate limiting on public API per key.

---

## 5. Compliance

- Double opt-in flows built-in per channel.
- One-click unsubscribe (RFC 8058 `List-Unsubscribe-Post`) — required by
  Gmail/Yahoo for bulk senders.
- Preference center page (hosted, brandable): per-channel and per-topic opt-outs.
- Quiet hours per tenant with per-contact timezone resolution (default 21:00–08:00).
- Data residency: design for EU-only deployment first.

---

## 6. Frontend (React)

| Need | Library |
|---|---|
| Routing | React Router v7 |
| Server state | TanStack Query |
| Forms | React Hook Form + Zod |
| UI | shadcn/ui + Tailwind |
| Tables | TanStack Table |
| Email designer | Unlayer React |
| **Journey/segment canvas** | **React Flow** |
| Charts | Recharts |
| API client | Generated from OpenAPI (orval or openapi-ts) — no hand-written fetch code |

UX principles: every complex builder (segments, journeys, emails) has an
"describe it in words" AI entry point that produces a draft in the visual editor.

---

## 7. Local development

Docker Compose: PostgreSQL, Redis, RabbitMQ, Seq, Mailpit (catches all outgoing
email locally — never send real mail from dev). `dotnet run` + `npm run dev`,
seeded demo tenant with sample contacts/events.

---

## 8. Build order

1. **Foundation**: solution structure, tenancy, auth, outbox, MassTransit, Docker
   Compose, CI (GitHub Actions: build, test, arch-tests).
2. **Contacts + Events**: profiles, consent ledger, suppression, event ingestion.
3. **Messaging pipeline**: email via SendGrid + Mailpit locally, policy gate,
   idempotent sends, DLR webhooks, link/open tracking.
4. **Campaigns**: full API contract, scheduling, batched sending, stats.
5. **Segments**: AST + SQL compilation + incremental membership + AI NL-builder.
6. **Journeys**: engine + React Flow editor + versioning.
7. **Templates + email designer** integration, brand kit, AI content assistant.
8. **SMS + Push** channels (pipeline already channel-agnostic).
9. **Analytics** dashboards + AI insights digest.
10. Fas 2: WhatsApp, in-app, predictive ML, inbox-placement testing, ClickHouse.

Each phase ships behind the same public API and is demoable end-to-end.
