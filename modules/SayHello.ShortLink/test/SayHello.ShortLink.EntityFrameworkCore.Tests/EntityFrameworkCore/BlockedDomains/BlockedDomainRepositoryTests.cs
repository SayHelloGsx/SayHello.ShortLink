using System;
using System.Linq;
using System.Threading.Tasks;
using SayHello.ShortLink.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Guids;
using Xunit;

namespace SayHello.ShortLink.BlockedDomains;

public class BlockedDomainRepositoryTests : ShortLinkEntityFrameworkCoreTestBase
{
    private readonly IBlockedDomainRepository _repository;
    private readonly IGuidGenerator _guidGenerator;

    public BlockedDomainRepositoryTests()
    {
        _repository = GetRequiredService<IBlockedDomainRepository>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
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

        var domains = await WithUnitOfWorkAsync(() => _repository.GetListAsync(null));

        domains.Select(x => x.Domain).ShouldBe(["a.example", "z.example"]);
    }
}
