using System;
using System.Threading.Tasks;
using SayHello.Subscription.Catalog;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace SayHello.Subscription.Public.Catalog;

public interface ISubscriptionCatalogAppService : IApplicationService
{
    Task<PagedResultDto<SubscriptionProductDto>> GetProductsAsync(GetPublicCatalogInput input);
    Task<SubscriptionProductDto> GetProductAsync(Guid id);
    Task<PagedResultDto<PublicSubscriptionPlanDto>> GetPlansAsync(GetPublicCatalogInput input);
    Task<PublicSubscriptionPlanDto> GetPlanAsync(Guid id);
    Task<PagedResultDto<PublicSubscriptionBundleDto>> GetBundlesAsync(GetPublicCatalogInput input);
    Task<PublicSubscriptionBundleDto> GetBundleAsync(Guid id);
}
