# Application services and cross-cutting ABP behavior

Read this file for application-service/API work, DTOs, permissions, UOW, multi-tenancy, auditing, soft delete, mapping, or GUIDs.

## Contracts, DTOs, and mapping

Keep service interfaces and DTOs in `Application.Contracts`. Use distinct create, update, and output DTOs where their allowable fields differ. Do not accept entity types over the boundary.

Create explicit mappings in the solution's configured object-mapping integration. Mapping is not authorization or domain validation. For create/update mappings, ignore or explicitly control `Id`, audit properties, `TenantId`, `ConcurrencyStamp`, and any domain-derived state. In particular, set the concurrency stamp explicitly in an update method after loading the aggregate.

`ApplicationService` exposes common services including `GuidGenerator`, `CurrentTenant`, and `ObjectMapper`; inject the appropriate abstraction if not deriving from it. Create root IDs with ABP's `IGuidGenerator`/`GuidGenerator`.

## Authorization

Define named permissions in the module's permission-definition provider, keep permission constants in the shared project, and protect application-service methods with `[Authorize(PermissionName)]` (or use an established equivalent). A class-level requirement plus method-level exceptions is fine when it matches the use case.

Use `AllowAnonymous` only deliberately. A successful permission check does not replace resource/business authorization: after loading the aggregate, enforce ownership, state, tenant, and other rules relevant to that resource. Do not expose an operation just because it is a conventional CRUD method.

## Unit of work

ABP conventionally creates an ambient UOW for application-service, controller, and repository methods; nested repository calls join it. In normal command methods, change tracked entities and allow completion to save/commit. Force a save only when later code needs generated data (notably a refreshed `ConcurrencyStamp`) or the exact scenario requires it.

GET/read flows normally should not mutate state. The default transaction behavior for HTTP methods and precise UOW attributes/options is version-sensitive; inspect the installed ABP version before changing it. Use a manual UOW only for a clearly justified boundary; choose transactional behavior explicitly when necessary.

## Multi-tenancy and filters

For tenant-owned data, implement `IMultiTenant` and maintain `TenantId` at creation according to the module's tenant model. ABP applies the current-tenant filter, so do not manually add duplicate tenant predicates unless a provider/query requires it. Do not trust a caller-supplied tenant ID to cross scopes.

Use `CurrentTenant.Change(tenantId)` only in a tightly scoped host-side operation with explicit authorization. Disabling `IMultiTenant` filtering is exceptional and does not query multiple per-tenant databases automatically.

For soft deletion, implement `ISoftDelete` or use the appropriate full-audited base class when deletion audit details are needed. ABP filters soft-deleted records by default. Use `IDataFilter.Disable<TFilter>()` only in the narrowest `using` scope and only after considering authorization, tenant isolation, and the reason hidden data is needed. Use hard deletion only when the business/retention policy explicitly permits it.

References: [Application services](https://abp.io/docs/latest/framework/architecture/domain-driven-design/application-services), [Authorization](https://abp.io/docs/latest/framework/fundamentals/authorization), [Unit of work](https://abp.io/docs/latest/framework/architecture/domain-driven-design/unit-of-work), [Multi-tenancy](https://abp.io/docs/latest/framework/architecture/multi-tenancy), [Data filtering](https://abp.io/docs/latest/framework/infrastructure/data-filtering), [GUID generation](https://abp.io/docs/latest/framework/infrastructure/guid-generation).
