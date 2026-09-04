using Volo.Abp.Modularity;

namespace SayHello.ShortLink.WebHost;

/* Inherit from this class for your domain layer tests. */
public abstract class WebHostDomainTestBase<TStartupModule> : WebHostTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
