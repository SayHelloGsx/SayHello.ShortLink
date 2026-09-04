using Volo.Abp.Modularity;

namespace SayHello.ShortLink.WebHost;

public abstract class WebHostApplicationTestBase<TStartupModule> : WebHostTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
