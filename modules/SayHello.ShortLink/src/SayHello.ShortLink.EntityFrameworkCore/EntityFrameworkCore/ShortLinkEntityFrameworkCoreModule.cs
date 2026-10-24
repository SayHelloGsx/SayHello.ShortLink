using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinkNamespaces;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink.EntityFrameworkCore;

[DependsOn(
    typeof(ShortLinkDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ShortLinkEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<ShortLinkDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
            options.AddRepository<ShortLinks.ShortLink, EfCoreShortLinkRepository>();
            options.AddRepository<BlockedDomain, EfCoreBlockedDomainRepository>();
            options.AddRepository<ShortLinkDomain, EfCoreShortLinkDomainRepository>();
            options.AddRepository<ShortLinkNamespace, EfCoreShortLinkNamespaceRepository>();
            options.AddRepository<ShortLinkNamespaceReservation, EfCoreShortLinkNamespaceReservationRepository>();
        });

        // These roots have no child collections; their incoming foreign keys only enforce referential
        // integrity between aggregates. Without this, adding a visit, link or namespace while the
        // referenced root is tracked would refresh its ConcurrencyStamp and cause spurious conflicts.
        Configure<AbpEntityChangeOptions>(options =>
        {
            options.IgnoredNavigationEntitySelectors.Add(
                "SayHello.ShortLink.CrossAggregateReferences",
                type => typeof(ShortLinks.ShortLink).IsAssignableFrom(type) ||
                        typeof(ShortLinkDomain).IsAssignableFrom(type) ||
                        typeof(ShortLinkNamespace).IsAssignableFrom(type));
        });
    }
}
