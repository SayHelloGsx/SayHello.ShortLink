using SayHello.Subscription.Localization;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Threading;

namespace SayHello.Subscription;

public abstract class SubscriptionApplicationService : ApplicationService
{
    protected ICancellationTokenProvider CancellationTokenProvider =>
        LazyServiceProvider.LazyGetRequiredService<ICancellationTokenProvider>();

    protected SubscriptionApplicationService()
    {
        LocalizationResource = typeof(SubscriptionResource);
        ObjectMapperContext = typeof(SubscriptionCommonApplicationModule);
    }
}
