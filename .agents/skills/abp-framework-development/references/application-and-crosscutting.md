# Application services and cross-cutting ABP behavior

Read this file for application-service/API work, contracts, DTO validation, authorization, UOW, multi-tenancy, mapping, exception behavior, and API exposure.

## Contracts and DTOs

Keep application-service interfaces and DTOs in the relevant Application.Contracts project. Interfaces should inherit `IApplicationService`. Use separate query, create, update, action, and output DTOs when their fields or compatibility can diverge; never accept or return entities.

- Prefer ABP DTO bases/interfaces such as `EntityDto<TKey>`, audited DTOs, paging inputs, and `IHasConcurrencyStamp` when their semantics apply.
- Keep route IDs as separate parameters when the local contract uses `UpdateAsync(Guid id, UpdateDto input)`; do not duplicate an authoritative ID inside the body.
- Use transport-safe types. ASP.NET Core-specific types belong in HttpApi; use the ABP remote-stream contract when a remote application service intentionally transfers files.
- Public DTOs expose only fields authorized for that audience. An output mapper must not leak `TenantId`, audit details, internal state, or extra properties by convention.

## Validation and interception

Use data annotations for structural input validation and `IValidatableObject` for cross-property transport rules that cannot be expressed declaratively. Use FluentValidation only when its ABP integration is already installed. Domain invariants remain in entities/domain services even if a DTO rejects the same invalid shape early.

Application services are validation-enabled automatically, but interception only occurs when the service is resolved through DI and the call goes through an interface or an interceptable virtual method. Do not instantiate application services manually in production or tests. Do not disable validation to work around a bad contract.

Propagate cancellation through custom async methods. When a contract does not expose a token, use the `CancellationTokenProvider.Token` supplied by `ApplicationService` for repositories, caches, locks, and external calls.

An application service should:

1. authorize the operation;
2. resolve current user/tenant and other caller context;
3. load the aggregate;
4. enforce resource ownership/visibility;
5. invoke aggregate/domain-service behavior;
6. persist through a repository;
7. map the saved result.

Do not call a sibling application service to share logic inside the same module. Extract domain behavior or a focused application helper. Avoid business logic in controllers, page models, mappers, or DTO validation.

## Authorization and capabilities

Define permission constants and `PermissionDefinitionProvider` in the relevant Application.Contracts surface unless the repository has a deliberate shared placement. Localize permission display names. Protect use cases with `[Authorize(Permission)]` or `CheckPolicyAsync`; use `[AllowAnonymous]` only where explicitly required.

A permission answers whether the current principal may attempt an operation. It does not prove ownership, tenant scope, aggregate state, or feature entitlement. Enforce those independently after loading the resource. Never accept caller-supplied user/tenant identity as authoritative.

Features describe tenant/customer capabilities, settings hold configurable values, and domain rules preserve invariants. Do not replace one with another.

## Mapping

This repository uses Mapperly plus explicit/manual mappers. Follow the owning module's mapper registration and `ObjectMapperContext`; do not introduce AutoMapper.

- Map outputs explicitly when audience filtering or derived values matter.
- For create/update input, allow-list mutable fields. Never bulk-map onto a loaded aggregate with protected invariants.
- Explicitly control `Id`, `TenantId`, audit fields, `ExtraProperties`, derived state, and `ConcurrencyStamp`.
- Map extra properties only when both contracts are intentionally extensible and safe for that surface; use the installed Mapperly `[MapExtraProperties]` integration rather than copying the dictionary blindly.
- Set `ConcurrencyStamp` deliberately in the command method as described in the concurrency reference, not as an incidental mapper side effect.

## Unit of work

Application-service, controller, and repository methods conventionally participate in an ambient UOW. In ABP 10.6, `UnitOfWorkInterceptor` chooses the default transaction behavior and completes the UOW after the intercepted method succeeds.

- Let nested repository calls join the ambient UOW.
- By default an HTTP GET has a UOW without a database transaction; other HTTP verbs use a transaction when the provider supports one. Keep GET operations side-effect free.
- Make mutation boundaries transactional when the invariant requires it; use `[UnitOfWork(isTransactional: true)]` when convention is insufficient or ambiguous.
- Do not call `SaveChangesAsync` after every repository operation. Save early only when later logic needs database-generated state, a refreshed concurrency stamp, or immediate constraint detection.
- `autoSave: true` and `CurrentUnitOfWork.SaveChangesAsync()` flush changes but do not commit the ambient transaction; a later failure can still roll them back.
- Start a manual UOW for workers/non-intercepted services or a deliberately new transaction/isolation boundary, not as boilerplate. Call `CompleteAsync` on a manually started UOW.
- Do not assume that a domain-service method automatically starts a UOW when invoked outside a conventional boundary.
- Never return success or publish irreversible external effects before the owning UOW commits.

## Multi-tenancy, auditing, and filters

For tenant-owned data, implement `IMultiTenant`. ABP entity construction initializes `TenantId` from `CurrentTenant`; when a factory sets it explicitly, use the trusted current scope rather than a caller-supplied ID. Rely on ABP's current-tenant filter. Do not add redundant predicates as a substitute for a missing filter, and never move an existing aggregate between tenants by casually changing `TenantId`.

Use `CurrentTenant.Change(tenantId)` only inside a tightly scoped, authorized host-side operation. Disabling `IMultiTenant` filtering does not query other tenant databases in database-per-tenant deployments.

For soft deletion, implement `ISoftDelete` or use the audited base type that matches retention needs. `IDataFilter.Disable<TFilter>()` must be a narrow `using` scope with an explicit reason and authorization review. Hard delete only under an intentional retention policy.

Audit properties are framework-managed metadata, not user-editable fields. Action auditing and entity-change history are separately configurable; entity history is not automatically enabled for every entity. Mark passwords, tokens, secrets, and other sensitive parameters/properties with the repository's `[DisableAuditing]` pattern and verify audit options. Use `Clock` for domain/application time so ABP time normalization and tests remain consistent.

## Exceptions and localization

Use namespaced error constants such as `Module:010001` in Domain.Shared and map the code namespace to the module localization resource with `AbpExceptionLocalizationOptions`. Add dynamic values with `.WithData(...)`; the localization text can reference those names.

- `BusinessException`: expected domain/business failure.
- `EntityNotFoundException`: missing entity when repository semantics do not already throw it.
- `AbpAuthorizationException`: authentication/authorization failure.
- `AbpValidationException`: usually produced by automatic input validation.
- `UserFriendlyException`: only when a pre-localized free-form message is truly the contract; error-code localization is usually more maintainable.

Do not catch a framework exception merely to return a success-shaped result or leak internal details. Configure exceptional HTTP mappings centrally with `AbpExceptionHttpStatusCodeOptions` when the default status is not the public contract.

## HTTP API exposure

Choose either conventional controllers or explicit controllers for a service surface; do not expose the same app service twice. This repository normally uses explicit, thin controllers that implement the contract interface and delegate to it.

- Keep `[Area]`, `[RemoteService(Name = ...)]`, route prefixes, and Admin/Public constants stable.
- When an app service should only be reached through an explicit controller, follow the local `[RemoteService(false)]` convention to prevent duplicate conventional exposure.
- HttpApi references Application.Contracts, not concrete Application implementations.
- Keep serialization, headers, file transport, and HTTP-specific status behavior in HttpApi; keep authorization and use-case behavior in Application.

## Official ABP 10.6 sources

- Docs: [application services](https://abp.io/docs/10.6/framework/architecture/domain-driven-design/application-services), [validation](https://abp.io/docs/10.6/framework/fundamentals/validation), [authorization](https://abp.io/docs/10.6/framework/fundamentals/authorization), [UOW](https://abp.io/docs/10.6/framework/architecture/domain-driven-design/unit-of-work), [multi-tenancy](https://abp.io/docs/10.6/framework/architecture/multi-tenancy), [data filtering](https://abp.io/docs/10.6/framework/infrastructure/data-filtering), [auditing](https://abp.io/docs/10.6/framework/infrastructure/audit-logging), [cancellation](https://abp.io/docs/10.6/framework/infrastructure/cancellation-token-provider), [object mapping](https://abp.io/docs/10.6/framework/infrastructure/object-to-object-mapping), [exception handling](https://abp.io/docs/10.6/framework/fundamentals/exception-handling), [auto controllers](https://abp.io/docs/10.6/framework/api-development/auto-controllers).
- Source: [`ValidationInterceptor`](https://github.com/abpframework/abp/blob/10.6.0/framework/src/Volo.Abp.Validation/Volo/Abp/Validation/ValidationInterceptor.cs), [`AuthorizationInterceptor`](https://github.com/abpframework/abp/blob/10.6.0/framework/src/Volo.Abp.Authorization/Volo/Abp/Authorization/AuthorizationInterceptor.cs), and [`UnitOfWorkInterceptor`](https://github.com/abpframework/abp/blob/10.6.0/framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWorkInterceptor.cs).
