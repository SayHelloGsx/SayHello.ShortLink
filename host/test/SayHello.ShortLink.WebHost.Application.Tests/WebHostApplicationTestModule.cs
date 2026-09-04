using Volo.Abp.Modularity;

namespace SayHello.ShortLink.WebHost;

[DependsOn(
    typeof(WebHostApplicationModule),
    typeof(WebHostDomainTestModule)
)]
public class WebHostApplicationTestModule : AbpModule
{

}
