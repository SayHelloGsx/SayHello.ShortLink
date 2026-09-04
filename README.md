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
