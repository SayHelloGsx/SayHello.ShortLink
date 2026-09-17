using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using SayHello.ShortLink.Admin.ShortLinkDomains;
using SayHello.ShortLink.ShortLinkDomains;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
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
                        IsEnabled = false
                    }));
            cannotDisable.Code.ShouldBe(
                ShortLinkErrorCodes.DefaultDomainCannotBeDisabled);

            var disabledCandidate = await _appService.SetEnabledAsync(
                second.Id,
                new SetShortLinkDomainEnabledDto
                {
                    IsEnabled = false
                });
            var newDefault = await _appService.SetDefaultAsync(disabledCandidate.Id);
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
                    IsEnabled = false
                });
            disabled.IsEnabled.ShouldBeFalse();

            await _appService.DeleteAsync(disabled.Id);
            (await _appService.GetListAsync()).Items.Single().Id
                .ShouldBe(second.Id);

            var cannotDelete = await Should.ThrowAsync<BusinessException>(() =>
                _appService.DeleteAsync(newDefault.Id));
            cannotDelete.Code.ShouldBe(
                ShortLinkErrorCodes.DefaultDomainCannotBeDeleted);
        }
    }

    [Theory]
    [InlineData("23503", "insert or update on table violates foreign key constraint")]
    [InlineData(null, "SQLite Error 19: 'FOREIGN KEY constraint failed'.")]
    public void Delete_Should_Recognize_Provider_Foreign_Key_Violations(
        string? sqlState,
        string message)
    {
        var exception = new InvalidOperationException(
            "Database update failed.",
            new TestDatabaseException(message, sqlState));

        ShortLinkDomainAppService.IsForeignKeyViolation(exception).ShouldBeTrue();
    }

    [Fact]
    public void Delete_Should_Not_Translate_Unrelated_Database_Errors()
    {
        var exception = new InvalidOperationException(
            "Database update failed.",
            new TestDatabaseException("Unique constraint failed.", "23505"));

        ShortLinkDomainAppService.IsForeignKeyViolation(exception).ShouldBeFalse();
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

    private sealed class TestDatabaseException(string message, string? sqlState)
        : DbException(message)
    {
        public override string? SqlState => sqlState;
    }
}
