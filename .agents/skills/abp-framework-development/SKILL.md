---
name: abp-framework-development
description: "Implement, modify, review, or refactor C# code in an ABP Framework solution using its DDD, modular, application-service, persistence, and infrastructure conventions. Use for modules, entities, repositories, DTOs, application services, Admin/Public surfaces, EF Core, permissions, validation, tenancy, UOW, events, caching, settings, features, background execution, HTTP APIs, or concurrency; not for plain ASP.NET Core projects that do not use ABP."
metadata:
  short-description: ABP Framework development conventions
---

# ABP Framework Development

Build the requested feature within the existing solution's ABP version, module boundaries, conventions, and enabled providers. Prefer ABP services and base classes over hand-rolled equivalents, but do not add a framework abstraction where ordinary domain code is clearer.

## Repository baseline

Re-detect these facts before every change rather than assuming this section is permanently current:

- Projects target .NET 10 and currently pin ABP packages to `10.6.0` in individual project files.
- Business modules use layered Domain.Shared, Domain, Application.Contracts, Application, HttpApi, Web, and EntityFrameworkCore projects. The main modules also split Common, Admin, and Public surfaces and provide thin umbrella modules for combined hosts.
- Mapperly is the configured object mapper. Do not introduce AutoMapper or a second mapping path unless the project is deliberately migrating.
- The host uses EF Core with PostgreSQL; EF Core integration tests use SQLite. Provider-sensitive SQL, indexes, and migrations must be checked against both relevant paths.
- APIs are generally explicit controllers with stable Area, route, and remote-service constants. Do not silently add conventional controllers that duplicate them.
- The solution already uses settings, typed distributed caches, background workers, distributed locks, multi-tenancy, and explicit transaction helpers. Reuse those local patterns before creating alternatives.

## Before changing code

1. Identify the exact ABP package version, target module and host, database provider, mapper, API style, and existing test base. Treat local code as the source of truth where it intentionally specializes ABP defaults.
2. Trace the use case through `.Domain.Shared`, `.Domain`, `.Application.Contracts`, `.Application`, provider, HTTP API, UI, and host composition. Include `.Common.*`, `.Admin.*`, and `.Public.*` when present.
3. Confirm aggregate ownership, tenant/host scope, permission and resource-ownership rules, setting/feature dependencies, audit/soft-delete behavior, concurrency contract, transaction boundary, and external side effects.
4. Search for an existing aggregate behavior, repository query, mapper, error code, permission, cache key, lock, worker, or test fixture before adding one.
5. Read every relevant reference below. Verify exact APIs against the installed package or the matching official source tag; never infer an overload from a different ABP major/minor version.

## Architecture and dependencies

ABP's DDD model separates presentation, application, domain, and infrastructure. Keep business invariants in the domain; application services coordinate a use case; infrastructure implements persistence and external technical concerns.

- `.Domain.Shared`: domain constants, enums, error codes, localization resources, and serialization-safe event contracts that must be shared without referencing Domain. Permission definitions normally belong to the relevant Application.Contracts surface; keep permission constants in Domain.Shared only when the repository deliberately shares them there.
- `.Domain`: aggregates, entities, value objects, domain services, repository interfaces, and domain events. It must not depend on Application or EF Core.
- `.Application.Contracts`: DTOs, application-service interfaces, permissions, and remotely exposed contracts. Do not expose entities.
- `.Application`: application-service implementations, authorization at the use-case boundary, DTO mapping, and orchestration.
- `.HttpApi`: transport adapters over application contracts. Keep controllers thin and do not move business rules into them.
- `.EntityFrameworkCore` (or another provider): `DbContext`, mappings, repository implementations, migrations, and provider-specific query code. Do not leak provider types into domain/application contracts.
- Host projects compose modules, select providers, and configure middleware. Reusable business modules must not depend on a host.

Project references and `[DependsOn]` declarations must express the same direct dependency graph. Do not compensate for a wrong graph with `AdditionalAssembly`, scan unrelated assemblies, or create cycles. Across business modules, use a public application contract for a synchronous boundary, an integration/event contract for asynchronous coupling, or an ID reference instead of another module's entity, repository, or `DbContext`.

## Core ABP conventions

- Use `Clock`/`IClock` for current time and `GuidGenerator`/`IGuidGenerator` for new GUID identities. Do not use `DateTime.Now`, `DateTime.UtcNow`, or `Guid.NewGuid()` in application/domain behavior unless a documented low-level exception requires it.
- Prefer ABP conventional DI (`ApplicationService`, `DomainService`, `ITransientDependency`, `IScopedDependency`, `ISingletonDependency`). Register explicitly when selecting an implementation, replacing a service, binding options, or requiring a deliberate lifetime.
- Reuse base-class services such as `CurrentUser`, `CurrentTenant`, `Clock`, `GuidGenerator`, `AuthorizationService`, `FeatureChecker`, `DataFilter`, and `LazyServiceProvider` instead of injecting duplicates.
- Keep async code async. Pass the ambient/request cancellation token through custom repository, lock, cache, job, and external-service calls.
- Automatic validation, authorization, and UOW behavior use interception. Services must be resolved through DI, and intercepted calls must go through an interface or an interceptable virtual method. Make public/protected extension points virtual in reusable modules.
- Use namespaced `BusinessException` codes plus localization and `.WithData(...)` for domain failures. Do not expose infrastructure exception messages or convert all failures into `UserFriendlyException`.

## Domain model

- Model a consistency boundary as an aggregate root. It preserves its own invariants and those of its children; retrieve and modify child entities through the root, not their own repository.
- Give roots constructors/factory methods with the minimum valid state and behavior methods that name business operations. Prefer private/protected setters when unrestricted mutation could violate an invariant. Keep ORM-only constructors protected.
- Reference another aggregate by ID, not a navigation property. Use a value object for a value with identity-independent equality and domain rules.
- Put a rule in an entity/root when it only needs that aggregate's state. Use a stateless `DomainService` when it spans aggregates or requires a domain-facing dependency. Pass actor/tenant facts into the domain rather than coupling domain behavior to `CurrentUser`.
- Use ABP's `GuidGenerator` (available from `ApplicationService`/`DomainService`, or inject `IGuidGenerator`) for new GUID identities.
- Default repositories are provided for aggregate roots. Add repository interfaces and custom methods only for meaningful domain queries; do not build repositories for aggregate children by default.

ABP's `IRepository` exposes `GetQueryableAsync()` and it is valid for ordinary application queries inside the active UOW. For reusable modules, strict provider independence, or complex reusable queries, prefer a provider-neutral custom repository method; never return `IQueryable` through an application contract.

Read [domain models and modules](./references/domain-and-modules.md) for entity types, repository/query boundaries, modules, specifications, and events.

## Application contracts and services

- Define application-service interfaces and DTOs in `Application.Contracts`; put implementations in `Application`. DTOs are transport shapes, not domain objects or persistence models.
- Validate transport shape with data annotations or `IValidatableObject`; use the installed FluentValidation integration only if the module already depends on it. Keep domain invariants enforced by the domain model.
- An application service expresses one use case: authorize, load the aggregate, enforce ownership, invoke domain behavior/domain services, persist through repositories, and return a DTO.
- Do not call another application service in the same module to reuse business logic. Move the reusable rule into Domain or use a focused application-layer helper for orchestration-only mechanics.
- Use CRUD base services only when their default flow represents the use case; otherwise implement explicit methods or override the appropriate hooks rather than bypassing authorization, filtering, or mapping.
- Keep query projections, paging, and sorting deterministic and provider-compatible; allow-list client sorting fields. Use no-tracking only for read paths that do not subsequently mutate the tracked entity.
- Use the repository's Mapperly/manual mapper pattern. Explicitly control server-managed, identity, audit, tenant, extra-property, and concurrency fields; mapping is neither authorization nor domain validation.

Read [application and cross-cutting behavior](./references/application-and-crosscutting.md) for DTOs, validation, permissions, UOW, tenancy, mapping, exceptions, and HTTP exposure.

## Admin and Public application surfaces

Split a business module into Admin and Public application surfaces when privileged management operations and audience-facing operations have materially different authorization, contracts, exposure, consumers, or deployment needs. Do not create empty parallel projects only for symmetry.

- Admin and Public are application/API boundaries over the same domain and persistence model, not duplicate domain modules.
- Sibling projects must not reference each other. Each depends on its own contracts and any genuinely shared Common layer.
- Public outputs expose only safe fields and public queries enforce published/visible state server-side. Admin operations require explicit management permissions.
- `Public` describes the audience-facing surface, not an authorization decision. Anonymous access must still be granted deliberately.
- Give each surface distinct namespaces, routes, remote-service identities, and controller assemblies. A selective host must not load the other surface indirectly.

Read [Admin and Public application surfaces](./references/admin-and-public-apps.md) before changing separate Admin/Public contracts, applications, HTTP APIs/clients, UI modules, or host composition.

## Concurrency is a required mutation contract

For mutable aggregate roots, `AggregateRoot` and its audited variants implement `IHasConcurrencyStamp`. For every update endpoint/use case using it:

1. Return `ConcurrencyStamp` in the read/output DTO.
2. Require it in the update/action DTO, typically through `IHasConcurrencyStamp`.
3. Load the entity and assign `entity.ConcurrencyStamp = input.ConcurrencyStamp` before domain behavior/mapping.
4. Persist within the ambient UOW. Save before mapping when the response must contain the refreshed stamp. `autoSave`/`SaveChangesAsync` flushes state but does not commit the ambient transaction.
5. Let `AbpDbConcurrencyException` follow the configured conflict path; never swallow or blindly retry it.

Apply an equivalent explicit stamp contract to deletes when stale-delete protection is required. Do not map the stamp accidentally or translate a conflict to a generic business error unless the public contract still preserves conflict semantics. Avoid bulk updates/deletes when per-row optimistic concurrency is required.

Read [concurrency and EF Core](./references/concurrency-and-efcore.md) before changing mutation flows, EF Core mappings, migrations, or persistence tests.

## Infrastructure, events, and side effects

- Application-service and repository methods conventionally run in an ambient ABP UOW. Avoid manual UOWs unless a worker, non-intercepted service, or explicit transaction/isolation boundary requires one.
- Treat a use case as a transaction boundary. Do not perform non-idempotent external side effects before persistence succeeds. Configure inbox/outbox where durable database/message coordination is required.
- Use local events for in-process behavior and distributed ETOs for module/service integration. ABP's default distributed bus is still in-process; a real broker changes transaction and failure semantics. Distributed payloads must be serialization-safe contracts, not entities.
- Distinguish startup options, dynamic settings, tenant features, and user permissions; none replaces a domain invariant.
- Treat caches as derived state. Preserve tenant/visibility dimensions and use UOW-aware cache mutation (`considerUow: true` where supported) or post-commit invalidation when stale shared state would be incorrect.
- A distributed lock is coordination, not a replacement for a database constraint or optimistic concurrency. Hold it through the protected transaction's completion.
- Use a background job for queued/retriable finite work and a background worker for recurring work. Both must be idempotent and cluster-aware.

Read [infrastructure and hosting](./references/infrastructure-and-hosting.md) for DI/interception, events/outbox/inbox, time, exceptions, settings/features, caching, locking, jobs/workers, and host/API composition.

## Persistence and tests

Keep `DbContext` configuration, mappings, migrations, and custom EF Core repositories in the EF Core project. Call `ConfigureByConvention()` for every mapped ABP entity before custom configuration. Apply ABP data filters and repository helpers rather than duplicating tenant/soft-delete predicates.

Test behavior at the correct boundary: domain invariant tests; application-service authorization, validation, concurrency, and tenant-scope tests; EF Core integration tests for mappings/queries/filters/provider behavior; HTTP tests for routes and surface isolation. Include a two-writer concurrency test for every important mutable aggregate.

## Version and source discipline

The principles above are stable, but package names, overloads, mapping integration, generated API behavior, event transport, UOW options, and provider details vary by version. For the current baseline, prefer [ABP 10.6 documentation](https://abp.io/docs/10.6/) and the [ABP `10.6.0` source tag](https://github.com/abpframework/abp/tree/10.6.0). If package versions change, use the matching docs/tag and update this baseline.

Official starting points: [DDD](https://abp.io/docs/10.6/framework/architecture/domain-driven-design), [application services](https://abp.io/docs/10.6/framework/architecture/domain-driven-design/application-services), [EF Core](https://abp.io/docs/10.6/framework/data/entity-framework-core), [concurrency](https://abp.io/docs/10.6/framework/infrastructure/concurrency-check), and [official ABP engineering skills](https://github.com/abpframework/abp/tree/10.6.0/.claude/skills).
