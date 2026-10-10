# Concurrency, EF Core, migrations, and tests

Read this file before implementing an update/delete contract, batch mutation, EF Core mapping/repository, migration, data seed, or persistence test.

## Why the concurrency assignment is required

`AggregateRoot<TKey>` implements `IHasConcurrencyStamp`. In ABP 10.6:

1. `ConfigureByConvention()` maps `ConcurrencyStamp` as an EF Core concurrency token.
2. Before saving a modified/deleted entity, `AbpDbContext.UpdateConcurrencyStamp` copies the entity's current stamp into EF's `OriginalValue`.
3. ABP generates a new stamp for the outgoing row.
4. EF includes the original stamp in the update predicate; a zero-row update becomes `DbUpdateConcurrencyException`, which ABP wraps as `AbpDbConcurrencyException`.

Therefore, the client stamp must be assigned to the loaded entity before save. A detached DTO comparison or newly generated application stamp is not the standard ABP flow.

## Required mutation contract

```csharp
public class UpdateOrderDto : IHasConcurrencyStamp
{
    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;

    public string ShippingAddress { get; set; } = string.Empty;
}

public virtual async Task<OrderDto> UpdateAsync(Guid id, UpdateOrderDto input)
{
    var order = await _orderRepository.GetAsync(id);

    order.ConcurrencyStamp = input.ConcurrencyStamp;
    order.ChangeShippingAddress(input.ShippingAddress);

    await _orderRepository.UpdateAsync(order, autoSave: true);
    return OrderMapper.ToDto(order);
}
```

- Return `ConcurrencyStamp` in every read/output DTO used to initiate a later mutation.
- Require it in update/action DTOs, normally through `IHasConcurrencyStamp`.
- Assign it explicitly before invoking allowed behavior and saving.
- Save before mapping when the response must contain the newly generated stamp (`autoSave: true` or ambient `SaveChangesAsync`).
- Saving only flushes the current UOW; it does not commit the ambient transaction, and a later failure can still roll the operation back.
- For stale-delete protection, accept a version input, assign its stamp to the loaded entity, and delete that entity.
- Never regenerate the input stamp, silently replace it with the current database value, or retry a conflict automatically.
- Let `AbpDbConcurrencyException` propagate through the configured conflict response. If the module translates it, preserve conflict status/semantics and test that contract; a default `BusinessException` mapping is not equivalent.

A manual pre-comparison may improve an error message but is not a substitute for the EF concurrency token during the write. Bulk methods, `ExecuteUpdate`/`ExecuteDelete`, raw SQL, and provider bulk libraries may bypass ABP's tracked-entity flow; do not use them where per-row concurrency is required without an equivalent checked predicate and affected-row assertion.

## EF Core mapping

Keep all EF Core concerns in the provider project. For every mapped ABP entity:

```csharp
builder.Entity<Order>(b =>
{
    b.ToTable(ModuleDbProperties.DbTablePrefix + "Orders", ModuleDbProperties.DbSchema);
    b.ConfigureByConvention();

    b.Property(x => x.Number).IsRequired().HasMaxLength(OrderConsts.MaxNumberLength);
    b.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
});
```

Call `ConfigureByConvention()` once before custom mapping. ABP 10.6 uses it for concurrency stamp, extra properties, auditing, soft delete, and multi-tenancy conventions. Do not add a competing row-version/concurrency mapping unless the provider design deliberately replaces the string stamp and is covered by integration tests.

- Map table/schema through the module's `*DbProperties`.
- Configure lengths, required fields, value objects/owned types, delete behavior, unique constraints, and indexes explicitly.
- Include `TenantId` and relevant visibility/filter columns in uniqueness and query indexes when semantics require tenant scoping.
- Configure relationships within an aggregate; use ID-only references between aggregates.
- Review provider-specific filtered indexes and SQL against PostgreSQL production and SQLite tests. A SQLite-only passing test does not prove PostgreSQL syntax/semantics.

## DbContext and repositories

- Register the module `DbContext` with `AddAbpDbContext`.
- Prefer `AddDefaultRepositories()` for aggregate roots. Use `includeAllEntities: true` only for a known compatibility need; it must not legitimize child-entity mutation.
- Register custom repository implementations with `AddRepository<TEntity, TRepository>()`.
- Inside an EF repository, use `GetDbSetAsync`, `GetQueryableAsync`, `WithDetailsAsync`, and `GetCancellationToken(cancellationToken)`.
- Keep `IQueryable`, `DbSet`, EF expressions tied to provider-only types, and `DbContext` out of application contracts/domain interfaces.
- Make read-only queries no-tracking only when their result will not be mutated. Load a tracked aggregate for commands.
- Allow-list sorting fields and append a stable tie-breaker for paging.
- Preserve ABP data filters unless a narrow, authorized operation explicitly disables one.

This repository has an intentional difference to remember: `SayHello.ShortLink.EntityFrameworkCore` currently enables `includeAllEntities: true`, while `SayHello.Subscription.EntityFrameworkCore` registers aggregate repositories explicitly. Do not normalize either approach incidentally.

## Migrations and data seeding

When the persistent model changes, locate the host migration project and design-time factory before invoking `dotnet ef`. Generate a new migration, inspect every operation, and ensure the host `DbContext` includes the module mapping. Never rewrite a migration already applied to shared environments.

Use `IDataSeedContributor` for repeatable seed behavior. Seeders must be idempotent, use stable IDs only when cross-referenced, honor `DataSeedContext.TenantId`/`CurrentTenant.Change`, and avoid assuming an empty database solely from an unrelated table count. Prefer the DbMigrator flow when the solution uses it so migrations and seed contributors run together.

## Test checklist

- Domain: constructors and behavior methods reject invalid state and preserve child/root invariants.
- Application: automatic validation runs through the proxied interface; permission and ownership failures are distinct; output DTOs expose only allowed data.
- Tenant/filter: host, tenant A, tenant B, disabled filters, and soft-deleted rows behave intentionally.
- Concurrency: read one aggregate twice, commit writer A, submit writer B's original stamp, and assert `AbpDbConcurrencyException` or the explicitly mapped 409 contract. Assert writer A's response contains a changed stamp.
- EF Core: run custom query/mapping/filter tests with the module's SQLite integration base, plus a PostgreSQL-focused test for provider-specific SQL/index/locking behavior.
- HTTP: verify routes, status codes, serialization, and Admin/Public isolation when the contract changes.
- Worker/job: disable automatic execution in unrelated tests and invoke the unit of work/idempotency path deterministically.

Prefer ABP integrated tests with real DI and SQLite over mocking internal repositories. Mock only true external systems or narrow process boundaries.

## Official ABP 10.6 sources

- Docs: [concurrency](https://abp.io/docs/10.6/framework/infrastructure/concurrency-check), [EF Core](https://abp.io/docs/10.6/framework/data/entity-framework-core), [EF Core best practices](https://abp.io/docs/10.6/framework/architecture/best-practices/entity-framework-core-integration), [repositories](https://abp.io/docs/10.6/framework/architecture/domain-driven-design/repositories), [data seeding](https://abp.io/docs/10.6/framework/infrastructure/data-seeding), [integration tests](https://abp.io/docs/10.6/testing/integration-tests).
- Source: [`AggregateRoot<TKey>`](https://github.com/abpframework/abp/blob/10.6.0/framework/src/Volo.Abp.Ddd.Domain/Volo/Abp/Domain/Entities/AggregateRoot.cs), [`ConfigureByConvention` and `TryConfigureConcurrencyStamp`](https://github.com/abpframework/abp/blob/10.6.0/framework/src/Volo.Abp.EntityFrameworkCore/Volo/Abp/EntityFrameworkCore/Modeling/AbpEntityTypeBuilderExtensions.cs), and [`AbpDbContext.UpdateConcurrencyStamp`](https://github.com/abpframework/abp/blob/10.6.0/framework/src/Volo.Abp.EntityFrameworkCore/Volo/Abp/EntityFrameworkCore/AbpDbContext.cs).
