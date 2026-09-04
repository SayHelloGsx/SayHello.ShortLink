using SayHello.ShortLink.Localization;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Threading;

namespace SayHello.ShortLink;

public abstract class ShortLinkApplicationService : ApplicationService
{
    protected ICancellationTokenProvider CancellationTokenProvider =>
        LazyServiceProvider.LazyGetRequiredService<ICancellationTokenProvider>();

    protected ShortLinkApplicationService()
    {
        LocalizationResource = typeof(ShortLinkResource);
        ObjectMapperContext = typeof(ShortLinkCommonApplicationModule);
    }
}
