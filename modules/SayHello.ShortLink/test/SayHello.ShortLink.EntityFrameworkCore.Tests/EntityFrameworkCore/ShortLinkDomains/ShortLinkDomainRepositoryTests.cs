using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SayHello.ShortLink.EntityFrameworkCore;
using SayHello.ShortLink.ShortLinks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace SayHello.ShortLink.ShortLinkDomains;

public class ShortLinkDomainRepositoryTests : ShortLinkEntityFrameworkCoreTestBase
{
    private readonly IShortLinkDomainRepository _repository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public ShortLinkDomainRepositoryTests()
    {
        _repository = GetRequiredService<IShortLinkDomainRepository>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Repository_Should_Filter_By_Tenant_And_Order_Default_First()
    {
        var tenantId = Guid.NewGuid();
        var defaultDomain = new ShortLinkDomain(
            _guidGenerator.Create(),
            tenantId,
            "https://z.example.test",
            isDefault: true);
        var ordinaryDomain = new ShortLinkDomain(
            _guidGenerator.Create(),
            tenantId,
            "https://a.example.test");

        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _repository.InsertAsync(defaultDomain, autoSave: true);
                await _repository.InsertAsync(ordinaryDomain, autoSave: true);
            });

            var domains = await WithUnitOfWorkAsync(
                () => _repository.GetListAsync(CancellationToken.None));
            domains.Select(x => x.Id).ShouldBe(
                [defaultDomain.Id, ordinaryDomain.Id]);
            (await WithUnitOfWorkAsync(() =>
                _repository.FindDefaultAsync()))
                .ShouldNotBeNull()
                .Id.ShouldBe(defaultDomain.Id);
            (await WithUnitOfWorkAsync(() =>
                _repository.OriginExistsAsync(
                    ordinaryDomain.Origin,
                    enabledOnly: true))).ShouldBeTrue();
        }

        (await WithUnitOfWorkAsync(
            () => _repository.GetListAsync(CancellationToken.None))).ShouldBeEmpty();
        (await WithUnitOfWorkAsync(() =>
            _repository.IsHostConfiguredAsync("a.example.test"))).ShouldBeTrue();
    }

    [Fact]
    public async Task Unique_Indexes_Should_Protect_Origin_And_Default_Invariants()
    {
        var tenantId = Guid.NewGuid();
        using (_currentTenant.Change(tenantId))
        {
            await Should.ThrowAsync<DbUpdateException>(() =>
                WithUnitOfWorkAsync(async () =>
                {
                    await _repository.InsertAsync(
                        new ShortLinkDomain(
                            _guidGenerator.Create(),
                            tenantId,
                            "https://one.example.test",
                            isDefault: true),
                        autoSave: true);
                    await _repository.InsertAsync(
                        new ShortLinkDomain(
                            _guidGenerator.Create(),
                            tenantId,
                            "https://two.example.test",
                            isDefault: true),
                        autoSave: true);
                }));
        }

        var secondTenantId = Guid.NewGuid();
        using (_currentTenant.Change(secondTenantId))
        {
            await Should.ThrowAsync<DbUpdateException>(() =>
                WithUnitOfWorkAsync(async () =>
                {
                    await _repository.InsertAsync(
                        new ShortLinkDomain(
                            _guidGenerator.Create(),
                            secondTenantId,
                            "https://same.example.test"),
                        autoSave: true);
                    await _repository.InsertAsync(
                        new ShortLinkDomain(
                            _guidGenerator.Create(),
                            secondTenantId,
                            "HTTPS://SAME.example.test/"),
                        autoSave: true);
                }));
        }
    }

    [Fact]
    public async Task Repository_Should_Reject_A_Stale_Entity_Update()
    {
        var tenantId = Guid.NewGuid();
        using (_currentTenant.Change(tenantId))
        {
            var domain = new ShortLinkDomain(
                _guidGenerator.Create(),
                tenantId,
                "https://concurrency.example.test");
            await WithUnitOfWorkAsync(() =>
                _repository.InsertAsync(domain, autoSave: true));

            var winner = await WithUnitOfWorkAsync(() =>
                _repository.GetAsync(domain.Id));
            var stale = await WithUnitOfWorkAsync(() =>
                _repository.GetAsync(domain.Id));

            winner.Disable();
            await WithUnitOfWorkAsync(() =>
                _repository.UpdateAsync(winner, autoSave: true));

            stale.Disable();
            await Should.ThrowAsync<AbpDbConcurrencyException>(() =>
                WithUnitOfWorkAsync(() =>
                    _repository.UpdateAsync(stale, autoSave: true)));

            (await WithUnitOfWorkAsync(() => _repository.GetAsync(domain.Id)))
                .IsEnabled.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Reference_Count_Should_Be_Zero_When_Unreferenced()
    {
        var domain = new ShortLinkDomain(
            _guidGenerator.Create(),
            null,
            "https://unreferenced.example.test");

        (await WithUnitOfWorkAsync(() =>
            _repository.GetShortLinkReferenceCountAsync(domain.Id))).ShouldBe(0);
    }

    [Fact]
    public async Task Data_Seed_Should_Isolate_Host_And_Tenant_And_Idempotently_Backfill_Legacy_Links()
    {
        var tenantId = Guid.NewGuid();
        var shortLinks = GetRequiredService<IShortLinkRepository>();
        var softDeleteFilter = GetRequiredService<IDataFilter<ISoftDelete>>();
        var hostLegacy = new global::SayHello.ShortLink.ShortLinks.ShortLink(
            _guidGenerator.Create(),
            null,
            Guid.NewGuid(),
            "HostLegacy",
            "https://example.com/host-legacy",
            null,
            null);
        var deletedHostLegacy = new global::SayHello.ShortLink.ShortLinks.ShortLink(
            _guidGenerator.Create(),
            null,
            Guid.NewGuid(),
            "DeletedHostLegacy",
            "https://example.com/deleted-host-legacy",
            null,
            null);
        await WithUnitOfWorkAsync(async () =>
        {
            await shortLinks.InsertAsync(hostLegacy, autoSave: true);
            await shortLinks.InsertAsync(deletedHostLegacy, autoSave: true);
            await shortLinks.DeleteAsync(deletedHostLegacy, autoSave: true);
        });
        var tenantLegacy = new global::SayHello.ShortLink.ShortLinks.ShortLink(
            _guidGenerator.Create(),
            tenantId,
            Guid.NewGuid(),
            "TenantLegacy",
            "https://example.com/tenant-legacy",
            null,
            null);
        var deletedTenantLegacy = new global::SayHello.ShortLink.ShortLinks.ShortLink(
            _guidGenerator.Create(),
            tenantId,
            Guid.NewGuid(),
            "DeletedTenantLegacy",
            "https://example.com/deleted-tenant-legacy",
            null,
            null);
        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await shortLinks.InsertAsync(tenantLegacy, autoSave: true);
                await shortLinks.InsertAsync(deletedTenantLegacy, autoSave: true);
                await shortLinks.DeleteAsync(deletedTenantLegacy, autoSave: true);
            });
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ShortLink:Urls:BaseUrl"] = "HTTPS://GO.EXAMPLE.TEST:443/"
            })
            .Build();
        var contributor = new ShortLinkDomainDataSeedContributor(
            GetRequiredService<ShortLinkDomainManager>(),
            GetRequiredService<ShortLinkDomainConfigurationLock>(),
            _repository,
            shortLinks,
            _currentTenant,
            GetRequiredService<IUnitOfWorkManager>(),
            configuration);

        await contributor.SeedAsync(new DataSeedContext());
        await contributor.SeedAsync(new DataSeedContext());

        var hostDomain = (await WithUnitOfWorkAsync(
            () => _repository.GetListAsync())).ShouldHaveSingleItem();
        hostDomain.TenantId.ShouldBeNull();
        hostDomain.IsDefault.ShouldBeTrue();
        hostDomain.Origin.ShouldBe("https://go.example.test");
        var hostBackfilled = await WithUnitOfWorkAsync(() =>
            shortLinks.GetAsync(hostLegacy.Id));
        hostBackfilled.DomainId.ShouldBe(hostDomain.Id);
        hostBackfilled.Origin.ShouldBe(hostDomain.Origin);
        using (softDeleteFilter.Disable())
        {
            var deletedHostBackfilled = await WithUnitOfWorkAsync(() =>
                shortLinks.GetAsync(deletedHostLegacy.Id));
            deletedHostBackfilled.IsDeleted.ShouldBeTrue();
            deletedHostBackfilled.DomainId.ShouldBe(hostDomain.Id);
            deletedHostBackfilled.Origin.ShouldBe(hostDomain.Origin);
        }

        using (_currentTenant.Change(tenantId))
        {
            (await WithUnitOfWorkAsync(
                () => _repository.GetListAsync())).ShouldBeEmpty();
            var tenantUnchanged = await WithUnitOfWorkAsync(() =>
                shortLinks.GetAsync(tenantLegacy.Id));
            tenantUnchanged.DomainId.ShouldBeNull();
            tenantUnchanged.Origin.ShouldBeNull();
            using (softDeleteFilter.Disable())
            {
                var deletedTenantUnchanged = await WithUnitOfWorkAsync(() =>
                    shortLinks.GetAsync(deletedTenantLegacy.Id));
                deletedTenantUnchanged.DomainId.ShouldBeNull();
                deletedTenantUnchanged.Origin.ShouldBeNull();
            }
        }

        await contributor.SeedAsync(new DataSeedContext(tenantId));
        await contributor.SeedAsync(new DataSeedContext(tenantId));

        ShortLinkDomain tenantDomain;
        using (_currentTenant.Change(tenantId))
        {
            tenantDomain = (await WithUnitOfWorkAsync(
                () => _repository.GetListAsync())).ShouldHaveSingleItem();
            tenantDomain.TenantId.ShouldBe(tenantId);
            tenantDomain.IsDefault.ShouldBeTrue();
            tenantDomain.Origin.ShouldBe(hostDomain.Origin);
            var tenantBackfilled = await WithUnitOfWorkAsync(() =>
                shortLinks.GetAsync(tenantLegacy.Id));
            tenantBackfilled.DomainId.ShouldBe(tenantDomain.Id);
            tenantBackfilled.Origin.ShouldBe(tenantDomain.Origin);
            using (softDeleteFilter.Disable())
            {
                var deletedTenantBackfilled = await WithUnitOfWorkAsync(() =>
                    shortLinks.GetAsync(deletedTenantLegacy.Id));
                deletedTenantBackfilled.IsDeleted.ShouldBeTrue();
                deletedTenantBackfilled.DomainId.ShouldBe(tenantDomain.Id);
                deletedTenantBackfilled.Origin.ShouldBe(tenantDomain.Origin);
            }
        }

        tenantDomain.Id.ShouldNotBe(hostDomain.Id);
        (await WithUnitOfWorkAsync(
            () => _repository.GetListAsync())).ShouldHaveSingleItem().Id.ShouldBe(hostDomain.Id);
        var hostStillBackfilled = await WithUnitOfWorkAsync(() =>
            shortLinks.GetAsync(hostLegacy.Id));
        hostStillBackfilled.DomainId.ShouldBe(hostDomain.Id);
        hostStillBackfilled.Origin.ShouldBe(hostDomain.Origin);
    }
}
