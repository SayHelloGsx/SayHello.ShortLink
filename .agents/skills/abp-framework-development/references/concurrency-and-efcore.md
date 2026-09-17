# Concurrency, EF Core, and tests

Read this file before implementing an update, a batch change, EF Core persistence, migrations, or the associated tests.

## Optimistic concurrency flow

ABP optimistic concurrency applies when an entity implements `IHasConcurrencyStamp`; standard `AggregateRoot` base classes already do. On creation ABP assigns a unique stamp. On a normal tracked update it compares the entity's stamp to the supplied value and changes it on success. A mismatch produces `AbpDbConcurrencyException`.

Use this update shape, adapting only to installed APIs and local conventions:

```csharp
public virtual async Task<OrderDto> UpdateAsync(Guid id, UpdateOrderDto input)
{
    var order = await _orderRepository.GetAsync(id);

    order.ConcurrencyStamp = input.ConcurrencyStamp;
    order.ChangeShippingAddress(input.ShippingAddress);

    await _orderRepository.UpdateAsync(order, autoSave: true);
    return ObjectMapper.Map<Order, OrderDto>(order);
}
```

`UpdateOrderDto` and `OrderDto` must expose the property, for example by implementing `IHasConcurrencyStamp`. The code must assign the incoming value to the loaded entity before update; an output DTO's newly returned value is usable by the next client update. If `autoSave: true` is not appropriate, call the ambient unit of work's `SaveChangesAsync()` before returning the mapped DTO when the caller needs the new stamp.

Never overwrite the stamp with a newly generated application value, omit it from updates, or silently replace it with the database's current value: each defeats stale-write detection. Do not hide `AbpDbConcurrencyException`; present the solution's standard conflict response or propagate it.

`UpdateManyAsync`/`DeleteManyAsync`, raw SQL, and provider-specific bulk operations may bypass change tracking and optimistic concurrency. Do not use them for a use case that requires per-row conflict detection unless the installed provider's behavior is verified and equivalent.

## EF Core boundaries

Keep EF Core configuration in the EF Core project. Configure table names, keys, owned/value-object mappings, lengths, indexes, relations, and provider-specific conversions there. Ensure a property participating in ABP concurrency has the correct mapping for the installed ABP EF Core integration; do not add a competing custom concurrency configuration without verifying it against ABP's current implementation.

Use repository queries or `GetQueryableAsync` only inside the appropriate UOW. Do not return `IQueryable` through an application contract. Use `DisableTracking()` only for read-only paths; a detached entity cannot safely be treated like the currently loaded aggregate in a concurrency-protected command.

Create and review EF Core migrations for schema changes. Do not edit historical migrations already applied to shared environments; add a corrective migration under the solution's migration policy.

## Test checklist

- Domain: constructors and behavior methods reject invalid state and preserve child/root invariants.
- Application: unauthorized callers fail; allowed callers receive the expected DTO; tenant A cannot read/write tenant B's entity; soft-deleted records follow the intended filter behavior.
- Concurrency: read the same aggregate twice (or keep the original output DTO), complete update A, then update with B's original `ConcurrencyStamp`; assert `AbpDbConcurrencyException` or the solution's translated conflict result. Assert the successful response has a changed stamp when returned after save.
- EF Core: verify custom queries, mappings, filters, and migration effects against the provider integration rather than only mocking a repository.

References: [Concurrency check](https://abp.io/docs/en/abp/latest/Concurrency-Check), [EF Core integration](https://abp.io/docs/latest/framework/infrastructure/data-access/entity-framework-core), [Repositories](https://abp.io/docs/latest/framework/architecture/domain-driven-design/repositories), [Testing](https://abp.io/docs/latest/framework/fundamentals/testing).
