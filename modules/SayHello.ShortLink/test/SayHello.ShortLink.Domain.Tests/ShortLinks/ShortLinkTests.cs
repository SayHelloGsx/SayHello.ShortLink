using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkTests
{
    [Fact]
    public void State_And_Expiration_Should_Be_Managed_By_Aggregate()
    {
        var now = DateTime.UtcNow;
        var shortLink = new ShortLink(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            "aB3xY9q",
            "https://example.com/path",
            " Example ",
            now.AddHours(1));

        shortLink.Status.ShouldBe(ShortLinkStatus.Active);
        shortLink.Title.ShouldBe("Example");
        shortLink.IsExpired(now).ShouldBeFalse();

        shortLink.Disable();
        shortLink.Status.ShouldBe(ShortLinkStatus.Disabled);

        shortLink.Activate();
        shortLink.Status.ShouldBe(ShortLinkStatus.Active);
        shortLink.IsExpired(now.AddHours(2)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("s")]
    [InlineData("S")]
    [InlineData("My_Campaign-1")]
    public void Routing_Identity_Should_Be_Immutable_Across_All_Aggregate_Behavior(string pathNamespace)
    {
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var domain = Guid.NewGuid();
        var namespaceId = pathNamespace.Length == 0 ? (Guid?)null : Guid.NewGuid();
        var link = new ShortLink(
            Guid.NewGuid(), tenant, owner, domain, "HTTPS://GO.EXAMPLE.TEST:443/",
            "AbC123", "https://example.com/start", null, null,
            isCustomCode: true, pathNamespace: pathNamespace, namespaceId: namespaceId);

        link.Update("https://example.com/changed", " Changed ", DateTime.UtcNow.AddDays(1));
        link.Disable();
        link.Activate();
        link.IncreaseVisitCount();

        link.OwnerUserId.ShouldBe(owner);
        link.TenantId.ShouldBe(tenant);
        link.DomainId.ShouldBe(domain);
        link.Origin.ShouldBe("https://go.example.test");
        link.Namespace.ShouldBe(pathNamespace);
        link.NamespaceId.ShouldBe(namespaceId);
        link.Code.ShouldBe("AbC123");
        link.IsCustomCode.ShouldBeTrue();
        link.TotalVisitCount.ShouldBe(1);
        Should.Throw<BusinessException>(() => link.AssignDomain(Guid.NewGuid(), "https://other.example.test"))
            .Code.ShouldBe(ShortLinkErrorCodes.InvalidState);
    }

    [Fact]
    public void Legacy_Constructor_And_Domain_Backfill_Should_Keep_The_Root_Path()
    {
        var link = new ShortLink(
            Guid.NewGuid(), null, Guid.NewGuid(), "Legacy01", "https://example.com/", null, null);
        link.Namespace.ShouldBe(string.Empty);
        link.NamespaceId.ShouldBeNull();
        link.DomainId.ShouldBeNull();
        link.Origin.ShouldBeNull();

        link.AssignDomain(Guid.NewGuid(), "HTTPS://GO.EXAMPLE.TEST:443/");

        link.Namespace.ShouldBe(string.Empty);
        link.NamespaceId.ShouldBeNull();
        link.Origin.ShouldBe("https://go.example.test");

        var domainBound = new ShortLink(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), link.Origin!,
            "Bound001", "https://example.com/", null, null);
        domainBound.Namespace.ShouldBe(string.Empty);
        domainBound.NamespaceId.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("s")]
    public void Empty_Namespace_Ids_Should_Not_Create_A_Routing_Identity(string pathNamespace)
    {
        Should.Throw<BusinessException>(() => new ShortLink(
                Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "https://go.example.test",
                "Valid01", "https://example.com/", null, null,
                pathNamespace: pathNamespace, namespaceId: Guid.Empty))
            .Code.ShouldBe(ShortLinkErrorCodes.NamespaceSelectionConflict);
    }

    [Fact]
    public void Root_Links_Should_Not_Reference_A_User_Namespace()
    {
        Should.Throw<BusinessException>(() => new ShortLink(
                Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "https://go.example.test",
                "Valid01", "https://example.com/", null, null,
                namespaceId: Guid.NewGuid()))
            .Code.ShouldBe(ShortLinkErrorCodes.NamespaceSelectionConflict);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("has space")]
    [InlineData("a/b")]
    [InlineData("s\n")]
    [InlineData("含中文")]
    public void Invalid_Namespace_Should_Not_Be_Stored_In_The_Aggregate(string? pathNamespace)
    {
        Should.Throw<BusinessException>(() => new ShortLink(
                Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "https://go.example.test",
                "Valid01", "https://example.com/", null, null, pathNamespace: pathNamespace!))
            .Code.ShouldBe(ShortLinkErrorCodes.InvalidNamespace);
    }

    [Theory]
    [InlineData(nameof(ShortLink.Namespace))]
    [InlineData(nameof(ShortLink.NamespaceId))]
    [InlineData(nameof(ShortLink.DomainId))]
    [InlineData(nameof(ShortLink.Origin))]
    [InlineData(nameof(ShortLink.Code))]
    public void Routing_Identity_Should_Not_Have_Public_Setters(string property)
    {
        typeof(ShortLink).GetProperty(property)!.GetSetMethod(nonPublic: true)!.IsPublic.ShouldBeFalse();
    }
}
