using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Admin.Permissions;
using SayHello.Subscription.Admin.Web.Pages.Admin.Subscriptions;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace SayHello.Subscription.AdminTests;

public class AdminDefaultPlanMetadataTests
{
    [Fact]
    public void Default_mutation_is_a_dedicated_versioned_contract_not_inherited_by_plans_or_bundles()
    {
        typeof(SetDefaultPlanInputDto).BaseType.ShouldBe(typeof(VersionInputDto));
        typeof(SetDefaultPlanInputDto).GetProperty(nameof(SetDefaultPlanInputDto.PlanId))!.PropertyType.ShouldBe(typeof(Guid?));
        foreach (var type in new[] { typeof(CreateProductDto), typeof(UpdateProductDto), typeof(CreatePlanDto),
                     typeof(UpdatePlanDto), typeof(CreateBundleDto), typeof(UpdateBundleDto) })
            type.GetProperty(nameof(AdminProductDto.DefaultPlanId)).ShouldBeNull();

        var clear = JsonSerializer.Deserialize<SetDefaultPlanInputDto>("""{"ConcurrencyStamp":"stamp","PlanId":null}""")!;
        clear.PlanId.ShouldBeNull();
        clear.ConcurrencyStamp.ShouldBe("stamp");
        var product = new AdminProductDto();
        product.HasDefaultPlan.ShouldBeFalse();
        product.DefaultPlanId = Guid.NewGuid();
        product.DefaultPlanName = "Free";
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(product));
        json.RootElement.GetProperty(nameof(AdminProductDto.DefaultPlanId)).GetGuid().ShouldBe(product.DefaultPlanId.Value);
        json.RootElement.GetProperty(nameof(AdminProductDto.DefaultPlanName)).GetString().ShouldBe("Free");
        json.RootElement.GetProperty(nameof(AdminProductDto.HasDefaultPlan)).GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Explicit_default_endpoints_and_Razor_handlers_share_product_permissions_and_contracts()
    {
        var mutation = typeof(ProductAdminController).GetMethod(nameof(IProductAdminAppService.SetDefaultPlanAsync))!;
        mutation.GetCustomAttribute<HttpPutAttribute>()!.Template.ShouldBe("{id:guid}/default-plan");
        mutation.GetCustomAttributes<AuthorizeAttribute>().Single().Policy.ShouldBe(SubscriptionAdminPermissions.Products.Update);
        mutation.GetParameters()[1].ParameterType.ShouldBe(typeof(SetDefaultPlanInputDto));
        mutation.GetParameters()[1].GetCustomAttribute<FromBodyAttribute>().ShouldNotBeNull();

        var options = typeof(ProductAdminController).GetMethod(nameof(IProductAdminAppService.GetDefaultPlanOptionsAsync))!;
        options.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("{id:guid}/default-plan-options");
        options.GetParameters()[1].ParameterType.ShouldBe(typeof(AdminCatalogQueryDto));
        options.GetParameters()[1].GetCustomAttribute<FromQueryAttribute>().ShouldNotBeNull();

        foreach (var type in new[] { typeof(ProductAdminController), typeof(ProductAdminAppService), typeof(ProductsModel) })
            type.GetCustomAttributes<AuthorizeAttribute>().Single().Policy.ShouldBe(SubscriptionAdminPermissions.Products.Default);

        var handler = typeof(ProductsModel).GetMethod(nameof(ProductsModel.OnPostDefaultPlanAsync))!;
        handler.GetParameters()[1].ParameterType.ShouldBe(typeof(SetDefaultPlanInputDto));
        handler.GetParameters()[1].GetCustomAttribute<FromBodyAttribute>().ShouldNotBeNull();
        typeof(ProductsModel).GetMethod(nameof(ProductsModel.OnGetDefaultPlanOptionsAsync))!.GetParameters()[1]
            .GetCustomAttribute<FromQueryAttribute>().ShouldNotBeNull();
        typeof(IProductAdminAppService).IsAssignableFrom(typeof(ProductAdminController)).ShouldBeTrue();
    }

    [Fact]
    public async Task Explicit_controller_and_picker_handler_forward_route_identity_and_inputs_unchanged()
    {
        var service = Substitute.For<IProductAdminAppService>();
        var controller = new ProductAdminController(service);
        var page = new ProductsModel(service);
        var productId = Guid.NewGuid();
        var input = new SetDefaultPlanInputDto { ConcurrencyStamp = "original", PlanId = Guid.NewGuid() };
        var result = new AdminProductDto { Id = productId, DefaultPlanId = input.PlanId, DefaultPlanName = "Free" };
        service.SetDefaultPlanAsync(productId, input).Returns(result);
        (await controller.SetDefaultPlanAsync(productId, input)).ShouldBeSameAs(result);
        input = new SetDefaultPlanInputDto { ConcurrencyStamp = "next", PlanId = null };
        await controller.SetDefaultPlanAsync(productId, input);
        await service.Received(1).SetDefaultPlanAsync(productId, input);

        var query = new AdminCatalogQueryDto { Filter = "free", SkipCount = 10, MaxResultCount = 10 };
        var options = new PagedResultDto<AdminPlanDto>(12, new[] { new AdminPlanDto { ProductId = productId } });
        service.GetDefaultPlanOptionsAsync(productId, query).Returns(options);
        (await controller.GetDefaultPlanOptionsAsync(productId, query)).ShouldBeSameAs(options);
        (await page.OnGetDefaultPlanOptionsAsync(productId, query)).Value.ShouldBeSameAs(options);
        await service.Received(2).GetDefaultPlanOptionsAsync(productId, query);
    }
}
