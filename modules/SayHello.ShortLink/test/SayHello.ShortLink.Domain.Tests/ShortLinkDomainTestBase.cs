using Volo.Abp.Modularity;

namespace SayHello.ShortLink;

/* Inherit from this class for your domain layer tests.
 * See SampleManager_Tests for example.
 */
public abstract class ShortLinkDomainTestBase<TStartupModule> : ShortLinkTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
