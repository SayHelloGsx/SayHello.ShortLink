using System;
using System.Threading.Tasks;
using SayHello.ShortLink.Common.BlockedDomains;
using SayHello.ShortLink.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Guids;
using Xunit;

namespace SayHello.ShortLink.BlockedDomains;

public class BlockedDomainCacheTests : ShortLinkEntityFrameworkCoreTestBase
{
    private readonly IBlockedDomainCache _cache;
    private readonly IBlockedDomainRepository _repository;
    private readonly IGuidGenerator _guidGenerator;

    public BlockedDomainCacheTests()
    {
        _cache = GetRequiredService<IBlockedDomainCache>();
        _repository = GetRequiredService<IBlockedDomainRepository>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task InvalidateAsync_Should_Refresh_Known_Subdomain_Cache()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var domain = $"{suffix}.example";
        var subdomain = $"deep.{domain}";

        (await _cache.GetAsync(subdomain, null)).IsBlocked.ShouldBeFalse();

        var blockedDomain = new BlockedDomain(
            _guidGenerator.Create(),
            null,
            domain,
            "Blocked for testing");
        await WithUnitOfWorkAsync(() =>
            _repository.InsertAsync(blockedDomain, autoSave: true));

        (await _cache.GetAsync(subdomain, null)).IsBlocked.ShouldBeFalse();

        await _cache.InvalidateAsync(domain, null);
        var blocked = await _cache.GetAsync(subdomain, null);
        blocked.IsBlocked.ShouldBeTrue();
        blocked.MatchedDomain.ShouldBe(domain);
        blocked.Reason.ShouldBe("Blocked for testing");

        await WithUnitOfWorkAsync(() =>
            _repository.DeleteAsync(blockedDomain, autoSave: true));
        await _cache.InvalidateAsync(domain, null);
        (await _cache.GetAsync(subdomain, null)).IsBlocked.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAsync_Should_Return_The_Most_Specific_Active_Parent()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var parent = $"{suffix}.example";
        var child = $"child.{parent}";
        var disabled = $"disabled.{parent}";

        await WithUnitOfWorkAsync(async () =>
        {
            await _repository.InsertAsync(
                new BlockedDomain(_guidGenerator.Create(), null, parent, "Parent"),
                autoSave: true);
            await _repository.InsertAsync(
                new BlockedDomain(_guidGenerator.Create(), null, child, "Child"),
                autoSave: true);
            var disabledEntity = new BlockedDomain(
                _guidGenerator.Create(),
                null,
                disabled,
                "Disabled");
            disabledEntity.Update("Disabled", isActive: false);
            await _repository.InsertAsync(disabledEntity, autoSave: true);
        });

        var childMatch = await _cache.GetAsync($"deep.{child}", null);
        childMatch.MatchedDomain.ShouldBe(child);
        childMatch.Reason.ShouldBe("Child");

        var disabledMatch = await _cache.GetAsync(disabled, null);
        disabledMatch.MatchedDomain.ShouldBe(parent);
        disabledMatch.Reason.ShouldBe("Parent");
    }
}
