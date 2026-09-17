For ABP Framework C# work, load and follow `.agents/skills/abp-framework-development/SKILL.md`.

Respect the project’s ABP version and existing conventions. Keep domain behavior in Domain, contracts in Application.Contracts, use-case orchestration in Application, and EF Core details in the provider project.

For mutable aggregate updates, preserve the `ConcurrencyStamp` flow: include it in read and update DTOs, assign the incoming value to the loaded entity before update, and do not suppress concurrency conflicts.