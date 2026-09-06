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
[Authorize(SubscriptionAdminPermissions.Products.Default)]
public class ProductAdminAppService : SubscriptionApplicationService, IProductAdminAppService
{
    private readonly ISubscriptionCatalogManager _manager;
    private readonly AdminCatalogReader _reader;
    public ProductAdminAppService(ISubscriptionCatalogManager manager, AdminCatalogReader reader)
    {
        _manager = manager; _reader = reader;
    }

    public virtual Task<PagedResultDto<AdminProductDto>> GetListAsync(AdminCatalogQueryDto input) => _reader.ProductsAsync(input);
    public virtual async Task<AdminProductDto> GetAsync(Guid id) => AdminDtoMapper.ToDto(await _reader.ProductAsync(id));
    public virtual Task<ListResultDto<RegisteredProductDto>> GetDefinitionsAsync() => Task.FromResult(_reader.Definitions());

    [Authorize(SubscriptionAdminPermissions.Products.Create)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<AdminProductDto> CreateAsync(CreateProductDto input) =>
        AdminDtoMapper.ToDto(await _manager.CreateProductAsync(CurrentTenant.Id, input.Code,
            new CatalogDetails(input.Name, input.Description, input.DisplayOrder), CancellationTokenProvider.Token));

    [Authorize(SubscriptionAdminPermissions.Products.Update)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<AdminProductDto> UpdateAsync(Guid id, UpdateProductDto input) =>
        AdminDtoMapper.ToDto(await _manager.UpdateProductAsync(CurrentTenant.Id, id, input.ConcurrencyStamp,
            new CatalogDetails(input.Name, input.Description, input.DisplayOrder), CancellationTokenProvider.Token));

    [Authorize(SubscriptionAdminPermissions.Products.Publish)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<AdminProductDto> SetStateAsync(Guid id, CatalogStateInputDto input) =>
        AdminDtoMapper.ToDto(await _manager.SetProductStateAsync(CurrentTenant.Id, id, input.ConcurrencyStamp,
            input.State, CancellationTokenProvider.Token));

    [Authorize(SubscriptionAdminPermissions.Products.Delete)]
    [UnitOfWork(isTransactional: true)]
    public virtual Task DeleteAsync(Guid id, VersionInputDto input) =>
        _manager.DeleteProductAsync(CurrentTenant.Id, id, input.ConcurrencyStamp, CancellationTokenProvider.Token);
}
