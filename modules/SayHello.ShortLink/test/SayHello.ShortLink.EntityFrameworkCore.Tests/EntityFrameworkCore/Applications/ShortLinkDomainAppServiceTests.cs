using System;
using System.Linq;
using System.Threading.Tasks;
using SayHello.ShortLink.Admin.ShortLinkDomains;
using SayHello.ShortLink.ShortLinkDomains;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
using Xunit;

namespace SayHello.ShortLink.Admin.Applications;

public class ShortLinkDomainAppServiceTests :
    ShortLinkTestBase<ShortLinkDomainAdminTestModule>
{
    private readonly IShortLinkDomainAppService _appService;
    private readonly ICurrentTenant _currentTenant;

    public ShortLinkDomainAppServiceTests()
    {
        _appService = GetRequiredService<IShortLinkDomainAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Catalog_Should_Always_Have_One_Enabled_Default()
    {
        var tenantId = Guid.NewGuid();
        using (_currentTenant.Change(tenantId))
        {
            var first = await _appService.CreateAsync(
                new CreateShortLinkDomainDto
                {
                    Origin = "HTTPS://One.Example.Test:443/"
                });
            var second = await _appService.CreateAsync(
                new CreateShortLinkDomainDto
                {
                    Origin = "https://two.example.test"
                });

            first.Origin.ShouldBe("https://one.example.test");
            first.IsDefault.ShouldBeTrue();
            first.IsEnabled.ShouldBeTrue();
            second.IsDefault.ShouldBeFalse();
            second.IsEnabled.ShouldBeTrue();

            var cannotDisable = await Should.ThrowAsync<BusinessException>(() =>
                _appService.SetEnabledAsync(
                    first.Id,
                    new SetShortLinkDomainEnabledDto
                    {
                        IsEnabled = false,
                        ConcurrencyStamp = first.ConcurrencyStamp
                    }));
            cannotDisable.Code.ShouldBe(
                ShortLinkErrorCodes.DefaultDomainCannotBeDisabled);

            var disabledCandidate = await _appService.SetEnabledAsync(
                second.Id,
                new SetShortLinkDomainEnabledDto
                {
                    IsEnabled = false,
                    ConcurrencyStamp = second.ConcurrencyStamp
                });
            var newDefault = await _appService.SetDefaultAsync(
                disabledCandidate.Id,
                new ShortLinkDomainVersionDto
                {
                    ConcurrencyStamp = disabledCandidate.ConcurrencyStamp
                });
            newDefault.IsDefault.ShouldBeTrue();
            newDefault.IsEnabled.ShouldBeTrue();

            var list = (await _appService.GetListAsync()).Items;
            list.Count(x => x.IsDefault && x.IsEnabled).ShouldBe(1);
            var previousDefault = list.Single(x => x.Id == first.Id);
            previousDefault.IsDefault.ShouldBeFalse();

            var disabled = await _appService.SetEnabledAsync(
                previousDefault.Id,
                new SetShortLinkDomainEnabledDto
                {
                    IsEnabled = false,
                    ConcurrencyStamp = previousDefault.ConcurrencyStamp
                });
            disabled.IsEnabled.ShouldBeFalse();

            await _appService.DeleteAsync(
                disabled.Id,
                new DeleteShortLinkDomainDto
                {
                    ConcurrencyStamp = disabled.ConcurrencyStamp
                });
            (await _appService.GetListAsync()).Items.Single().Id
                .ShouldBe(second.Id);

            var cannotDelete = await Should.ThrowAsync<BusinessException>(() =>
                _appService.DeleteAsync(
                    newDefault.Id,
                    new DeleteShortLinkDomainDto
                    {
                        ConcurrencyStamp = newDefault.ConcurrencyStamp
                    }));
            cannotDelete.Code.ShouldBe(
                ShortLinkErrorCodes.DefaultDomainCannotBeDeleted);
        }
    }

    [Fact]
    public async Task Concurrent_Creates_Should_Be_Serialized_With_One_Default()
    {
        var tenantId = Guid.NewGuid();
        using (_currentTenant.Change(tenantId))
        {
            var created = await Task.WhenAll(
                _appService.CreateAsync(
                    new CreateShortLinkDomainDto
                    {
                        Origin = "https://one.concurrent.example.test"
                    }),
                _appService.CreateAsync(
                    new CreateShortLinkDomainDto
                    {
                        Origin = "https://two.concurrent.example.test"
                    }));

            created.Length.ShouldBe(2);
            var list = (await _appService.GetListAsync()).Items;
            list.Count.ShouldBe(2);
            list.Count(x => x.IsDefault && x.IsEnabled).ShouldBe(1);
        }
    }

    [Fact]
    public async Task Duplicate_Normalized_Origin_Should_Return_Business_Error()
    {
        var tenantId = Guid.NewGuid();
        using (_currentTenant.Change(tenantId))
        {
            await _appService.CreateAsync(
                new CreateShortLinkDomainDto
                {
                    Origin = "https://BÜCHER.example"
                });

            var exception = await Should.ThrowAsync<BusinessException>(() =>
                _appService.CreateAsync(
                    new CreateShortLinkDomainDto
                    {
                        Origin = "https://xn--bcher-kva.example/"
                    }));

            exception.Code.ShouldBe(ShortLinkErrorCodes.DomainAlreadyExists);
        }
    }

    [Fact]
    public async Task Domain_Updates_Should_Reject_A_Stale_Concurrency_Stamp()
    {
        var tenantId = Guid.NewGuid();
        using (_currentTenant.Change(tenantId))
        {
            await _appService.CreateAsync(
                new CreateShortLinkDomainDto
                {
                    Origin = "https://default.concurrency.example.test"
                });
            var candidate = await _appService.CreateAsync(
                new CreateShortLinkDomainDto
                {
                    Origin = "https://candidate.concurrency.example.test"
                });

            var updated = await _appService.SetEnabledAsync(
                candidate.Id,
                new SetShortLinkDomainEnabledDto
                {
                    IsEnabled = false,
                    ConcurrencyStamp = candidate.ConcurrencyStamp
                });
            updated.ConcurrencyStamp.ShouldNotBe(candidate.ConcurrencyStamp);

            await Should.ThrowAsync<AbpDbConcurrencyException>(() =>
                _appService.SetEnabledAsync(
                    candidate.Id,
                    new SetShortLinkDomainEnabledDto
                    {
                        IsEnabled = true,
                        ConcurrencyStamp = candidate.ConcurrencyStamp
                    }));
        }
    }

    [Fact]
    public async Task Domain_Delete_Should_Require_A_Current_Concurrency_Stamp()
    {
        var tenantId = Guid.NewGuid();
        using (_currentTenant.Change(tenantId))
        {
            await _appService.CreateAsync(
                new CreateShortLinkDomainDto
                {
                    Origin = "https://default.delete.example.test"
                });
            var candidate = await _appService.CreateAsync(
                new CreateShortLinkDomainDto
                {
                    Origin = "https://candidate.delete.example.test"
                });
            var updated = await _appService.SetEnabledAsync(
                candidate.Id,
                new SetShortLinkDomainEnabledDto
                {
                    IsEnabled = false,
                    ConcurrencyStamp = candidate.ConcurrencyStamp
                });

            await Should.ThrowAsync<AbpValidationException>(() =>
                _appService.DeleteAsync(
                    candidate.Id,
                    new DeleteShortLinkDomainDto()));
            await Should.ThrowAsync<AbpDbConcurrencyException>(() =>
                _appService.DeleteAsync(
                    candidate.Id,
                    new DeleteShortLinkDomainDto
                    {
                        ConcurrencyStamp = candidate.ConcurrencyStamp
                    }));
            (await _appService.GetListAsync()).Items
                .Single(x => x.Id == candidate.Id)
                .ConcurrencyStamp.ShouldBe(updated.ConcurrencyStamp);

            await _appService.DeleteAsync(
                candidate.Id,
                new DeleteShortLinkDomainDto
                {
                    ConcurrencyStamp = updated.ConcurrencyStamp
                });
            (await _appService.GetListAsync()).Items
                .ShouldNotContain(x => x.Id == candidate.Id);
        }
    }
}
