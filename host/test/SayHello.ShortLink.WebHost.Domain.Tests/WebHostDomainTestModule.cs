using Volo.Abp.Modularity;

namespace SayHello.ShortLink.WebHost;

[DependsOn(
    typeof(WebHostDomainModule),
    typeof(WebHostTestBaseModule)
)]
public class WebHostDomainTestModule : AbpModule
{

}
