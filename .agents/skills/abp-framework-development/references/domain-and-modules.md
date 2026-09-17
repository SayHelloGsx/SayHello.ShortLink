# Domain model, repositories, modules, and events

Read this file when designing an aggregate, adding a repository or domain service, defining module dependencies, or choosing an event boundary.

## Entity and aggregate selection

Use `AggregateRoot<TKey>` or an appropriate audited aggregate-root base class when the object is a consistency boundary. ABP aggregate roots include `IHasConcurrencyStamp` and `IHasExtraProperties`; use `BasicAggregateRoot<TKey>` only when those features are intentionally unwanted. Audit base types add the corresponding creation/modification/deletion audit properties—select the least broad base type that meets the use case.

An aggregate root owns its internal entities. Expose operations such as `AddLine`, `ChangeStatus`, or `Cancel`, rather than exposing mutable collections or public setters that can create impossible states. For a collection, enforce uniqueness, allowed transitions, totals, and other invariants in the root.

Only add an entity as a separate aggregate root if it has its own independent consistency/transaction boundary. Otherwise, make it a child entity and access it through the root. Refer to external aggregate roots by their primary key.

## Domain services and repositories

Place a domain service in the Domain project when a core rule is stateless and spans aggregates or needs an abstraction such as a repository. A domain service must express domain language, not become a grab-bag for application orchestration.

Put repository interfaces in Domain when the domain/application requires a domain-specific query or operation. Keep their return types and signatures provider-neutral. Implement custom EF Core repository methods in the EF Core project. A generic `IRepository<TEntity, TKey>` is usually sufficient for ordinary aggregate persistence.

Do not give aggregate children their own repositories solely for convenience. ABP generates default repositories for aggregate roots by default; enabling repositories for every entity is a deliberate infrastructure decision, not the standard aggregate workflow.

## Modules and public contracts

Each project/module class declares its direct dependencies with `[DependsOn(typeof(...))]`. Match project references to the layered dependency direction; for example, an Application project can depend on its Domain and Application.Contracts projects, while Domain must not reference Application or EF Core.

When one business module needs another:

1. Prefer the other module's application contract for a synchronous use case.
2. Prefer a distributed event plus a small ETO for an asynchronous integration boundary or a future service split.
3. Keep the dependency one-way and use IDs/contracts, not another module's entity type or EF Core `DbContext`.

Local events are for behavior that remains in the same process. Distributed events are for inter-module/inter-service communication. In a modular monolith, a distributed event bus can run in-process, leaving a migration path to a real transport. Once a real distributed provider is used, configure inbox/outbox where delivery consistency matters; handlers must tolerate retries and be idempotent.

References: [Entities and aggregate roots](https://abp.io/docs/latest/framework/architecture/domain-driven-design/entities), [Repositories](https://abp.io/docs/latest/framework/architecture/domain-driven-design/repositories), [Domain services](https://abp.io/docs/latest/framework/architecture/domain-driven-design/domain-services), [Modularity](https://abp.io/docs/latest/framework/architecture/modularity/basics), [Event bus](https://abp.io/docs/latest/framework/infrastructure/event-bus).
