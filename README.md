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
- Namespace redirects at `/{namespace}/{code}`, with backward-compatible root paths.
- User-owned namespaces and independent subscription quotas for namespaces and root links.
- Privacy-friendly visit analytics without persisted raw IP addresses or user agents.
- QR codes, per-user quotas, distributed creation rate limits, configurable short-link
  Origins, and target-domain blocking.
- Redirect-time blocked-domain enforcement with cached parent-domain matching and HTTP 451 pages.
- Admin CSV import for up to 10,000 blocked domains per 1 MB file.
- English and Simplified Chinese module UI.
- Reusable Razor Pages liquid-glass theme with light/dark/system modes and accessible CSS fallbacks.
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
- `modules/SayHello.LiquidGlassTheme`: independent ABP Razor Pages theme; the host's
  default presentation layer, with no business-module or commercial-theme dependency.
- `host`: layered MVC host and unified database migrations.
- `deploy`: production Compose, Caddy, certificate, backup, restore, and update assets.
- `.github/workflows`: CI and container publishing.

The single host exposes:

- Public UI: `/short-links`
- Namespace management: `/short-links/namespaces`
- Admin UI: `/admin/short-links`
- Public API: `/api/short-link/public/*`
- Admin API: `/api/short-link/admin/*`
- Anonymous redirects: `/{namespace}/{code}` and legacy/explicit root `/{code}`

Every API `DELETE` of a link (`public/links/{id}`, `admin/links/{id}`), an Origin
(`admin/domains/{id}`), a blocked domain (`admin/blocked-domains/{id}`), or a namespace
requires a JSON body containing the read `concurrencyStamp`. A request without a body
returns HTTP 415, a missing stamp returns HTTP 400, and a stale stamp returns HTTP 409.

A `concurrencyStamp` changes only when that resource itself changes. Creating,
changing, or deleting a resource that references another one by ID (a link and its
Origin or namespace, a subscription plan and its product, a product and its default
plan) leaves the referenced resource's stamp unchanged.

## Short-link namespaces

New links use the nonempty global setting `ShortLink.DefaultNamespace`, initially
`s`: omitting namespace selection creates `https://example.com/s/{code}`, not a
root-path link. Administrators change this value through the existing short-link
settings page/API. The setting accepts only default/global providers, not tenant,
user, or configuration overrides. Blank, invalid, protected, or user-occupied
values are rejected. Current and historical defaults remain globally reserved;
changing the setting never changes an existing link or its QR code.

Users manage their own namespaces at `/short-links/namespaces` or
`/api/short-link/public/namespaces`:

- `GET` lists owned namespaces, optionally filtered by `origin`.
- `POST` accepts `origin` and `name`.
- `DELETE /{id}` requires a JSON body containing the read `concurrencyStamp`.
  Stale deletion fails as a concurrency conflict.

Creating a link at `/api/short-link/public/links` still requires `origin`.
Set `namespaceId` to an owned namespace on that Origin, or set `useRootPath: true`
to request `/{code}`. These selections are mutually exclusive. Omitting both uses
the current default; update inputs cannot change a link's path identity.

Namespace names contain 1-64 ASCII letters, digits, underscores, or hyphens.
Names and codes are case-sensitive (`Foo` and `foo` differ), while system-prefix
protection is case-insensitive. Namespace uniqueness spans all users and tenants
on the normalized Origin; different Origins may reuse a name. Code uniqueness
is `(Origin, namespace, code)`, so root paths and different namespaces may reuse a
code. Namespaces cannot be renamed or moved. Any link reference, including a
soft-deleted link, prevents namespace deletion; even an empty namespace prevents
its domain from being deleted.

Protected first segments include API, administration, subscription, account,
authentication, health, and static-resource routes. Hosts with additional routes
must extend `ShortLinkNamespaceRouteOptions.ReservedPrefixes`. Protection does not
apply to the second segment: `/s/api` is a valid short-link path.

Deploy the new schema and namespace-aware application together. The migration
backfills existing links with an empty namespace, preserving every old `/{code}`
address; it does not prepend `s`. Do not run an older namespace-unaware build once
namespaced data exists. Downgrade is explicitly blocked when it would lose link
identity, user namespaces, or historical default reservations. Origin-null legacy
fallback is root-only on the configured legacy Origin; namespaced requests never
fall back to another namespace or to root links.

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
  Values can be Boolean switches, non-negative integer limits with an explicit
  unlimited state, one value from a business-provided Enum, or a normalized
  `Set<string>`. Business integrations can provide Enum/Set choices asynchronously.
  A Set with no choices uses repeatable free-text inputs. An absent entitlement is
  not unlimited access.
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
Business integrations that own Enum or String-Set features register an
`ISubscriptionEntitlementOptionProvider`. `CanProvide` identifies the exact product and
feature owned by that provider; `GetOptionsAsync` returns its current values. Providers
compose across integrations, while registering more than one provider for the same feature
is rejected explicitly. Returning an empty list for a String-Set feature enables free-text
values; Enum features must return at least one value.
The standalone connection-string name is `Subscription` (falling back to `Default`);
table prefix and schema are configurable through `SubscriptionDbProperties`.
Code inside the Subscription domain can inject `ISubscriptionEntitlementChecker`.
Cross-module integrations should instead depend on a Subscription Application Contracts
package, allowing the composing Host to supply either a local application service or an
HTTP client proxy. Numeric checks do not reserve or consume quota.

The ShortLink Subscription shared integration package registers product `short-link`,
Enum feature `statistics`, String-Set feature `domains`, and numeric features
`max-links`, `max-custom-codes`, `max-namespaces`, and `max-root-links` (including
explicit unlimited values).
`statistics` accepts `none`, `basic`, or `advanced`;
the domain choices come from the current tenant's configured Origin pool. This host's
database seeding creates only missing draft product metadata and preserves
administrative edits. Publish the product and configure/publish its plans in
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
- `max-namespaces` alone controls user namespaces. A positive or unlimited grant
  permits new user namespaces and new links in owned namespaces, and limits the
  current tenant/user's namespaces across all Origins. A missing grant or a value
  `<= 0` means the benefit is absent: new user namespaces and new links in them are
  denied. Default-namespace links do not require this feature. Lowering a positive
  limit below current usage blocks additional namespaces but leaves existing
  namespaces usable; lowering it to `0` also blocks new links in them.
- `max-root-links` independently limits all non-deleted root-path links, including
  historical links, disabled links, expired links, and Origin-null legacy rows.
  Every root link also consumes `max-links`; custom aliases additionally consume
  `max-custom-codes`. Denied, zero, finite, and unlimited grants remain distinct.
  Committed link deletion releases link quotas; rollback does not. Existing
  redirects and link management never require a namespace/root grant.
- Creation is serialized per tenant/owner through ABP distributed locking and a
  transactional UOW. `ShortLinkManager.CreateAndSaveAsync` checks and inserts under
  that lock; an ambient transaction retains the lock until commit or rollback/disposal.
  Compound workflows acquire namespace-configuration, domain-configuration, owner,
  then namespace locks; reverse acquisition is explicitly rejected.
  The older `CreateAsync` only constructs an entity and is not an atomic persistence API.
  Nontransactional, read-uncommitted, repeatable-read, and snapshot UOWs are rejected
  by the atomic entry point; use read-committed or serializable transactions.
  A lock acquisition failure never falls back to an unlocked write.
- `statistics=none` (and a missing value) hides ordinary-user statistics and rejects
  ordering by visit count. `basic` discloses only total visits, including list/get,
  mutation responses, ordering, and the statistics endpoint. `advanced` additionally
  exposes unique visitors, daily trends, referrers, browsers, and devices.
  `ShortLinkDto.TotalVisitCount` remains nullable: `null` means not disclosed, not
  zero visits. Administrative responses still return actual counts under their
  existing permissions.
- Each tenant manages its own HTTP/HTTPS Origin pool and must have one enabled default
  Origin. The default is available to every otherwise eligible creator. The `domains`
  Set grants additional enabled Origins; stale or disabled values do not authorize
  new links. Creation requires an explicit Origin, and a link's Origin is immutable.
  Codes are unique per Origin/namespace, so the same code can be reused on another path.
  Disabling an Origin blocks new links but leaves existing redirects working. Any
  Origin referenced by a namespace or a link, including a soft-deleted link, can only be disabled
  and cannot be deleted.
- Redirects continue collecting visits without consulting subscriptions. Restored
  statistics access includes history that remains within the original retention policy.
- `GET /api/short-link/public/links/capabilities` returns the authenticated user's
  current usage, quota state, finite limit/remaining capacity or unlimited flag,
  statistics level, the default namespace, namespace/root quota usage, and currently
  selectable Origins. The ShortLink page displays
  these values; the Subscription UI stays generic. A displayed capability is not a
  reservation or a substitute for server-side authorization and quota checks.
- Each server-side entitlement check uses then-effective rights. Later checks see
  expired/revoked assignments or changed defaults; already-checked in-flight operations
  may finish. Subscription administration is not globally locked against link creation.

Before enabling the bridge, publish the product and configure an appropriate default
plan or assign explicit subscriptions. Seeding does **not** grant quota or statistics
rights automatically. Without a usable quota grant, creation is denied; without a
statistics value, statistics are denied. The tenant's default Origin remains available
without a `domains` grant, while existing links continue to resolve.
Existing plans and subscription snapshots are not automatically granted the new
namespace/root benefits. Configure those benefits explicitly in the existing typed
subscription administration UI; missing benefits never fall back to standalone grants.
Host role seeding adds namespace management permissions to the default `user` role,
but permissions alone do not grant subscription benefits.
Removing `ShortLinkSubscriptionApplicationModule` from a Host restores ShortLink's
setting-backed total quota, normal statistics, enabled namespaces, and unlimited
additional namespace/root quotas without a database migration. Both
modules remain usable on their own. Multi-instance ShortLink deployments must configure
a shared ABP lock provider; this host uses Redis. Process-local locks cannot enforce a
cross-instance quota.

No payments, checkout, automatic renewal, or subscription purchase/upgrade flow is
implemented by this integration. The upgrade migration adds typed entitlement storage
and the tenant Origin catalog, converts legacy statistics values (`false` to `basic`,
`true` to `advanced`), and attaches legacy links to the seeded default Origin.

The `RemoveShortLinkNamespaceEntitlement` migration retires the former Boolean
`namespaces` feature of product `short-link`; deploy it together with this build,
which no longer defines that feature. It deletes stored `namespaces` values from
plans and subscription snapshots. A positive or unlimited `max-namespaces` value is
kept only where `namespaces=true` was also stored; otherwise it becomes `0`, so the
upgrade never grants new namespace rights. Rights that relied on `namespaces=true`
without a positive quota are removed; grant a positive `max-namespaces` value
explicitly where they are still wanted. Affected plans and subscriptions receive a
new concurrency stamp, so reload open administration pages and assignment previews.
Downgrading restores `namespaces=true` wherever a positive or unlimited quota
remains; quotas set to `0` by the upgrade are not restored.

## Liquid-glass Razor Pages theme

The WebHost selects `SayHello.LiquidGlass` from the reusable
[`SayHello.LiquidGlassTheme` module](modules/SayHello.LiquidGlassTheme/README.md).
Application, Account, Empty, existing module pages, and ABP widgets retain their
normal forms, permissions, antiforgery, menus, and scripts. Light/dark/system modes,
local SVG scenes, navigation capsules, and hover transitions adapt the earlier
liquid-glass demo to real Razor Pages rather than replacing business content.

The optional renderer uses decorative proxy surfaces only. Unavailable/disabled
WebGL, reduced motion, and forced colors fall back to CSS; no CDN or CSP relaxation
is required. Host client libraries come from ABP Theme.Shared `~10.6.0`, not Lepton.
Configure `LiquidGlassThemeOptions` or the `LiquidGlassTheme` configuration section;
see the module README for integration, layout overrides, page attributes, tests,
and third-party notices. The module is Razor Pages / MVC only, not Angular or Blazor.

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

### Focused theme and creation-form regression

The frontend checks use Node.js 20+ and its built-in test runner without additional
packages. The host-owned creation-form tests cover namespace/root selection, quotas,
loading, stale responses, and retry behavior. The script keeps its helpers as function
expressions so ABP bundle minification preserves their lexical scope.

```powershell
node --test .\modules\SayHello.LiquidGlassTheme\tests\liquid-glass.test.mjs .\host\test\SayHello.ShortLink.WebHost.Web.Tests\Frontend\short-link-index.test.mjs
dotnet test .\host\test\SayHello.ShortLink.WebHost.Web.Tests\SayHello.ShortLink.WebHost.Web.Tests.csproj --filter "FullyQualifiedName~LiquidGlass|FullyQualifiedName~Index_Tests|FullyQualifiedName~SubscriptionRouteTests"
```

To run the creation-form checks against a saved, actual ABP-minified page bundle,
set `SHORT_LINK_BUNDLE_PATH` to that JavaScript file for the Node test command.

### PostgreSQL subscription integration tests

The normal test run uses SQLite. PostgreSQL-specific subscription tests are opt-in:
set `SUBSCRIPTION_TEST_POSTGRES_CONNECTION_STRING` to an **isolated PostgreSQL 17
test instance**, using an account allowed to create databases. Never use a production
connection string. The tests create uniquely named databases, apply the real host
migrations, and delete only the databases they created.
Coverage includes fresh schemas, upgrading existing `AddSubscriptions` data with
nullable defaults, Boolean-to-Enum statistics conversion, typed entitlement constraints,
Origin/domain indexes and restrictive references, same-product subscription references,
catalog lock leases, short-link namespace upgrades and guarded downgrades, and retiring
the `namespaces` entitlement, including its downgrade.

```powershell
$env:SUBSCRIPTION_TEST_POSTGRES_CONNECTION_STRING = '<isolated PostgreSQL test connection string>'
try {
    dotnet test .\host\test\SayHello.ShortLink.WebHost.EntityFrameworkCore.Tests\SayHello.ShortLink.WebHost.EntityFrameworkCore.Tests.csproj --filter FullyQualifiedName~PostgreSql
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
- `Redis__Configuration`: StackExchange.Redis configuration. Redis holds the distributed
  cache, distributed locks, Data Protection keys, and the per-user creation rate-limit
  counters shared by all Host instances. A host that does not replace
  `IShortLinkCreationRateLimiter` gets the module's per-process in-memory limiter, which
  enforces the hourly limit only for a single instance.
- `ShortLink__Urls__BaseUrl`: bootstrap/default Origin used by data seeding and as a
  diagnostic legacy fallback during migration. New links and QR codes use the
  immutable Origin stored on each link.
- `ShortLink__Security__OwnHosts__0`: public short-link host, blocked as a recursive target.
- `ShortLink__Privacy__VisitorHashKey`: at least 32 random UTF-8 bytes.
- `Settings__Abp.Mailing.*`: SMTP and sender settings.
- `OpenIddict__ServerCertificate__*`: production signing/encryption certificate.

See `deploy/README.md` for the complete VPS procedure.
