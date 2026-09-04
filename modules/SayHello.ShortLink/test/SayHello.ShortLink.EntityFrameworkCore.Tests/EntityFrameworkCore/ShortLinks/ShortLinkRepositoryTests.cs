using System;
using System.Linq;
using System.Threading.Tasks;
using SayHello.ShortLink.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Guids;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkRepositoryTests : ShortLinkEntityFrameworkCoreTestBase
{
    private readonly IShortLinkRepository _repository;
    private readonly IGuidGenerator _guidGenerator;

    public ShortLinkRepositoryTests()
    {
        _repository = GetRequiredService<IShortLinkRepository>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task Code_Should_Be_Case_Sensitive_And_Remain_Reserved_After_Soft_Delete()
    {
        var ownerId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await _repository.InsertAsync(Create(ownerId, "AbC1234"), autoSave: true);
            await _repository.InsertAsync(Create(ownerId, "abc1234"), autoSave: true);
        });

        (await WithUnitOfWorkAsync(() => _repository.CodeExistsAsync("AbC1234"))).ShouldBeTrue();
        (await WithUnitOfWorkAsync(() => _repository.CodeExistsAsync("abc1234"))).ShouldBeTrue();

        await WithUnitOfWorkAsync(async () =>
        {
            var entity = await _repository.FindByCodeAsync("AbC1234");
            await _repository.DeleteAsync(entity!, autoSave: true);
        });

        (await WithUnitOfWorkAsync(() => _repository.CodeExistsAsync("AbC1234"))).ShouldBeTrue();
        var deleted = await WithUnitOfWorkAsync(
            () => _repository.FindByCodeAsync("AbC1234", includeDeleted: true));
        deleted.ShouldNotBeNull();
        deleted.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task RecordVisitAsync_Should_Insert_Visit_And_Atomically_Increment_Total()
    {
        var link = Create(Guid.NewGuid(), "vIsIt01");
        await WithUnitOfWorkAsync(() => _repository.InsertAsync(link, autoSave: true));

        await WithUnitOfWorkAsync(() => _repository.RecordVisitAsync(
            new ShortLinkVisit(
                _guidGenerator.Create(),
                null,
                link.Id,
                DateTime.UtcNow,
                new string('A', ShortLinkConsts.VisitorHashLength),
                "example.com",
                "Chrome",
                "Desktop")));

        var reloaded = await WithUnitOfWorkAsync(() => _repository.GetAsync(link.Id));
        reloaded.TotalVisitCount.ShouldBe(1);
    }

    [Fact]
    public async Task List_And_Count_Should_Apply_The_Same_Explicit_Filters()
    {
        var firstOwnerId = Guid.NewGuid();
        var secondOwnerId = Guid.NewGuid();
        var first = Create(
            firstOwnerId,
            "Alpha01",
            "https://example.com/target-match",
            "Alpha title");
        var second = Create(
            firstOwnerId,
            "Beta002",
            "https://example.com/other",
            "Beta title");
        second.Disable();
        var third = Create(
            secondOwnerId,
            "Gamma03",
            "https://example.com/third",
            "Another Alpha title");

        await WithUnitOfWorkAsync(async () =>
        {
            await _repository.InsertAsync(first, autoSave: true);
            await _repository.InsertAsync(second, autoSave: true);
            await _repository.InsertAsync(third, autoSave: true);
        });

        var ownerCount = await WithUnitOfWorkAsync(() =>
            _repository.GetCountAsync(firstOwnerId, null, null, null));
        ownerCount.ShouldBe(2);

        var activeOwnerLinks = await WithUnitOfWorkAsync(() =>
            _repository.GetListAsync(
                firstOwnerId,
                null,
                null,
                ShortLinkStatus.Active,
                "code",
                0,
                10));
        activeOwnerLinks.Select(x => x.Code).ShouldBe(["Alpha01"]);

        var filterCount = await WithUnitOfWorkAsync(() =>
            _repository.GetCountAsync(null, null, "Alpha", null));
        var filteredPage = await WithUnitOfWorkAsync(() =>
            _repository.GetListAsync(
                null,
                null,
                "Alpha",
                null,
                "code desc",
                1,
                1));

        filterCount.ShouldBe(2);
        filteredPage.Select(x => x.Code).ShouldBe(["Alpha01"]);

        var targetMatches = await WithUnitOfWorkAsync(() =>
            _repository.GetListAsync(
                firstOwnerId,
                null,
                "target-match",
                null,
                null,
                0,
                10));
        targetMatches.Select(x => x.Code).ShouldBe(["Alpha01"]);
    }

    private ShortLink Create(
        Guid ownerId,
        string code,
        string targetUrl = "https://example.com/",
        string? title = null)
    {
        return new ShortLink(
            _guidGenerator.Create(),
            null,
            ownerId,
            code,
            targetUrl,
            title,
            null);
    }
}
