# Admin and Public application surfaces

Read this file when a business module serves both privileged management users and an audience-facing application, especially when those APIs can be consumed or deployed independently.

## Decide whether to split

Admin/Public separation is an exposure and use-case boundary, not a required project pair for every module.

Split the surfaces when one or more of these differ materially:

- authorization policies or resource-ownership rules;
- DTO fields and allowed operations;
- public versus internal lifecycle states, such as published and draft content;
- HTTP exposure, remote-service clients, UI consumers, or deployment hosts;
- release or compatibility requirements.

Keep the normal Application.Contracts/Application structure when the module is small and the audiences, contracts, and deployment boundary are genuinely the same. Do not add empty Admin, Public, or Common projects merely to imitate another module.

Admin normally owns back-office use cases such as creation, configuration, moderation, publication, and full lifecycle management. Public owns audience-facing reads and explicitly supported interactions. `Public` does not mean anonymous: decide authorization per use case with the solution's normal `[Authorize]`, `[AllowAnonymous]`, permission, ownership, tenant, and feature checks.

## Project and dependency layout

When a full split is justified, use the solution's naming convention for projects equivalent to:

```text
<Module>.Common.Application.Contracts
<Module>.Common.Application
<Module>.Admin.Application.Contracts
<Module>.Admin.Application
<Module>.Admin.HttpApi
<Module>.Public.Application.Contracts
<Module>.Public.Application
<Module>.Public.HttpApi
```

Add Admin/Public HTTP API clients or presentation projects only when those surfaces are independently consumed. The Common layer is optional. It is appropriate for contracts, base classes, mapping helpers, and implementation utilities whose semantics are identical and safe on both surfaces; it must not become a miscellaneous application layer or hide audience-specific use cases.

Keep dependencies one-way:

1. Admin.Contracts and Public.Contracts may each depend on Common.Contracts; neither references the other.
2. Admin.Application and Public.Application each depend on their own Contracts and may depend on Common.Application; neither calls or references the other.
3. Each HttpApi module depends on its matching Contracts module and any genuinely shared HttpApi module.
4. Common.Application may depend on Domain, and Common.Contracts may depend on Domain.Shared. Domain and persistence providers never depend on Admin or Public application projects.
5. Project references and `[DependsOn]` declarations must describe the same direct dependency graph.

If duplicate application logic appears, first move business rules to the aggregate or a domain service. Use Common.Application only for application-layer mechanics that cannot belong to Domain; never make a Public service call an Admin service, or the reverse.

ABP CMS Kit demonstrates optional umbrella projects such as `<Module>.Application.Contracts`, `<Module>.Application`, and `<Module>.HttpApi` that compose both Admin and Public modules. Such projects should contain no audience-specific implementation. Use them only for an all-in-one host or compatibility. A separately deployed Admin or Public host must reference the specific modules it needs rather than an umbrella that transitively loads both surfaces.

## Contracts and application behavior

- Define separate service interfaces and DTOs whenever fields, validation, permissions, or compatibility can diverge. Do not reuse an Admin DTO as a convenient superset for Public output.
- Put each surface in an explicit `Admin` or `Public` namespace and follow the local service naming pattern, such as `PageAdminAppService` and `PagePublicAppService`.
- Admin reads may expose drafts, moderation state, audit context, or internal configuration only under the required management permissions.
- Public queries must enforce publication, visibility, ownership, tenant, soft-delete, and feature rules on the server. Hiding a field or record only in the UI is not a boundary.
- Public writes such as comments, profile actions, or submissions require the same domain invariants as Admin writes plus the appropriate authentication and resource checks.
- Both surfaces operate on the same aggregates, repositories, domain services, events, and persistence mappings. Do not duplicate entities or create separate tables merely because the APIs are split.
- Apply the normal optimistic-concurrency contract independently to every mutable Admin or Public update endpoint that uses concurrency stamps.

Share a DTO only when its meaning and safe field set are truly identical for both audiences. Prefer a small shared DTO over making Public depend on an Admin contract.

## HTTP API and host composition

- Place controllers in separate Admin/Public assemblies and module namespaces so a host can load either surface without loading the other.
- Use distinct route prefixes, remote-service names, API explorer areas, and client registrations according to the repository's convention. Verify that conventional-controller generation cannot create route or service-name collisions.
- Register each controller assembly through its own module using the ABP-version-appropriate mechanism. Do not use `AdditionalAssembly` or an umbrella dependency to smuggle the other surface into a host.
- Protect Admin at the application-service boundary even when controllers also declare authorization. Public endpoints must declare or inherit the intended authenticated/anonymous policy rather than becoming anonymous by naming convention.
- If one host intentionally serves both surfaces, compose both modules explicitly or use a thin existing umbrella module. Keep selective hosts on the specific dependency path.

## Verification

Test the boundary itself, not only the business result:

- a Public-only test host cannot resolve or route Admin services/controllers, and the converse holds when required;
- Admin methods reject callers without the exact management permissions;
- Public reads exclude drafts, unpublished, soft-deleted, cross-tenant, or otherwise invisible data and omit sensitive fields;
- authenticated Public mutations enforce ownership and resource authorization;
- routes, OpenAPI/API explorer groups, and generated remote clients remain distinct;
- the project/module dependency graph has no Admin-to-Public or Public-to-Admin reference.

CMS Kit is a useful structural example, not a template to copy blindly. Inspect the source matching the solution's ABP version before relying on exact project names or module APIs: [CMS Kit source](https://github.com/abpframework/abp/tree/dev/modules/cms-kit/src) and [ABP modularity](https://abp.io/docs/latest/framework/architecture/modularity/basics).
