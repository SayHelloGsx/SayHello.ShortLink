using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace SayHello.Subscription.Admin.Catalog;

public interface IProductAdminAppService : IApplicationService
{
    Task<PagedResultDto<AdminProductDto>> GetListAsync(AdminCatalogQueryDto input);
    Task<AdminProductDto> GetAsync(Guid id);
    Task<ListResultDto<RegisteredProductDto>> GetDefinitionsAsync();
    Task<AdminProductDto> CreateAsync(CreateProductDto input);
    Task<AdminProductDto> UpdateAsync(Guid id, UpdateProductDto input);
    Task<AdminProductDto> SetStateAsync(Guid id, CatalogStateInputDto input);
    Task DeleteAsync(Guid id, VersionInputDto input);
}

public interface IPlanAdminAppService : IApplicationService
{
    Task<PagedResultDto<AdminPlanDto>> GetListAsync(AdminCatalogQueryDto input);
    Task<AdminPlanDto> GetAsync(Guid id);
    Task<PagedResultDto<AdminProductDto>> GetProductsAsync(AdminCatalogQueryDto input);
    Task<RegisteredProductDto> GetDefinitionAsync(Guid productId);
    Task<AdminPlanDto> CreateAsync(CreatePlanDto input);
    Task<AdminPlanDto> UpdateAsync(Guid id, UpdatePlanDto input);
    Task<AdminPlanDto> SetStateAsync(Guid id, CatalogStateInputDto input);
    Task DeleteAsync(Guid id, VersionInputDto input);
}

public interface IBundleAdminAppService : IApplicationService
{
    Task<PagedResultDto<AdminBundleDto>> GetListAsync(AdminCatalogQueryDto input);
    Task<AdminBundleDto> GetAsync(Guid id);
    Task<PagedResultDto<AdminPlanDto>> GetPlansAsync(AdminCatalogQueryDto input);
    Task<AdminBundleDto> CreateAsync(CreateBundleDto input);
    Task<AdminBundleDto> UpdateAsync(Guid id, UpdateBundleDto input);
    Task<AdminBundleDto> SetStateAsync(Guid id, CatalogStateInputDto input);
    Task DeleteAsync(Guid id, VersionInputDto input);
}
