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
[Authorize(SubscriptionAdminPermissions.Bundles.Default)]
public class BundleAdminAppService : SubscriptionApplicationService, IBundleAdminAppService
{
    private readonly ISubscriptionCatalogManager _manager;
    private readonly AdminCatalogReader _reader;
    public BundleAdminAppService(ISubscriptionCatalogManager manager, AdminCatalogReader reader)
    {
        _manager = manager; _reader = reader;
    }

    public virtual Task<PagedResultDto<AdminBundleDto>> GetListAsync(AdminCatalogQueryDto input) => _reader.BundlesAsync(input);
    public virtual async Task<AdminBundleDto> GetAsync(Guid id) => await _reader.MapAsync(await _reader.BundleAsync(id));
    public virtual Task<PagedResultDto<AdminPlanDto>> GetPlansAsync(AdminCatalogQueryDto input) => _reader.PlansAsync(input);

    [Authorize(SubscriptionAdminPermissions.Bundles.Create)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<AdminBundleDto> CreateAsync(CreateBundleDto input) =>
        await _reader.MapAsync(await _manager.CreateBundleAsync(CurrentTenant.Id, input.Code,
            new CatalogDetails(input.Name, input.Description, input.DisplayOrder), input.PlanIds, CancellationTokenProvider.Token));

    [Authorize(SubscriptionAdminPermissions.Bundles.Update)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<AdminBundleDto> UpdateAsync(Guid id, UpdateBundleDto input) =>
        await _reader.MapAsync(await _manager.UpdateBundleAsync(CurrentTenant.Id, id, input.ConcurrencyStamp,
            new CatalogDetails(input.Name, input.Description, input.DisplayOrder), input.PlanIds, CancellationTokenProvider.Token));

    [Authorize(SubscriptionAdminPermissions.Bundles.Publish)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<AdminBundleDto> SetStateAsync(Guid id, CatalogStateInputDto input) =>
        await _reader.MapAsync(await _manager.SetBundleStateAsync(CurrentTenant.Id, id, input.ConcurrencyStamp,
            input.State, CancellationTokenProvider.Token));

    [Authorize(SubscriptionAdminPermissions.Bundles.Delete)]
    [UnitOfWork(isTransactional: true)]
    public virtual Task DeleteAsync(Guid id, VersionInputDto input) =>
        _manager.DeleteBundleAsync(CurrentTenant.Id, id, input.ConcurrencyStamp, CancellationTokenProvider.Token);
}
