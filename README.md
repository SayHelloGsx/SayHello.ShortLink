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
- `modules/SayHello.ShortLink.Subscription`: optional integration module. Its
  `Domain.Shared` project owns the subscription definition and its `Application` project
  adapts the Subscription public application contract to ShortLink's domain capability port.
  Neither business module references the bridge or the other module.
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
the host composes the modules and supplies ABP's standard external user lookup provider.
Optional product integrations supply their own product entitlement definitions.

- A plan belongs to one product. Products can have multiple plan tiers.
- A bundle is a catalog combination of plans for different products, not a user
  subscription. Assigning it creates a separate subscription for each product.
- Each user has at most one effective subscription per product and tenant.
  Reassigning one product replaces only that product's subscription, even when the
  previous subscription originated from a bundle.
- Each assignment stores its own entitlement snapshot. Later catalog edits and
  withdrawal do not alter existing grants. Reassignment captures the latest values.
- Administrators can select, replace, or explicitly clear one published default
  Free plan for each published product. Configuration is tenant-specific, including
  the host (no tenant); there is no cross-tenant or host-to-tenant fallback.
- Entitlement queries use an effective explicit subscription first, otherwise the
  product's current default. Expired, revoked, or not-yet-effective assignments do
  not suppress the default. An unconfigured product still returns `NoSubscription`.
  Defaults never fill missing features in an explicit subscription or override its
  false, zero, or unlimited values.
- Default rights use the plan's live values: edits and replacement affect fallback
  users on their next query, while actual subscriptions keep their snapshots.
  Resolution reports the source and plan identity; a default has no subscription
  ID and does not count as an effective subscription. It creates no subscription,
  assignment history, or background work.
- Replace or clear a default before withdrawing, archiving, or deleting its plan
  or product. Stale configuration changes are rejected. Catalog writes are
  serialized per tenant until transaction completion; default configuration remains
  per product, and assignment locking remains separate.
- Entitlements are registered in code and configured on plans by administrators.
  Values are Boolean switches or non-negative integer limits with an explicit
  unlimited state. An absent entitlement is not unlimited access.
- Assignments take effect immediately. Each product can have a different expiration,
  or remain valid indefinitely. Expiration is evaluated at query time, without a
  background worker. Revocation and expiration changes apply per product.
- Bundle assignment and all affected replacements are transactional. Stale
  administrative changes are rejected, and database uniqueness protects current
  product assignments, including users without a tenant.
- Subscription stores only the external user ID and does not replicate Identity users.
  Admin search queries the configured ABP external user provider. Preview and assignment
  require a currently existing, active user in the current tenant; inactive users remain
  visible in search but cannot receive a new assignment.

Public catalog pages are available at `/subscriptions/plans` and
`/subscriptions/bundles`, with default plans marked in the plan catalog.
`/subscriptions/mine` requires login and shows the current user's applicable default
Free rights separately from actual subscriptions, with independent paging.
Subscription history remains actual assignments only. Administrative pages are under
`/admin/subscriptions`; product administration shows the selected default or an
explicit unconfigured state.
The API surfaces use `/api/subscription/public/*` and `/api/subscription/admin/*`
with separate HTTP client registrations. Catalog publication does not expose user
records or assignment history to anonymous visitors.

The host adds the module model to its existing database and migrations; a standalone
consumer can use the module's own DbContext and connection-string configuration.
Subscription management permissions are separate from product entitlements.

For another host, compose the appropriate Subscription modules at each layer and provide
ABP's `IExternalUserLookupServiceProvider`: load `AbpIdentityDomainModule` for a local
Identity repository, or `AbpIdentityHttpApiClientModule` and configure the Identity remote
service for distributed deployment. No Subscription-specific Host adapter or local user table
is required. Register a `SubscriptionDefinitionProvider` through
`SubscriptionDefinitionOptions.DefinitionProviders`.
The standalone connection-string name is `Subscription` (falling back to `Default`);
table prefix and schema are configurable through `SubscriptionDbProperties`.
Code inside the Subscription domain can inject `ISubscriptionEntitlementChecker`.
Cross-module integrations should instead depend on a Subscription Application Contracts
package, allowing the composing Host to supply either a local application service or an
HTTP client proxy. Numeric checks do not reserve or consume quota.

The ShortLink Subscription shared integration package registers product `short-link`,
Boolean feature `statistics`, and numeric feature `max-links` (including unlimited
values). This host's database seeding creates only missing draft product metadata and
preserves administrative edits. Publish the product and configure/publish its plans in
administration before selecting a default or assigning subscriptions. Free
`max-links = 20` and Pro `max-links = 100` are administrator-configured examples, not
hard-coded values; seeding does not publish sample plans, select a default, or assign
users.
Additional products and their feature definitions must be registered by the consuming
host or product integration; they are not hard-coded into the Subscription module.

### Optional ShortLink integration

The integration is split by responsibility:

- `SayHello.ShortLink.Subscription.Domain.Shared` owns and registers the stable product and
  feature definitions. A Host that owns the Subscription catalog loads this module.
- `SayHello.ShortLink.Subscription.Application` replaces ShortLink's
  `IShortLinkCapabilityProvider` and consumes only
  `ICurrentUserEntitlementAppService` from
  `SayHello.Subscription.Public.Application.Contracts`.

The bridge does not choose how that application contract is implemented. A monolithic Host,
including this one, loads `SubscriptionApplicationModule` (which includes the local public
application implementation). A distributed ShortLink Host instead loads
`SubscriptionPublicHttpApiClientModule` and configures the `SubscriptionPublic` remote
service endpoint. Do not add either implementation module to the bridge itself.

The entitlement contract is intentionally current-user-only. Before calling it, the adapter
requires the tenant and user passed by ShortLink's domain port to match ABP's ambient
authenticated context. Anonymous or mismatched-subject calls fail authorization; they never
fall back to settings or query another user. A future background or administrative workflow
for another subject requires a separate, explicitly authorized integration contract.

- With the bridge, `max-links` entirely replaces `MaxLinksPerUser`. Missing rights
  deny creation; zero permits no links; unlimited is an explicit grant, not a null
  or missing-value fallback. Per-hour creation rate limits, email confirmation,
  permissions, target validation, and domain blocking still apply.
- Usage is the current tenant/user's **non-deleted** links, including disabled and
  expired links. Committed deletion by the user or an administrator releases capacity;
  failed operations and rolled-back deletions do not. There is no consumption ledger,
  monthly reset, or cross-module create/delete event accounting.
- Free 20 and Pro 100 therefore reject the 21st and 101st owned links respectively.
  A downgrade below current usage blocks further creation, not editing, deletion,
  QR codes, or existing redirects.
- Creation is serialized per tenant/owner through ABP distributed locking and a
  transactional UOW. `ShortLinkManager.CreateAndSaveAsync` checks and inserts under
  that lock; an ambient transaction retains the lock until commit or rollback/disposal.
  The older `CreateAsync` only constructs an entity and is not an atomic persistence API.
  Nontransactional, read-uncommitted, repeatable-read, and snapshot UOWs are rejected
  by the atomic entry point; use read-committed or serializable transactions.
  A lock acquisition failure never falls back to an unlocked write.
- `statistics` gates ordinary-user statistics, including total visits in list/get
  and mutation responses and ordering by visit count. `ShortLinkDto.TotalVisitCount`
  is now nullable: `null` means not disclosed, not zero visits. Update typed clients
  accordingly. Administrative responses still return actual counts under their
  existing permissions.
- Redirects continue collecting visits without consulting subscriptions. Restored
  statistics access includes history that remains within the original retention policy.
- `GET /api/short-link/public/links/capabilities` returns the authenticated user's
  current usage, quota state, finite limit/remaining capacity or unlimited flag, and
  statistics availability. The ShortLink page displays these values; the Subscription
  UI stays generic. A displayed capability is not a reservation or a substitute for
  server-side authorization and quota checks.
- Each server-side entitlement check uses then-effective rights. Later checks see
  expired/revoked assignments or changed defaults; already-checked in-flight operations
  may finish. Subscription administration is not globally locked against link creation.

Before enabling the bridge, publish the product and configure an appropriate default
plan or assign explicit subscriptions. Seeding does **not** grant rights automatically.
Without a usable grant, creation and statistics are denied; existing links remain.
Removing `ShortLinkSubscriptionApplicationModule` from a Host restores ShortLink's
setting-backed quota and normal statistics behavior without a database migration. Both
modules remain usable on their own. Multi-instance ShortLink deployments must configure
a shared ABP lock provider; this host uses Redis. Process-local locks cannot enforce a
cross-instance quota.

No payments, checkout, automatic renewal, or subscription purchase/upgrade flow is
implemented by this integration. No tables or usage backfill are required.

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
Coverage includes fresh schemas, upgrading existing `AddSubscriptions` data with
nullable defaults, restrictive same-product references, and catalog lock leases.

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
