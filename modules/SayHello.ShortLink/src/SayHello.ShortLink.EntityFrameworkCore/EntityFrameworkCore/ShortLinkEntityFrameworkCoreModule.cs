using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinks;
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
        });
    }
}
