# ShortLink

ShortLink is a modular URL shortener built with .NET 10 and ABP Framework 10.6.
The reusable module follows the same upper-layer composition pattern as ABP CMS Kit:
shared Common services, separate Public and Admin modules, and unsuffixed aggregate
modules for consumers. `SayHello.ShortLink.WebHost` combines them with ABP Account,
Identity, permissions, settings, OpenIddict, PostgreSQL, and Redis.

## Features

- Authenticated link creation with mandatory email confirmation.
- Seven-character cryptographically random Base62 codes or custom aliases.
- Link editing, activation, expiration, soft deletion, and a 180-day code cooldown.
- Root-path redirects with 302, 404, and 410 behavior.
- Privacy-friendly visit analytics without persisted raw IP addresses or user agents.
- QR codes, per-user quotas, distributed creation rate limits, and domain blocking.
- Redirect-time blocked-domain enforcement with cached parent-domain matching and HTTP 451 pages.
- Admin CSV import for up to 10,000 blocked domains per 1 MB file.
- English and Simplified Chinese module UI.
- Docker Compose deployment with Caddy HTTPS, PostgreSQL, Redis, and a one-shot migrator.

## Repository layout

- `modules/SayHello.ShortLink`: reusable ABP application module.
  - `Domain.Shared`, `Domain`, and `EntityFrameworkCore` contain the single shared model.
  - `Common.Application.Contracts` and `Common.Application` contain reusable upper-layer
    DTOs and infrastructure used by both application surfaces.
  - `Public.*` contains ordinary-user link management, analytics, QR codes, and anonymous
    short-code redirects.
  - `Admin.*` contains cross-user administration, blocked domains, and global settings.
  - Unsuffixed `Application*`, `HttpApi*`, and `Web` projects are composition-only modules.
- `modules/SayHello.Subscription`: independent product subscription module, using the
  same Common/Public/Admin and composition-only upper layers.
- `host`: layered MVC host and unified database migrations.
- `deploy`: production Compose, Caddy, certificate, backup, restore, and update assets.
- `.github/workflows`: CI and container publishing.

The single host exposes:

- Public UI: `/short-links`
- Admin UI: `/admin/short-links`
- Public API: `/api/short-link/public/*`
- Admin API: `/api/short-link/admin/*`
- Anonymous redirect: `/{code}`

## Application query boundary

Common, Public, and Admin Application projects do not consume `IQueryable` or call
`GetQueryableAsync`. Application services pass explicit filter, sort, paging, tenant, and
owner parameters to Domain repository interfaces. EF Core repositories own all database
filtering, ordering, paging, grouping, and aggregation, and return Domain entities or
Domain read models. Architecture tests enforce this boundary.

## Product subscriptions

The Subscription module provides an administrator-managed subscription catalog and
user entitlements. It does not depend on ShortLink or Identity implementation types;
the host composes the modules and supplies its Identity user-directory adapter and
product entitlement definitions.

- A plan belongs to one product. Products can have multiple plan tiers.
- A bundle is a catalog combination of plans for different products, not a user
  subscription. Assigning it creates a separate subscription for each product.
- Each user has at most one effective subscription per product and tenant.
  Reassigning one product replaces only that product's subscription, even when the
  previous subscription originated from a bundle.
- Each assignment stores its own entitlement snapshot. Later catalog edits and
  withdrawal do not alter existing grants. Reassignment captures the latest values.
- Entitlements are registered in code and configured on plans by administrators.
  Values are Boolean switches or non-negative integer limits with an explicit
  unlimited state. An absent entitlement is not unlimited access.
- Assignments take effect immediately. Each product can have a different expiration,
  or remain valid indefinitely. Expiration is evaluated at query time, without a
  background worker. Revocation and expiration changes apply per product.
- Bundle assignment and all affected replacements are transactional. Stale
  administrative changes are rejected, and database uniqueness protects current
  product assignments, including users without a tenant.

Public catalog pages are available at `/subscriptions/plans` and
`/subscriptions/bundles`; `/subscriptions/mine` requires login and shows only the
current user's subscriptions. Administrative pages are under `/admin/subscriptions`.
The API surfaces use `/api/subscription/public/*` and `/api/subscription/admin/*`
with separate HTTP client registrations. Catalog publication does not expose user
records or assignment history to anonymous visitors.

The host adds the module model to its existing database and migrations; a standalone
consumer can use the module's own DbContext and connection-string configuration.
Subscription management permissions are separate from product entitlements.

For another host, compose the appropriate Subscription modules at each layer and
implement `ISubscriptionUserDirectory` using that host's user system. Register a
`SubscriptionDefinitionProvider` through `SubscriptionDefinitionOptions.DefinitionProviders`.
The standalone connection-string name is `Subscription` (falling back to `Default`);
table prefix and schema are configurable through `SubscriptionDbProperties`.
Business integrations can inject `ISubscriptionEntitlementChecker` from Domain and
use its Boolean or numeric query/require methods without referencing Public, Admin,
HTTP, or EntityFrameworkCore. Numeric checks do not reserve or consume quota.

This host registers product `short-link`, Boolean feature `statistics`, and numeric
feature `max-links` (including unlimited values). Database seeding creates only
missing draft product metadata and preserves administrative edits. Publish the
product and configure/publish its plans in administration before assigning them.
Additional products and their feature definitions must be registered by the consuming
host or product integration; they are not hard-coded into the Subscription module.

This version deliberately does **not** change ShortLink's existing feature access or
quota settings. It provides entitlement queries for future business integration, but
does not implement payments, checkout, automatic renewal, usage metering, or quota
deduction. No user receives a subscription automatically.

## Development prerequisites

- .NET SDK 10.0.400 or a compatible patch selected by `global.json`.
- ABP CLI 10.6.x.
- Node.js LTS and Yarn 1.x for MVC client libraries.
- Docker Desktop or Docker Engine.

## Build and test

The checked-in NuGet configuration uses Microsoft's public package proxy because the current
development network cannot complete a TLS handshake with NuGet.org directly.

```powershell
dotnet restore .\SayHello.ShortLink.slnx
abp install-libs --working-directory .\host\src\SayHello.ShortLink.WebHost.Web
dotnet build .\SayHello.ShortLink.slnx --configuration Release --no-restore
dotnet test .\SayHello.ShortLink.slnx --configuration Release --no-build
```

### PostgreSQL subscription integration tests

The normal test run uses SQLite. PostgreSQL-specific subscription tests are opt-in:
set `SUBSCRIPTION_TEST_POSTGRES_CONNECTION_STRING` to an **isolated PostgreSQL 17
test instance**, using an account allowed to create databases. Never use a production
connection string. The tests create uniquely named databases, apply the real host
migrations, and delete only the databases they created.

```powershell
$env:SUBSCRIPTION_TEST_POSTGRES_CONNECTION_STRING = '<isolated PostgreSQL test connection string>'
try {
    dotnet test .\host\test\SayHello.ShortLink.WebHost.EntityFrameworkCore.Tests\SayHello.ShortLink.WebHost.EntityFrameworkCore.Tests.csproj --filter FullyQualifiedName~SubscriptionPostgreSql
}
finally {
    Remove-Item Env:\SUBSCRIPTION_TEST_POSTGRES_CONNECTION_STRING
}
```

Without the environment variable these tests are explicitly skipped. SQLite tests
and model inspection alone do not validate PostgreSQL migration or constraint behavior.

## Local container run

Copy `deploy/env.example` to `deploy/.env`, replace every placeholder, generate the
OpenIddict certificate as described in `deploy/README.md`, then:

```powershell
docker compose -f .\deploy\compose.yaml up -d postgres redis
docker compose -f .\deploy\compose.yaml --profile migration run --rm dbmigrator
docker compose -f .\deploy\compose.yaml up -d web caddy
```

For public deployment, point `DOMAIN` at the VPS before starting Caddy. Never commit `.env`,
SMTP credentials, database passwords, Redis passwords, visitor-hash keys, or certificates.

## Important configuration

- `ConnectionStrings__Default`: PostgreSQL connection string.
- `Redis__Configuration`: StackExchange.Redis configuration.
- `ShortLink__Urls__BaseUrl`: public URL used to generate short links and QR codes.
- `ShortLink__Security__OwnHosts__0`: public short-link host, blocked as a recursive target.
- `ShortLink__Privacy__VisitorHashKey`: at least 32 random UTF-8 bytes.
- `Settings__Abp.Mailing.*`: SMTP and sender settings.
- `OpenIddict__ServerCertificate__*`: production signing/encryption certificate.

See `deploy/README.md` for the complete VPS procedure.
