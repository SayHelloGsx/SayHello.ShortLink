---
name: abp-framework-development
description: Implement, modify, review, or refactor C# code in an ABP Framework solution using its DDD, modular, and application-service conventions. Use for ABP modules, entities, repositories, DTOs, application services, Admin/Public application surfaces, EF Core mappings, permissions, tenancy, UOW, domain events, or concurrency handling; not for plain ASP.NET Core projects that do not use ABP.
metadata:
  short-description: ABP Framework DDD and module-development conventions
---

# ABP Framework Development

Build the requested feature within the existing solution's ABP version, module boundaries, conventions, and enabled providers. Prefer ABP services and base classes over hand-rolled equivalents, but do not add a framework abstraction where ordinary domain code is clearer.

## Before changing code

1. Identify the ABP version from the central package management file or project references, the target module(s), and existing conventions. Treat the existing solution as the local source of truth where it intentionally differs from generic guidance.
2. Trace the requested use case across `.Domain.Shared`, `.Domain`, `.Application.Contracts`, `.Application`, and provider/presentation projects. Where the solution separates audiences, also trace the corresponding `.Common.*`, `.Admin.*`, and `.Public.*` projects. Make the smallest set of compatible changes.
3. Confirm the data ownership, tenant scope, authorization rule, audit/soft-delete requirements, and whether an existing aggregate or service already owns the behavior.
4. Read the relevant reference below before working in that area. For APIs whose exact availability differs by ABP or provider version, inspect the installed package/source or current official documentation before coding.

## Architecture and dependencies

ABP's DDD model separates presentation, application, domain, and infrastructure. Keep business invariants in the domain; application services coordinate a use case; infrastructure implements persistence and external technical concerns.

- `.Domain.Shared`: shared constants, localization resources, permission names/definitions, enums, and cross-layer contracts that are genuinely shared.
- `.Domain`: aggregates, entities, value objects, domain services, repository interfaces, and domain events. It must not depend on Application or EF Core.
- `.Application.Contracts`: DTOs and application-service interfaces exposed to clients or other modules. Do not expose entities.
- `.Application`: application-service implementations, authorization at the use-case boundary, DTO mapping, and orchestration.
- `.EntityFrameworkCore` (or another provider): `DbContext`, mappings, repository implementations, migrations, and provider-specific query code. Do not leak provider types into domain/application contracts.

Declare only direct module dependencies with `[DependsOn]`; do not compensate for an incorrect dependency graph with `AdditionalAssembly`. Across business modules, prefer a public application contract, a carefully designed integration/event contract, or an ID reference over reaching into another module's internals. Avoid cyclic dependencies.

## Domain model

- Model a consistency boundary as an aggregate root. It preserves its own invariants and those of its children; retrieve and modify child entities through the root, not their own repository.
- Give roots constructors/factory methods with the minimum valid state and behavior methods that name business operations. Prefer private/protected setters when unrestricted mutation could violate an invariant. Keep ORM-only constructors protected.
- Reference another aggregate by ID, not a navigation property. Use a value object for a value with identity-independent equality and domain rules.
- Put a rule in an entity/root when it only needs that aggregate's state. Use a stateless `DomainService` when it spans aggregates or requires a domain-facing dependency. Do not move domain rules to an application service merely to make persistence convenient.
- Use ABP's `GuidGenerator` (available from `ApplicationService`/`DomainService`, or inject `IGuidGenerator`) for new GUID identities; do not use `Guid.NewGuid()` unless a project-specific exception requires it.
- Default repositories are provided for aggregate roots. Add repository interfaces and custom methods only for meaningful domain queries; do not build repositories for aggregate children by default.

Read [references/domain-and-modules.md](references/domain-and-modules.md) for entity types, repository boundaries, modules, and events.

## Application contracts and services

- Define application-service interfaces and DTOs in `Application.Contracts`; put implementations in `Application`. DTOs are transport shapes, not domain objects or persistence models.
- Validate input at the boundary with normal ABP/.NET validation attributes or project conventions. Keep domain invariants enforced again by the domain model.
- An application service expresses one use case: authorize, load the aggregate, invoke domain behavior/domain services, persist through repositories, and return a DTO. Do not accept an entity from the client or map arbitrary client input directly onto protected business state.
- Use `ApplicationService` when its conveniences (`GuidGenerator`, `CurrentTenant`, `ObjectMapper`, etc.) fit. Use CRUD base services only when their default flow represents the use case; otherwise write an explicit service method and override the appropriate hooks rather than bypassing authorization, filtering, or mapping.
- Keep query projections/paging/sorting deterministic and compatible with the repository/provider. Use no-tracking only for read paths that do not subsequently mutate the tracked entity.
- Configure mapping profiles in the module's established mapper. Explicitly ignore server-managed, identity, audit, tenant, and concurrency properties unless deliberately handling them.

Read [references/application-and-crosscutting.md](references/application-and-crosscutting.md) for DTOs, permissions, UOW, tenancy, audit/filtering, mappings, and cross-module calls.

## Admin and Public application surfaces

Split a business module into Admin and Public application surfaces when privileged management operations and audience-facing operations have materially different authorization, contracts, exposure, consumers, or deployment needs. Do not create empty parallel projects only for symmetry.

- Admin and Public are application/API boundaries over the same domain and persistence model, not duplicate domain modules.
- When split, use dedicated Admin/Public Application.Contracts, Application, and HttpApi modules (plus clients or UI modules when independently consumed). An optional Common application layer may contain only genuinely shared contracts and implementation helpers.
- Admin and Public sibling projects must not reference each other. Each depends on its own contracts and any Common layer. Optional umbrella modules may depend on both solely for all-in-one host composition or compatibility.
- Keep DTOs and services audience-specific. Public outputs expose only safe fields and public queries enforce published/visible state server-side; Admin operations require explicit management permissions.
- `Public` describes the audience-facing surface, not an authorization decision. Anonymous access must still be granted deliberately; authenticated public operations must enforce ownership and other resource rules.
- Give the surfaces distinct controller/module namespaces, routes, remote-service identities, and API explorer areas as the solution requires. A Public-only host must not load Admin controllers or services indirectly.

Read [references/admin-and-public-apps.md](references/admin-and-public-apps.md) before creating, changing, or composing separate Admin/Public application or HTTP API surfaces.

## Concurrency is a required update contract

For a mutable aggregate root, optimistic concurrency is normally already available: `AggregateRoot` and its audited aggregate-root variants implement `IHasConcurrencyStamp`.

For every update endpoint/use case that uses this mechanism:

1. Return `ConcurrencyStamp` in the read/output DTO.
2. Require it in the update DTO, typically by implementing `IHasConcurrencyStamp`.
3. Load the current entity, assign `entity.ConcurrencyStamp = input.ConcurrencyStamp`, then invoke explicit domain behavior/mapping for allowed fields.
4. Persist via the repository within the ambient UOW. When the response must contain the freshly generated stamp, save with `autoSave: true` or otherwise call `CurrentUnitOfWork.SaveChangesAsync()` before mapping the response.
5. Let `AbpDbConcurrencyException` follow the solution's established exception-handling path; do not swallow it or retry blindly. A conflict means the caller must retrieve current state and reconcile.

Do not map the stamp from an untrusted input accidentally while also ignoring it: assign it deliberately. Do not use bulk updates/deletes when the use case requires optimistic concurrency, because provider/bulk behavior can bypass normal change tracking and concurrency checks.

Read [references/concurrency-and-efcore.md](references/concurrency-and-efcore.md) before creating or changing update flows, EF Core mappings, or migrations.

## Events, UOW, and side effects

- Public application-service methods conventionally run in an ambient ABP unit of work. Repository calls participate in it. Avoid manually starting a UOW unless the use case needs a deliberate boundary or options.
- Treat a use case as a transaction boundary. Do not perform non-idempotent external side effects before persistence has succeeded. For distributed messaging, use the installed provider and configure inbox/outbox where the architecture requires reliable delivery.
- Use a local event for strictly in-process behavior; use a distributed event/ETO for module-to-module or service-to-service integration. Distributed event payloads must be serialization-safe contracts, not entities.

## Persistence and tests

Keep `DbContext` configuration, entity mappings, migrations, and custom EF Core repositories in the EF Core project. Apply ABP data filters and repository helpers rather than duplicating tenant/soft-delete predicates. Review a migration whenever the persistent model changes.

Test behavior at the correct boundary: aggregate/domain-service invariant tests; application-service authorization, validation, concurrency and tenant-scope tests; EF Core integration tests for mappings/queries/filters. Include a two-writer concurrency test for an updated aggregate when practical.

Read [references/concurrency-and-efcore.md](references/concurrency-and-efcore.md) for EF Core and test guidance.

## Version-sensitive rules

The principles above are stable, but package names, method overloads, mapping integration, generated API behavior, event transport, UOW options, and database-provider details can vary by ABP major version and provider. Never invent an ABP API from this skill. Verify exact types, namespaces, overloads, and configured modules against the solution and the official documentation for its version.

Official starting points: [DDD architecture](https://abp.io/docs/latest/framework/architecture/domain-driven-design), [entities](https://abp.io/docs/latest/framework/architecture/domain-driven-design/entities), [concurrency](https://abp.io/docs/en/abp/latest/Concurrency-Check), and [modularity](https://abp.io/docs/latest/framework/architecture/modularity/basics).
