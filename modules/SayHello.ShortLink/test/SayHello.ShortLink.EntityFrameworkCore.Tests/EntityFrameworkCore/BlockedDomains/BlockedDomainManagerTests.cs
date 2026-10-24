using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace SayHello.ShortLink.BlockedDomains;

public class BlockedDomainManagerTests : ShortLinkEntityFrameworkCoreTestBase
{
    private readonly BlockedDomainManager _manager;
    private readonly IBlockedDomainRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public BlockedDomainManagerTests()
    {
        _manager = GetRequiredService<BlockedDomainManager>();
        _repository = GetRequiredService<IBlockedDomainRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task CreateAsync_Should_Normalize_Domain_For_Current_Tenant()
    {
        var tenantId = Guid.NewGuid();
        BlockedDomain entity;
        using (_currentTenant.Change(tenantId))
        {
            entity = await WithUnitOfWorkAsync(() =>
                _manager.CreateAsync(" Bücher.Example. ", "Spam"));
        }

        entity.Id.ShouldNotBe(Guid.Empty);
        entity.TenantId.ShouldBe(tenantId);
        entity.Domain.ShouldBe("xn--bcher-kva.example");
        entity.Reason.ShouldBe("Spam");
    }

    [Fact]
    public async Task CreateAsync_Should_Reject_An_Existing_Domain()
    {
        await InsertAsync("existing.example");

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            WithUnitOfWorkAsync(() => _manager.CreateAsync("EXISTING.example.")));

        exception.Code.ShouldBe(ShortLinkErrorCodes.BlockedDomainAlreadyExists);
        exception.Data["Host"].ShouldBe("existing.example");
    }

    [Fact]
    public async Task CreateManyAsync_Should_Skip_Existing_And_Repeated_Domains()
    {
        await InsertAsync("existing.example");

        var result = await WithUnitOfWorkAsync(() =>
            _manager.CreateManyAsync(
            [
                ("New.Example", "First"),
                ("new.example.", "Second"),
                ("Existing.Example", null),
                ("bücher.example", null)
            ]));

        result.ExistingDomains.ShouldBe(["existing.example"]);
        result.Created.Select(x => x.Domain).ShouldBe(["new.example", "xn--bcher-kva.example"]);
        result.Created[0].Reason.ShouldBe("First");
        result.Created.Select(x => x.Id).Distinct().Count().ShouldBe(2);

        var stored = await WithUnitOfWorkAsync(() =>
            _repository.GetListAsync(CancellationToken.None));
        stored.Select(x => x.Domain).ShouldBe(["existing.example"]);
    }

    public static TheoryData<string> InvalidDomains => new()
    {
        "a..b",
        "exa mple.com",
        "*.example.com",
        "example.com/path",
        "http://example.com",
        "[::1]:80",
        "fe80::1%3",
        string.Join('.', Enumerable.Repeat(new string('a', 63), 4))
    };

    [Theory]
    [MemberData(nameof(InvalidDomains))]
    public async Task CreateAsync_And_CreateManyAsync_Should_Reject_An_Invalid_Domain(string domain)
    {
        var singleException = await Should.ThrowAsync<BusinessException>(() =>
            WithUnitOfWorkAsync(() => _manager.CreateAsync(domain)));
        var batchException = await Should.ThrowAsync<BusinessException>(() =>
            WithUnitOfWorkAsync(() => _manager.CreateManyAsync([(domain, null)])));

        singleException.Code.ShouldBe(ShortLinkErrorCodes.InvalidBlockedDomain);
        singleException.Data["Domain"].ShouldBe(domain);
        batchException.Code.ShouldBe(ShortLinkErrorCodes.InvalidBlockedDomain);
    }

    [Theory]
    [InlineData(" 203.0.113.5. ", "203.0.113.5")]
    [InlineData("0xCB.0.113.5", "203.0.113.5")]
    [InlineData("1.2.3", "1.2.0.3")]
    [InlineData("[2001:DB8:0:0::1]", "2001:db8::1")]
    public async Task CreateAsync_And_CreateManyAsync_Should_Store_An_Ip_Address_In_Canonical_Form(
        string domain,
        string expected)
    {
        var entity = await WithUnitOfWorkAsync(() => _manager.CreateAsync(domain));
        var result = await WithUnitOfWorkAsync(() =>
            _manager.CreateManyAsync([(domain, null), (expected, null)]));

        entity.Domain.ShouldBe(expected);
        result.Created.Select(x => x.Domain).ShouldBe([expected]);
    }

    private Task InsertAsync(string domain)
    {
        return WithUnitOfWorkAsync(() =>
            _repository.InsertAsync(
                new BlockedDomain(Guid.NewGuid(), null, domain),
                autoSave: true));
    }
}
