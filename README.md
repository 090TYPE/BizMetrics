# BizMetrics

A multi-tenant **analytics SaaS** for small businesses. Each organization uploads its
data and gets dashboards, reports and automated insights — with subscription billing,
team roles and a full audit trail.

Built as a portfolio piece demonstrating the things that separate a production SaaS from
a CRUD app: **tenant isolation, RBAC, Stripe billing, background processing, audit log,
rate limiting, Google OAuth, and observability.**

> **Status:** All seven phases complete — register, explore and try the demo.
> Demo credentials (auto-seeded): **demo@bizmetrics.io / demo1234**

## Stack

| Layer      | Tech                                                              |
| ---------- | ----------------------------------------------------------------- |
| Frontend   | React 19 + TypeScript + Vite + React Router                       |
| Backend    | ASP.NET Core 10 Web API (.NET 10)                                 |
| Database   | PostgreSQL 16 + EF Core 9 (JSONB for parsed data rows)           |
| Storage    | MinIO / S3-compatible via AWS SDK v4, presigned download URLs     |
| Auth       | JWT (15-min access) + rotating refresh tokens (14-day), BCrypt   |
| OAuth      | Google Sign-In via ID-token verification (Google.Apis.Auth)      |
| Async      | In-process `Channel<T>` queues + `IHostedService` workers        |
| Billing    | Stripe Checkout + Customer Portal + idempotent webhooks          |
| Audit      | Append-only `AuditEntry` table, 19 action constants, admin API   |
| Rate limit | ASP.NET Core built-in: 10 req/min/IP (auth), 10 req/min/user (upload) |
| Observability | Sentry error tracking + DB & storage health checks            |
| Deploy     | Docker Compose (local) + Fly.io (production)                     |
| Tests      | xUnit unit tests (72), Playwright e2e tests                      |

## Architecture

```
┌───────────────────────┐   JWT (org_id, org_role claims)  ┌──────────────────────────────┐
│   React SPA (Vite)    │ ────────────────────────────────▶│  ASP.NET Core 10 API          │
│   @react-oauth/google │ ◀────────────────────────────────│                              │
└───────────────────────┘          JSON / REST             │  RateLimiter (10/min/IP)      │
                                                           │  TenantMiddleware             │
                                                           │    └─ sets ITenantContext     │
                                                           │  Controllers                  │
                                                           │    ├─ AuthController          │
                                                           │    ├─ DatasetsController      │
                                                           │    ├─ AnalyticsController     │
                                                           │    ├─ BillingController       │
                                                           │    └─ AuditController         │
                                                           │  AuditService (silent log)    │
                                                           │  PlanGuard (402 on limit)     │
                                                           └──────────────┬───────────────┘
                                                                          │
                    ┌─────────────────────────────────────────────────────┼──────────────┐
                    │                                                      │              │
              ┌─────▼──────┐   EF Core global        ┌────────────┐  ┌───▼────┐   ┌────▼────┐
              │ AppDbContext│   query filter          │  MinIO /   │  │ Stripe │   │ Sentry  │
              │  (scoped)  │   (OrganizationId)       │  S3 Object │  │  API   │   │  SDK    │
              └─────┬──────┘                          │  Storage   │  └────────┘   └─────────┘
                    │                                 └────────────┘
              ┌─────▼──────┐   Channel<T> queues
              │ PostgreSQL │◀──────────────────── EmailBackgroundService
              │     16     │                      DatasetProcessingService
              └────────────┘
```

### Tenant isolation

Single shared database, shared schema. Every tenant entity implements `ITenantEntity`
(`OrganizationId` property) and `AppDbContext` applies an EF Core **global query filter**
keyed on the current `ITenantContext`. A query that forgets to filter simply returns nothing
— cross-tenant reads are impossible to write by accident. Covered by
[`TenantIsolationTests`](tests/BizMetrics.Tests/TenantIsolationTests.cs).

The current tenant comes from the authenticated user's `org_id` JWT claim, resolved per
request by [`TenantMiddleware`](src/BizMetrics.Api/Auth/TenantMiddleware.cs).

### RBAC

Roles: `Owner > Admin > Member > Viewer`. Endpoints guarded by policy-based authorization
([`MinimumRoleRequirement`](src/BizMetrics.Api/Auth/MinimumRoleRequirement.cs)).
Role-change and removal business rules enforced by pure, unit-tested logic
([`RoleManagementRules`](src/BizMetrics.Api/Authorization/RoleManagementRules.cs)).

## Project layout

```
src/
  BizMetrics.Domain/          Entities, enums, audit action constants
  BizMetrics.Infrastructure/  EF Core DbContext + migrations, tenancy,
                              audit service, billing service, storage, email
  BizMetrics.Api/             Controllers, auth/authz, health checks,
                              demo seeder, Fly.io deployment
tests/
  BizMetrics.Tests/           72 xUnit tests (tenant isolation, RBAC, tokens,
                              billing, audit log, plan guards)
e2e/                          Playwright end-to-end tests
frontend/                     React + TypeScript SPA
```

## Running locally

### Option A — Docker Compose (one command)

```bash
docker compose up --build
```

| Service   | URL                                    |
| --------- | -------------------------------------- |
| API       | http://localhost:8080 (Swagger at `/swagger`) |
| Frontend  | http://localhost:5173                  |
| PostgreSQL| localhost:5432                         |
| MinIO     | S3 API :9000, console http://localhost:9001 (`minioadmin` / `minioadmin`) |

The API applies migrations, seeds billing plans, and **auto-seeds demo data** on startup.

**Demo login:** `demo@bizmetrics.io` / `demo1234`

### Option B — run pieces individually

```bash
# 1. Postgres + MinIO
docker compose up -d db minio

# 2. API
dotnet run --project src/BizMetrics.Api      # http://localhost:8080

# 3. Frontend
cd frontend && npm install && npm run dev     # http://localhost:5173
```

## Tests

```bash
# Unit tests (72 tests, ~1 s)
dotnet test

# End-to-end tests (requires docker compose up first)
cd e2e && npm install && npm test
```

## Deploying to Fly.io

```bash
# 1. Create apps
fly apps create bizmetrics-api
fly apps create bizmetrics-web

# 2. Attach a Postgres database
fly postgres create --name bizmetrics-db
fly postgres attach bizmetrics-db --app bizmetrics-api

# 3. Provision MinIO or use AWS S3
#    Then set the required secrets:
fly secrets set \
  Jwt__SigningKey="<32+ random chars>" \
  Jwt__Issuer="https://bizmetrics-api.fly.dev" \
  Jwt__Audience="https://bizmetrics-api.fly.dev" \
  Storage__Endpoint="https://your-minio-or-s3" \
  Storage__AccessKey="..." \
  Storage__SecretKey="..." \
  Storage__BucketName="bizmetrics" \
  --app bizmetrics-api

# 4. Deploy
fly deploy                                          # API  (uses fly.toml)
fly deploy --config fly.frontend.toml               # Web  (uses fly.frontend.toml)
```

Optional secrets: `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Google__ClientId`,
`Sentry__Dsn` — leave blank to run in demo mode.

## API reference

| Method | Route                                       | Min role | Purpose                                          |
| ------ | ------------------------------------------- | -------- | ------------------------------------------------ |
| POST   | `/api/auth/register`                        | —        | Create user + org, start 14-day trial            |
| POST   | `/api/auth/login`                           | —        | Authenticate, return JWT pair                    |
| POST   | `/api/auth/google`                          | —        | Google ID-token login (auto-registers on first)  |
| POST   | `/api/auth/refresh`                         | —        | Rotate refresh token, issue new access token     |
| GET    | `/api/orgs/current`                         | Member   | Current org details                              |
| PATCH  | `/api/orgs/current`                         | Admin    | Rename the organization                          |
| GET    | `/api/orgs/current/members`                 | Member   | List members                                     |
| PATCH  | `/api/orgs/current/members/{id}/role`       | Admin    | Change a member's role (rule-checked)            |
| DELETE | `/api/orgs/current/members/{id}`            | Admin    | Remove a member (rule-checked)                   |
| GET    | `/api/orgs/mine`                            | Any      | Orgs the user belongs to (org switcher)          |
| POST   | `/api/orgs/{orgId}/switch`                  | Any      | Re-issue token scoped to another org             |
| POST   | `/api/orgs/current/invitations`             | Admin    | Invite by email (queues email, checks plan limit)|
| GET    | `/api/orgs/current/invitations`             | Admin    | List pending invitations                         |
| DELETE | `/api/orgs/current/invitations/{id}`        | Admin    | Revoke invitation                                |
| GET    | `/api/invitations/{token}`                  | Public   | Preview invitation (accept page)                 |
| POST   | `/api/invitations/accept`                   | Any      | Accept, join org                                 |
| GET    | `/api/datasets`                             | Any      | List datasets                                    |
| GET    | `/api/datasets/{id}`                        | Any      | Dataset status, schema, errors                   |
| POST   | `/api/datasets/upload`                      | Any      | Upload CSV → storage + queue (plan-guarded)      |
| GET    | `/api/datasets/{id}/rows`                   | Any      | Paged row preview                                |
| GET    | `/api/datasets/{id}/download-url`           | Any      | Presigned URL for raw file                       |
| DELETE | `/api/datasets/{id}`                        | Any      | Delete dataset + rows                            |
| POST   | `/api/datasets/{id}/query`                  | Any      | Aggregation query + auto-insights                |
| GET    | `/api/dashboards`                           | Any      | List dashboards                                  |
| POST   | `/api/dashboards`                           | Any      | Create dashboard                                 |
| GET    | `/api/dashboards/{id}/data`                 | Any      | Dashboard with all widgets computed              |
| POST   | `/api/dashboards/{id}/widgets`              | Any      | Save a chart widget                              |
| DELETE | `/api/dashboards/{id}/widgets/{widgetId}`   | Any      | Remove widget                                    |
| DELETE | `/api/dashboards/{id}`                      | Any      | Delete dashboard                                 |
| GET    | `/api/billing`                              | Member   | Plan, subscription status, trial countdown, usage|
| POST   | `/api/billing/checkout`                     | Member   | Stripe Checkout session URL                      |
| POST   | `/api/billing/portal`                       | Member   | Stripe Customer Portal URL                       |
| POST   | `/api/webhooks/stripe`                      | —        | Stripe webhook (sig-verified, idempotent)        |
| GET    | `/api/audit`                                | Admin    | Paginated audit log (filterable by action/type)  |
| POST   | `/api/demo/seed`                            | —        | Seed demo workspace (dev / DEMO_SEED=true only)  |
| GET    | `/health`                                   | —        | Liveness probe                                   |
| GET    | `/health/detail`                            | —        | Readiness probe (DB + storage, JSON)             |

## Roadmap

- [x] **Phase 0** — skeleton, auth (JWT + refresh), tenancy foundation, Docker, CI
- [x] **Phase 1** — org management, RBAC, org switcher, team UI
- [x] **Phase 2** — team invitations by email (async queue), accept flow
- [x] **Phase 3** — CSV upload to object storage + background parsing into JSONB
- [x] **Phase 4** — analytics engine (group-by + time-series), auto-insights, saved dashboards
- [x] **Phase 5** — Stripe billing (checkout, portal, webhooks, trials, plan limits)
- [x] **Phase 6** — audit log, rate limiting, Google OAuth, observability (Sentry + health checks)
- [x] **Phase 7** — demo seed data, production Dockerfile (nginx), Fly.io deployment, Playwright e2e

## License

MIT
