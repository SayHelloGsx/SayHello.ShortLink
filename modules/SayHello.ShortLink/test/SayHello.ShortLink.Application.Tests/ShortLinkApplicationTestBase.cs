using Volo.Abp.Modularity;

namespace SayHello.ShortLink;

/* Inherit from this class for your application layer tests.
 * See SampleAppService_Tests for example.
 */
public abstract class ShortLinkApplicationTestBase<TStartupModule> : ShortLinkTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
