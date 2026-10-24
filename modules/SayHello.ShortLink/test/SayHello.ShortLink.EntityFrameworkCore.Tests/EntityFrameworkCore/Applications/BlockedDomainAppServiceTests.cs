using System;
using System.Linq;
using System.Threading.Tasks;
using SayHello.ShortLink.Admin.BlockedDomains;
using SayHello.ShortLink.Common.BlockedDomains;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
using Xunit;

namespace SayHello.ShortLink.Admin.Applications;

public class BlockedDomainAppServiceTests :
    ShortLinkTestBase<ShortLinkDomainAdminTestModule>
{
    private readonly IBlockedDomainAppService _appService;
    private readonly IBlockedDomainCache _cache;
    private readonly ICurrentTenant _currentTenant;

    public BlockedDomainAppServiceTests()
    {
        _appService = GetRequiredService<IBlockedDomainAppService>();
        _cache = GetRequiredService<IBlockedDomainCache>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Delete_Should_Require_A_Current_Concurrency_Stamp()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var domain = $"{Guid.NewGuid():N}.example";
            var created = await _appService.CreateAsync(
                new CreateBlockedDomainDto
                {
                    Domain = domain,
                    Reason = "First writer"
                });
            var updated = await _appService.UpdateAsync(
                created.Id,
                new UpdateBlockedDomainDto
                {
                    Reason = "Second writer",
                    IsActive = true,
                    ConcurrencyStamp = created.ConcurrencyStamp
                });
            updated.ConcurrencyStamp.ShouldNotBe(created.ConcurrencyStamp);

            foreach (var missing in new[] { null, string.Empty, " " })
            {
                await Should.ThrowAsync<AbpValidationException>(() =>
                    _appService.DeleteAsync(
                        created.Id,
                        new DeleteBlockedDomainDto
                        {
                            ConcurrencyStamp = missing!
                        }));
            }

            await Should.ThrowAsync<AbpDbConcurrencyException>(() =>
                _appService.DeleteAsync(
                    created.Id,
                    new DeleteBlockedDomainDto
                    {
                        ConcurrencyStamp = created.ConcurrencyStamp
                    }));
            (await _appService.GetListAsync()).Items
                .Single(x => x.Id == created.Id)
                .ConcurrencyStamp.ShouldBe(updated.ConcurrencyStamp);
            (await _cache.GetAsync($"deep.{domain}")).IsBlocked.ShouldBeTrue();

            await _appService.DeleteAsync(
                created.Id,
                new DeleteBlockedDomainDto
                {
                    ConcurrencyStamp = updated.ConcurrencyStamp
                });
            (await _appService.GetListAsync()).Items
                .ShouldNotContain(x => x.Id == created.Id);
            (await _cache.GetAsync($"deep.{domain}")).IsBlocked.ShouldBeFalse();
        }
    }
}
