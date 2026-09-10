using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace SayHello.ShortLink.BlockedDomains;

public class BlockedDomainRepositoryTests : ShortLinkEntityFrameworkCoreTestBase
{
    private readonly IBlockedDomainRepository _repository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public BlockedDomainRepositoryTests()
    {
        _repository = GetRequiredService<IBlockedDomainRepository>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_Deleted_Items_And_Order_By_Domain()
    {
        var deleted = new BlockedDomain(
            _guidGenerator.Create(),
            null,
            "deleted.example");

        await WithUnitOfWorkAsync(async () =>
        {
            await _repository.InsertAsync(
                new BlockedDomain(_guidGenerator.Create(), null, "z.example"),
                autoSave: true);
            await _repository.InsertAsync(
                new BlockedDomain(_guidGenerator.Create(), null, "a.example"),
                autoSave: true);
            await _repository.InsertAsync(deleted, autoSave: true);
            await _repository.DeleteAsync(deleted, autoSave: true);
        });

        var domains = await WithUnitOfWorkAsync(() => _repository.GetListAsync(CancellationToken.None));

        domains.Select(x => x.Domain).ShouldBe(["a.example", "z.example"]);
    }

    [Fact]
    public async Task GetListAsync_Should_Use_ABP_Tenant_Filter()
    {
        var tenantId = Guid.NewGuid();
        var hostDomain = new BlockedDomain(_guidGenerator.Create(), null, "host.example");
        var tenantDomain = new BlockedDomain(_guidGenerator.Create(), tenantId, "tenant.example");

        await WithUnitOfWorkAsync(() => _repository.InsertAsync(hostDomain, autoSave: true));
        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(() => _repository.InsertAsync(tenantDomain, autoSave: true));
        }

        (await WithUnitOfWorkAsync(() => _repository.GetListAsync(CancellationToken.None)))
            .Single().Id.ShouldBe(hostDomain.Id);
        using (_currentTenant.Change(tenantId))
        {
            (await WithUnitOfWorkAsync(() => _repository.GetListAsync(CancellationToken.None)))
                .Single().Id.ShouldBe(tenantDomain.Id);
        }
    }
}
