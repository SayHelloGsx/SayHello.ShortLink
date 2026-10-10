# Domain model, repositories, modules, and events

Read this file when designing an aggregate, adding a repository/domain service, changing module dependencies, or choosing an event boundary.

## Entity and aggregate selection

Choose the least broad ABP base type that supplies the required behavior:

| Type | Use |
|---|---|
| `Entity<TKey>` | Child or standalone entity identity without aggregate-root semantics |
| `BasicAggregateRoot<TKey>` | Aggregate root that intentionally does not need extra properties or a concurrency stamp |
| `AggregateRoot<TKey>` | Aggregate root with `IHasExtraProperties` and `IHasConcurrencyStamp` |
| Audited aggregate-root variants | Add only the creation/modification/deletion audit fields the use case needs |

ABP 10.6 source confirms that `AggregateRoot<TKey>` initializes `ConcurrencyStamp` and `ExtraProperties`; do not reimplement them. Constructors/factories must establish minimum valid state and accept the ID generated outside the entity. Keep the ORM constructor protected. Expose domain-language methods such as `AddLine`, `Publish`, or `Revoke`, not public state setters or mutable collection access.

An aggregate root owns its children and enforces their uniqueness, lifecycle transitions, totals, and other invariants. A child becomes another aggregate root only when it has an independent consistency and transaction boundary. Reference external aggregates by ID; do not add navigation properties across aggregate boundaries just to simplify an EF query.

Use a value object when equality is determined by values and the type owns validation/normalization rules. Keep value objects persistence-neutral and configure their EF representation in the provider project.

## Domain services

Place a stateless domain service in Domain when a rule spans aggregates or needs a domain-facing dependency such as a repository, clock, setting abstraction, or capability provider. Name it with domain language, commonly `*Manager`; accept and return domain values/entities, not DTOs.

- Put an invariant in the entity when it only needs that aggregate's state.
- Pass actor ID, tenant ID, and granted capability facts from Application. Do not make domain rules depend on authentication/UI concepts such as `CurrentUser`.
- Use `GuidGenerator` and `Clock` available from `DomainService`.
- Do not use a domain service as a transaction script, mapping layer, or app-service proxy.

## Repositories and specifications

Define a custom repository interface in Domain only for a meaningful domain query/operation. Keep signatures provider-neutral: no EF types, `IQueryable`, tracking objects, or provider exceptions in the interface. Implement EF-specific code in EntityFrameworkCore.

- Use one repository per aggregate root. Modify children through the root.
- Generic `IRepository<TEntity, TKey>` is enough for simple aggregate persistence.
- ABP officially exposes `GetQueryableAsync()` from `IRepository`; using it inside an application-service UOW is supported for ordinary applications. Execute it asynchronously through the repository's `AsyncExecuter` or the installed provider integration.
- Do not confuse that convenience with a domain contract: never return `IQueryable` from a custom repository/application interface. Encapsulate complex/reusable/provider-specific queries in the provider implementation.
- Reusable modules that must support stores without `IQueryable` may depend on `IBasicRepository`; ordinary EF Core/MongoDB applications can normally use `IRepository`.
- Preserve ABP's `includeDetails` convention: single-item loads normally include details; list queries normally do not unless requested.
- Accept `CancellationToken cancellationToken = default` on custom async methods and use the provider repository's cancellation-token helper.
- Use a `Specification<TEntity>` only when a predicate is genuinely reusable and provider-compatible; do not hide one-off application filtering behind a specification.

`AddDefaultRepositories()` creates defaults for aggregate roots. `includeAllEntities: true` is an explicit compatibility/infrastructure choice, not permission to mutate aggregate children directly. This repository currently uses it in the ShortLink EF module; do not copy or remove it without auditing consumers and tests. The Subscription module explicitly registers aggregate-root repositories.

## Modules and dependency direction

Each module class declares direct runtime dependencies with `[DependsOn]`, and project references must describe the same graph. Do not rely on a transitive dependency as if it were direct.

`AdditionalAssembly` is a documented escape hatch when several assemblies intentionally form one module. Official guidance still prefers giving the assembly its own module and using `[DependsOn]`; never use `AdditionalAssembly` to hide an incorrect layer or surface dependency.

- Domain.Shared has no dependency on Domain.
- Domain depends only on Domain.Shared and domain-level abstractions.
- Application.Contracts depends on Domain.Shared, not Domain.
- Application depends on its contracts and Domain.
- HttpApi depends on contracts and delegates through service interfaces.
- EF Core depends on Domain and implements its persistence abstractions.
- Hosts compose application, HTTP API, provider, authentication, and UI modules.

Reusable modules configure their own services, options, virtual files, mappings, and contributors. Middleware and provider selection belong in the final host. Expose intentional customization through options or virtual public/protected methods; do not make every private implementation detail an extension point.

When one business module needs another:

1. Use the other module's public application contract for a synchronous use case when runtime coupling is intended.
2. Use a small distributed ETO for an asynchronous integration boundary or future service split.
3. Use IDs or a narrow domain-facing abstraction when only identity/capability is required.
4. Keep the dependency one-way; never reach into another module's entity, repository, `DbContext`, or internal app service.

## Domain and integration events

An aggregate may register local/distributed events with `AddLocalEvent` and `AddDistributedEvent`. ABP's EF Core `AbpDbContext` collects these events from changed entities during `SaveChanges`.

- Local events are in-process and may participate in the current UOW. Use them for behavior that belongs to the same application boundary.
- Distributed events cross module/service boundaries. The default `LocalDistributedEventBus` also runs in-process, so test the configured broker separately. Use stable event names and serialization-safe ETOs; never serialize entities, lazy-loading proxies, or request-scoped services.
- Treat distributed delivery as retryable. Handlers must be idempotent and tolerate duplicate/out-of-order delivery where the transport permits it.
- Events published inside a UOW are deferred to UOW completion by default. Keep that behavior unless immediate publication is explicitly required.
- Configure transactional outbox/inbox in the actual host/provider when database state and messages must be coordinated. Merely calling `IDistributedEventBus` does not prove durable delivery.
- Do not perform irreversible external work from an aggregate method. Record intent with an event and execute it at the correct post-commit/retryable boundary.

## Official ABP 10.6 sources

- Docs: [DDD](https://abp.io/docs/10.6/framework/architecture/domain-driven-design), [entities](https://abp.io/docs/10.6/framework/architecture/domain-driven-design/entities), [repositories](https://abp.io/docs/10.6/framework/architecture/domain-driven-design/repositories), [domain services](https://abp.io/docs/10.6/framework/architecture/domain-driven-design/domain-services), [modularity](https://abp.io/docs/10.6/framework/architecture/modularity/basics), [event bus](https://abp.io/docs/10.6/framework/infrastructure/event-bus).
- Source: [`AggregateRoot<TKey>`](https://github.com/abpframework/abp/blob/10.6.0/framework/src/Volo.Abp.Ddd.Domain/Volo/Abp/Domain/Entities/AggregateRoot.cs), [`BasicAggregateRoot<TKey>`](https://github.com/abpframework/abp/blob/10.6.0/framework/src/Volo.Abp.Ddd.Domain/Volo/Abp/Domain/Entities/BasicAggregateRoot.cs), and [`AbpDbContext` event collection](https://github.com/abpframework/abp/blob/10.6.0/framework/src/Volo.Abp.EntityFrameworkCore/Volo/Abp/EntityFrameworkCore/AbpDbContext.cs).
