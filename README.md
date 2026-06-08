# BizMetrics

A multi-tenant **analytics SaaS** for small businesses. Each organization uploads its
data and gets dashboards, reports and automated insights — with subscription billing,
team roles and a full audit trail.

This repository is built as a portfolio piece demonstrating the things that separate a
production SaaS from a CRUD app: **tenant isolation, RBAC, Stripe billing, and background
data processing.**

> **Status:** Phase 0 complete — solution skeleton, authentication (JWT + refresh),
> the multitenancy foundation (EF Core global query filter), Docker Compose, and CI.
> See the [roadmap](#roadmap) below.

## Stack

| Layer    | Tech                                              |
| -------- | ------------------------------------------------- |
| Frontend | React 19 + TypeScript + Vite + React Router       |
| Backend  | ASP.NET Core 10 Web API                           |
| Data     | PostgreSQL 16 + EF Core 9                          |
| Auth     | JWT access tokens + rotating refresh tokens, BCrypt |
| Infra    | Docker Compose, GitHub Actions CI                 |

## Architecture

```
┌────────────┐      JWT (org_id claim)      ┌──────────────────────┐
│  React SPA │ ───────────────────────────▶ │  ASP.NET Core API     │
│  (Vite)    │ ◀─────────────────────────── │                      │
└────────────┘        JSON / REST           │  TenantMiddleware     │
                                            │     │ sets ITenantContext│
                                            │     ▼                  │
                                            │  AppDbContext          │
                                            │   global query filter  │
                                            │   (OrganizationId)     │
                                            └──────────┬────────────┘
                                                       │
                                                 ┌─────▼─────┐
                                                 │ PostgreSQL │
                                                 └───────────┘
```

### Multitenancy model

Single shared database, shared schema. Every tenant-scoped entity implements
`ITenantEntity` (carries `OrganizationId`), and `AppDbContext` applies an EF Core
**global query filter** keyed on the current `ITenantContext`. The result: a query that
forgets to filter by organization simply returns nothing — cross-tenant reads are
impossible to write by accident. This is covered by
[`TenantIsolationTests`](tests/BizMetrics.Tests/TenantIsolationTests.cs).

The current tenant comes from the authenticated user's `org_id` JWT claim, resolved per
request by [`TenantMiddleware`](src/BizMetrics.Api/Auth/TenantMiddleware.cs).

## Project layout

```
src/
  BizMetrics.Domain/          Entities (User, Organization, Membership, Plan, Dataset…)
  BizMetrics.Infrastructure/  EF Core DbContext, migrations, tenancy
  BizMetrics.Api/             Controllers, auth, middleware, composition root
tests/
  BizMetrics.Tests/           xUnit tests (tenant isolation, tokens)
frontend/                     React + TS SPA
```

## Running locally

### Option A — Docker Compose (everything)

```bash
docker compose up --build
```

- API → http://localhost:8080 (Swagger at `/swagger`)
- Web → http://localhost:5173
- Postgres → localhost:5432

The API applies migrations and seeds billing plans automatically on startup.

### Option B — run pieces by hand

```bash
# 1. Postgres
docker compose up -d db

# 2. API
dotnet run --project src/BizMetrics.Api      # http://localhost:8080

# 3. Frontend
cd frontend && npm install && npm run dev     # http://localhost:5173
```

## Tests

```bash
dotnet test
```

## API

| Method | Route                                      | Min role | Purpose                                 |
| ------ | ------------------------------------------ | -------- | --------------------------------------- |
| POST   | `/api/auth/register`                       | —        | Create user + organization, start trial |
| POST   | `/api/auth/login`                          | —        | Authenticate, return token pair         |
| POST   | `/api/auth/refresh`                        | —        | Rotate refresh token, issue new access  |
| GET    | `/api/orgs/current`                        | Member   | Current organization details            |
| PATCH  | `/api/orgs/current`                        | Admin    | Rename the organization                 |
| GET    | `/api/orgs/current/members`                | Member   | List members                            |
| PATCH  | `/api/orgs/current/members/{userId}/role`  | Admin    | Change a member's role (rule-checked)   |
| DELETE | `/api/orgs/current/members/{userId}`       | Admin    | Remove a member (rule-checked)          |
| GET    | `/api/orgs/mine`                           | (any)    | Orgs the user belongs to (switcher)     |
| POST   | `/api/orgs/{orgId}/switch`                 | (any)    | Re-issue a token scoped to another org  |
| POST   | `/api/orgs/current/invitations`            | Admin    | Invite an email to join (queues email)  |
| GET    | `/api/orgs/current/invitations`            | Admin    | List pending invitations                |
| DELETE | `/api/orgs/current/invitations/{id}`       | Admin    | Revoke a pending invitation             |
| GET    | `/api/invitations/{token}`                 | (public) | Preview an invitation (accept page)     |
| POST   | `/api/invitations/accept`                  | (any)    | Accept an invitation, join the org      |
| GET    | `/api/datasets`                            | (any)    | List the current org's datasets         |
| POST   | `/api/datasets`                            | (any)    | Create a dataset (placeholder)          |
| GET    | `/health`                                  | —        | Liveness probe                          |

### RBAC

Roles are `Owner > Admin > Member > Viewer`. Endpoints are guarded by policy-based
authorization ([`MinimumRoleRequirement`](src/BizMetrics.Api/Auth/MinimumRoleRequirement.cs)),
and member administration enforces business rules via pure, unit-tested logic
([`RoleManagementRules`](src/BizMetrics.Api/Authorization/RoleManagementRules.cs)) — e.g.
the last Owner can't be demoted/removed, only an Owner can grant ownership, and no one can
change their own role.

## Roadmap

- [x] **Phase 0** — skeleton, auth (JWT + refresh), tenancy foundation, Docker, CI
- [x] **Phase 1** — org management, RBAC via authorization policies, org switcher, team UI
- [x] **Phase 2** — team invitations by email (async queue), accept flow, role management
- [ ] **Phase 3** — CSV upload + background processing, object storage
- [ ] **Phase 4** — analytics query engine, dashboards, charts, auto-insights
- [ ] **Phase 5** — Stripe billing (checkout, portal, webhooks, trials, plan limits)
- [ ] **Phase 6** — audit log, rate limiting, OAuth login, observability
- [ ] **Phase 7** — deploy + live demo + e2e tests

## License

MIT
