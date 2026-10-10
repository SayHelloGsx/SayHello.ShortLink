# Infrastructure services, background execution, and hosting

Read this file when using ABP DI/interception, time, options, settings, features, caching, distributed locks, background jobs/workers, exception localization, or HTTP/host composition.

## Framework primitives and interception

- Use `Clock`/`IClock`, `GuidGenerator`/`IGuidGenerator`, `CurrentUser`, and `CurrentTenant` rather than static system APIs or ambient globals.
- Prefer conventional registration through ABP base classes and dependency marker interfaces. Use explicit registrations for implementation selection/replacement, mapper instances, options, decorators, or intentional lifetimes.
- Reuse `CancellationTokenProvider.Token` where a base class exposes it, and propagate the token to repositories, caches, locks, jobs, and external calls.
- Automatic authorization, validation, UOW, and service-method auditing depend on DI interception; entity audit properties are applied by the persistence integration. Invoke the service through its injected interface or an interceptable virtual method, since direct construction/self-invocation can bypass interceptors.
- Reusable modules configure services in `ConfigureServices` and register startup contributors/workers in the appropriate initialization hook. The final host owns middleware order, provider selection, authentication, database implementation, and deployment-specific options.

## Options, settings, features, permissions, and invariants

Use the mechanism that matches the question:

| Mechanism | Answers | Typical API |
|---|---|---|
| Options/configuration | How is this process deployed/configured? | `IOptions<T>` / `Configure<T>` |
| Setting | What mutable value applies at global, tenant, or user scope? | `ISettingProvider`; `ISettingManager` only in management paths |
| Feature | Is this tenant/customer capability enabled, or what limit was purchased? | `IFeatureChecker`, `[RequiresFeature]` |
| Permission | May this current principal perform the operation? | `[Authorize]`, `IPermissionChecker` |
| Domain invariant | Is this state transition valid regardless of caller/configuration? | Aggregate/domain service |

Define settings/features and their constants in the module layer used by local conventions, with localizable display metadata where applicable. Parse typed setting/feature values deterministically and fail explicitly on invalid persisted configuration. Do not inject setting-management services into public/domain code just to read a value; use the read-only provider or a narrow domain-facing capability abstraction.

## Business errors and localization

Use stable, namespaced error codes in Domain.Shared:

```csharp
throw new BusinessException(ModuleErrorCodes.QuotaExceeded)
    .WithData("Limit", limit);
```

Map the code namespace to the module resource with `AbpExceptionLocalizationOptions` and add localized entries keyed by the full code. Keep infrastructure details in logs, not public messages. Use `AbpExceptionHttpStatusCodeOptions` only when a particular code intentionally differs from ABP's default HTTP mapping. Do not catch/re-wrap `AbpDbConcurrencyException`, cancellation, or authorization failures into a generic business error.

## Distributed events and transactional messages

Use `ILocalEventBus` for behavior intentionally confined to one process. Use `IDistributedEventBus` plus a serialization-safe ETO and stable `[EventName]` for bounded-context/service integration. ABP's default `LocalDistributedEventBus` is still in-process: handlers share the publisher's UOW and do not prove real-broker behavior.

- Distributed events published inside a UOW are deferred to UOW completion by default. Keep `onUnitOfWorkComplete: true` unless immediate publication is an explicit requirement.
- With a real broker, configure an outbox when a committed database change must eventually publish an event. Store outbox records in the same database/transaction as the application data.
- Configure an inbox when incoming handling needs transactional processing and duplicate suppression. Handlers still need idempotent external side effects.
- Outbox/inbox processing requires provider mappings/migrations and a real distributed-lock provider in multi-instance deployments. Keep each configured database/lock identity unique.
- Do not set `useOutbox: false` merely to reduce latency; that intentionally gives up transactional publication.
- Test broker outage, retries, duplicates, process restarts, and ordering assumptions, not only the in-process provider.

## Typed distributed caching

Prefer `IDistributedCache<TCacheItem, TKey>` and annotate stable cache item types with `[CacheName]`.

- The default `IDistributedCache` implementation is process-local memory. Configure a real shared provider when multiple instances must observe the same cache.
- ABP 10.6's key normalizer includes cache name, configured key prefix, and current tenant ID by default. `[IgnoreMultiTenancy]` removes tenant partitioning; use it only for genuinely host/global data.
- Include every other visibility/version dimension in the logical key (owner, locale, audience, resource version, etc.).
- Cache DTO-like serializable data, not tracked entities or service objects.
- Use cache-aside and deterministic expirations. Prevent stampedes with a distributed lock only when repository fallback alone is too expensive.
- Treat cache state as derived. Use `considerUow: true` on ABP cache operations when the mutation should be applied only after successful UOW completion, or invalidate post-commit; never publish uncommitted state to a shared cache.
- `AbpDistributedCacheOptions.HideErrors` defaults to `true`. For correctness/security-critical cache operations, explicitly choose whether failure may degrade to the source of truth or must surface; do not return a false success.

## Distributed locks

Use `IAbpDistributedLock.TryAcquireAsync` with a stable, namespaced key containing tenant/host and resource identity. A null handle means the lock was not acquired: retry according to policy or fail explicitly, but never enter the protected section.

- Dispose the handle with `await using`.
- When the lock protects a database invariant, hold it through the owning UOW's commit/completion; releasing when the method returns can be too early if an outer UOW still owns the transaction.
- A distributed lock does not replace a unique constraint, concurrency stamp, or idempotency key.
- A reusable module may depend on `Volo.Abp.DistributedLocking.Abstractions`, whose fallback is in-process. A multi-instance host must install/configure a real provider before claiming cross-node safety.

## Background jobs versus workers

| Use | Background job | Background worker |
|---|---|---|
| Shape | One finite queued operation | Recurring/long-lived schedule |
| Durability/retry | Provider-backed; normally persisted and retried | Provider-dependent recurring execution |
| Input | Small serialization-safe args | Resolve current state each run |
| Typical examples | Email, export, deferred integration | Cleanup, polling, periodic reconciliation |

For jobs:

- Derive from `AsyncBackgroundJob<TArgs>` or implement the configured abstraction.
- Pass identifiers and immutable scalar data, not entities, streams, service instances, or oversized payloads.
- Make execution idempotent. Do not swallow retryable exceptions; ABP providers use failures to retry/abandon according to configuration.
- Include a trusted tenant ID when work is tenant-specific, then re-read current state inside a scoped `CurrentTenant.Change(tenantId)` and the correct UOW.
- Respect cancellation.
- Queue only when transaction ordering is safe. `IUnitOfWork.OnCompleted` runs after a successful commit but is not durable across process failure; use an outbox or provider-backed transactional design when enqueue/delivery must be guaranteed.

For workers:

- `AsyncPeriodicBackgroundWorkerBase` is singleton-like; resolve scoped repositories/services from `PeriodicBackgroundWorkerContext.ServiceProvider`.
- Open and complete an explicit UOW when repository operations are not already intercepted by an ambient UOW.
- Register workers through the module initialization API and disable them in hosts/tests that must not execute them.
- Every application instance can run the same worker. Make the operation naturally partitioned/idempotent, acquire a distributed lock, or run it in a dedicated worker host.
- Cron support and persistence depend on the selected provider; do not assume the default in-memory timer behaves like Hangfire or Quartz.

## HTTP and host composition

- Keep controllers thin and route only to application contracts. Do not inject repositories/domain services into controllers.
- Choose explicit controllers or ABP conventional controllers per surface. Prevent duplicate exposure with the local `[RemoteService(false)]`, controller-assembly registration, route, and remote-service-name conventions.
- Put only transport concerns in HttpApi: binding, headers, status customization, and stream conversion.
- Configure ASP.NET Core middleware, authentication, OpenAPI, database provider, real cache/lock/job/event providers, and connection strings in the final host.
- Reusable modules must not call host middleware APIs or assume a particular deployment topology.

## Official ABP 10.6 sources

- Docs: [local events](https://abp.io/docs/10.6/framework/infrastructure/event-bus/local), [distributed events and outbox/inbox](https://abp.io/docs/10.6/framework/infrastructure/event-bus/distributed), [settings](https://abp.io/docs/10.6/framework/infrastructure/settings), [features](https://abp.io/docs/10.6/framework/infrastructure/features), [caching](https://abp.io/docs/10.6/framework/fundamentals/caching), [distributed locking](https://abp.io/docs/10.6/framework/infrastructure/distributed-locking), [background jobs](https://abp.io/docs/10.6/framework/infrastructure/background-jobs), [background workers](https://abp.io/docs/10.6/framework/infrastructure/background-workers), [exception handling](https://abp.io/docs/10.6/framework/fundamentals/exception-handling), [auto controllers](https://abp.io/docs/10.6/framework/api-development/auto-controllers).
- Source: [`DistributedCacheKeyNormalizer`](https://github.com/abpframework/abp/blob/10.6.0/framework/src/Volo.Abp.Caching/Volo/Abp/Caching/DistributedCacheKeyNormalizer.cs), [`DistributedCache<TCacheItem,TCacheKey>`](https://github.com/abpframework/abp/blob/10.6.0/framework/src/Volo.Abp.Caching/Volo/Abp/Caching/DistributedCache.cs), and [ABP 10.6 infrastructure skill](https://github.com/abpframework/abp/blob/10.6.0/.claude/skills/abp-infrastructure/SKILL.md).
