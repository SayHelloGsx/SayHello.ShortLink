# Admin and Public application surfaces

Read this file when a business module serves both privileged management users and an audience-facing application, especially when those APIs can be consumed or deployed independently.

This repository already uses Common/Admin/Public splits in the ShortLink and Subscription modules. Preserve their existing contracts, remote-service constants, explicit controllers, and thin umbrella composition modules unless the requested change intentionally alters deployment boundaries.

## Decide whether to split

Admin/Public separation is an exposure and use-case boundary, not a required project pair for every module. Split when one or more of these differ materially:

- authorization policies or resource-ownership rules;
- DTO fields and allowed operations;
- public versus internal lifecycle states, such as published and draft;
- HTTP exposure, remote clients, UI consumers, or deployment hosts;
- release or compatibility requirements.

Keep the normal Application.Contracts/Application structure when the audiences and deployment boundary are genuinely the same. Do not add empty Admin, Public, or Common projects for symmetry.

Admin normally owns back-office creation, configuration, moderation, publication, and full lifecycle management. Public owns audience-facing reads and explicitly supported interactions. `Public` does not mean anonymous: decide authorization per use case with `[Authorize]`, `[AllowAnonymous]`, permissions, ownership, tenant, and feature checks.

## Project and dependency layout

When a full split is justified, follow the local equivalent of:

```text
<Module>.Common.Application.Contracts
<Module>.Common.Application
<Module>.Common.HttpApi
<Module>.Common.HttpApi.Client
<Module>.Common.Web
<Module>.Admin.Application.Contracts
<Module>.Admin.Application
<Module>.Admin.HttpApi
<Module>.Admin.HttpApi.Client
<Module>.Admin.Web
<Module>.Public.Application.Contracts
<Module>.Public.Application
<Module>.Public.HttpApi
<Module>.Public.HttpApi.Client
<Module>.Public.Web
<Module>.Application.Contracts   (optional umbrella)
<Module>.Application             (optional umbrella)
<Module>.HttpApi                 (optional umbrella)
<Module>.HttpApi.Client          (optional umbrella)
<Module>.Web                     (optional umbrella)
```

Create only the projects the module needs. Add matching HTTP API clients or UI projects only when independently consumed. The Common layer may contain base application classes, shared-safe contracts, mapping helpers, cache/options infrastructure, module-wide workers, and orchestration utilities whose semantics are identical for both audiences; it must not hide audience-specific use cases.

Keep dependencies one-way:

1. Admin.Contracts and Public.Contracts may depend on Common.Contracts; neither references the other.
2. Admin.Application and Public.Application each depend on their own Contracts and may depend on Common.Application; neither calls/references the other.
3. Common.Application depends on Common.Contracts and Domain; Common.Contracts depends inward on Domain.Shared.
4. Each Admin/Public HttpApi depends on its matching Contracts and Common.HttpApi. It does not need the concrete Application implementation.
5. Each matching HttpApi.Client depends on its Contracts and Common.HttpApi.Client, and registers proxies with that surface's remote-service name.
6. Each Admin/Public Web module depends on its matching Contracts and Common.Web. The host separately composes local application implementations or remote clients.
7. Domain and persistence never depend on Common/Admin/Public application or presentation projects.
8. Project references and `[DependsOn]` declarations describe the same direct graph.

If duplicate application logic appears, first move business rules to an aggregate/domain service. Use Common.Application only for application mechanics that cannot belong to Domain.

Optional umbrella Application.Contracts, Application, HttpApi, HttpApi.Client, and Web modules may compose both surfaces for an all-in-one host or compatibility. They should contain no audience-specific implementation. A selective host references only the surface modules it serves.

## Contracts and application behavior

- Define separate interfaces and DTOs whenever fields, validation, permissions, or compatibility can diverge. Do not reuse an Admin DTO as a Public superset.
- Use explicit `Admin`/`Public` namespaces and local naming such as `ProductAdminAppService`.
- Admin reads expose draft, moderation, audit, or internal configuration fields only under management permissions.
- Public queries enforce publication, visibility, ownership, tenant, soft-delete, and feature rules on the server.
- Public writes enforce the same domain invariants as Admin writes plus authentication/resource checks.
- Both surfaces operate on the same aggregates, repositories, domain services, events, and persistence mappings.
- Apply the optimistic-concurrency contract independently to every mutable surface.
- Keep permission definition providers with the owning contracts surface. A Public permission is still a permission.

Share a DTO only when its meaning and safe field set are truly identical. Prefer a small Common DTO over a Public dependency on Admin contracts.

## HTTP API and host composition

- Place controllers in separate Admin/Public assemblies and namespaces so a host can load either surface independently.
- Reuse `[Area(...)]`, `[RemoteService(Name = ...)]`, stable `api/<module>/<surface>/...` routes, and the owning `*RemoteServiceConsts`.
- Explicit controllers implement/delegate to the Application.Contracts interface, never a concrete app service.
- Follow the local `[RemoteService(false)]` pattern on app services where needed to prevent duplicate conventional exposure.
- If conventional controllers are intentional, configure only the owning contracts/application assembly and root path. Check for route, action-name, and remote-service collisions.
- Register each controller assembly through its own module. Do not use `AdditionalAssembly` or an umbrella dependency to smuggle the other surface into a selective host.
- Protect Admin at the application-service boundary even when controllers also authorize. Public endpoints must explicitly inherit or declare authenticated/anonymous behavior.
- Verify both local and generated remote-client composition: route, verb, body/stream binding, serialization, and remote-service identity must implement the same application contract.

## Verification

- A Public-only host cannot resolve or route Admin services/controllers, and conversely where isolation is required.
- Admin methods reject callers without the exact management permission.
- Public reads exclude drafts, unpublished, soft-deleted, cross-tenant, or otherwise invisible data and omit sensitive fields.
- Authenticated Public mutations enforce ownership and resource authorization.
- Routes, API explorer groups, remote-service names, and generated clients remain distinct.
- Local implementations and remote proxies satisfy the same surface contract.
- The project/module graph has no Admin-to-Public or Public-to-Admin reference.
- Umbrella modules contain composition only.

CMS Kit is a useful structural example, not a template to copy blindly. Use [CMS Kit 10.6 source](https://github.com/abpframework/abp/tree/10.6.0/modules/cms-kit/src), [ABP modularity](https://abp.io/docs/10.6/framework/architecture/modularity/basics), and [auto API controllers](https://abp.io/docs/10.6/framework/api-development/auto-controllers).
