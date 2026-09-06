using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SayHello.Subscription.Admin.Permissions;
using SayHello.Subscription.Catalog;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Uow;

namespace SayHello.Subscription.Admin.Catalog;

[RemoteService(false)]
[Authorize(SubscriptionAdminPermissions.Plans.Default)]
public class PlanAdminAppService : SubscriptionApplicationService, IPlanAdminAppService
{
    private readonly ISubscriptionCatalogManager _manager;
    private readonly AdminCatalogReader _reader;
    public PlanAdminAppService(ISubscriptionCatalogManager manager, AdminCatalogReader reader)
    {
        _manager = manager; _reader = reader;
    }

    public virtual Task<PagedResultDto<AdminPlanDto>> GetListAsync(AdminCatalogQueryDto input) => _reader.PlansAsync(input);
    public virtual async Task<AdminPlanDto> GetAsync(Guid id) => await _reader.MapAsync(await _reader.PlanAsync(id));
    public virtual Task<PagedResultDto<AdminProductDto>> GetProductsAsync(AdminCatalogQueryDto input) => _reader.ProductsAsync(input);
    public virtual async Task<RegisteredProductDto> GetDefinitionAsync(Guid productId) =>
        _reader.Definition((await _reader.ProductAsync(productId)).Code);

    [Authorize(SubscriptionAdminPermissions.Plans.Create)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<AdminPlanDto> CreateAsync(CreatePlanDto input) =>
        await _reader.MapAsync(await _manager.CreatePlanAsync(CurrentTenant.Id, input.ProductId, input.Code,
            new CatalogDetails(input.Name, input.Description, input.DisplayOrder),
            SubscriptionDtoMapper.ToValues(input.Entitlements), CancellationTokenProvider.Token));

    [Authorize(SubscriptionAdminPermissions.Plans.Update)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<AdminPlanDto> UpdateAsync(Guid id, UpdatePlanDto input) =>
        await _reader.MapAsync(await _manager.UpdatePlanAsync(CurrentTenant.Id, id, input.ConcurrencyStamp,
            new CatalogDetails(input.Name, input.Description, input.DisplayOrder),
            SubscriptionDtoMapper.ToValues(input.Entitlements), CancellationTokenProvider.Token));

    [Authorize(SubscriptionAdminPermissions.Plans.Publish)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<AdminPlanDto> SetStateAsync(Guid id, CatalogStateInputDto input) =>
        await _reader.MapAsync(await _manager.SetPlanStateAsync(CurrentTenant.Id, id, input.ConcurrencyStamp,
            input.State, CancellationTokenProvider.Token));

    [Authorize(SubscriptionAdminPermissions.Plans.Delete)]
    [UnitOfWork(isTransactional: true)]
    public virtual Task DeleteAsync(Guid id, VersionInputDto input) =>
        _manager.DeletePlanAsync(CurrentTenant.Id, id, input.ConcurrencyStamp, CancellationTokenProvider.Token);
}
