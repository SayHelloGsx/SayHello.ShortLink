using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public class ShortLinkNamespaceTests
{
    private readonly ShortLinkNamespacePolicy _policy =
        new(Options.Create(new ShortLinkNamespaceRouteOptions()));

    [Theory]
    [InlineData("s")]
    [InlineData("S")]
    [InlineData("_")]
    [InlineData("-")]
    [InlineData("Campaign_2026-ABC")]
    public void Valid_Namespace_Should_Be_Preserved_Exactly(string name)
    {
        ShortLinkNamespaceFormat.IsValid(name).ShouldBeTrue();
        _policy.Validate(name).ShouldBe(name);
    }

    [Fact]
    public void Namespace_Length_Limits_Should_Include_Both_Boundaries()
    {
        ShortLinkNamespaceConsts.MinNameLength.ShouldBe(1);
        ShortLinkNamespaceConsts.MaxNameLength.ShouldBe(64);
        ShortLinkNamespaceConsts.DefaultNamespace.ShouldBe("s");
        _policy.Validate(new string('A', 64)).Length.ShouldBe(64);
        ShortLinkNamespaceFormat.IsValid(new string('A', 65)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" s")]
    [InlineData("s ")]
    [InlineData("s\n")]
    [InlineData("s\r\n")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a.b")]
    [InlineData("s%2F")]
    [InlineData("é")]
    [InlineData("名字")]
    public void Invalid_Namespace_Should_Not_Be_Trimmed_Or_Normalized(string? name)
    {
        ShortLinkNamespaceFormat.IsValid(name).ShouldBeFalse();
        Should.Throw<BusinessException>(() => _policy.Validate(name!))
            .Code.ShouldBe(ShortLinkErrorCodes.InvalidNamespace);
    }

    public static IEnumerable<object[]> ProtectedPrefixes()
    {
        foreach (var prefix in new ShortLinkNamespaceRouteOptions().ReservedPrefixes)
        {
            yield return [prefix];
            yield return [prefix.ToUpperInvariant()];
        }
    }

    [Theory]
    [MemberData(nameof(ProtectedPrefixes))]
    public void Every_Protected_Route_Prefix_Should_Be_Rejected_Ignoring_Case(string name)
    {
        var exception = Should.Throw<BusinessException>(() => _policy.Validate(name));
        exception.Code.ShouldBe(ShortLinkErrorCodes.ReservedNamespace);
        exception.Data["Namespace"].ShouldBe(name);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("tenantmanagement")]
    [InlineData("settingmanagement")]
    [InlineData("featuremanagement")]
    [InlineData("abppermissionmanagement")]
    [InlineData("signout-oidc")]
    public void Loaded_Abp_Module_Routes_Should_Remain_Required_Protected_Prefixes(string name)
    {
        new ShortLinkNamespaceRouteOptions().ReservedPrefixes.Contains(name).ShouldBeTrue();
        Should.Throw<BusinessException>(() => _policy.Validate(name.ToUpperInvariant()))
            .Code.ShouldBe(ShortLinkErrorCodes.ReservedNamespace);
    }

    [Fact]
    public void Host_Configured_Protected_Prefix_Should_Use_The_Same_Policy()
    {
        var options = new ShortLinkNamespaceRouteOptions();
        options.ReservedPrefixes.Add("future-route");
        var policy = new ShortLinkNamespacePolicy(Options.Create(options));

        Should.Throw<BusinessException>(() => policy.Validate("FUTURE-ROUTE"))
            .Code.ShouldBe(ShortLinkErrorCodes.ReservedNamespace);
        policy.Validate("Future_Route").ShouldBe("Future_Route");
    }

    [Fact]
    public void Aggregate_Should_Preserve_Owner_Tenant_Domain_And_Exact_Name()
    {
        var id = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var domain = Guid.NewGuid();
        var entity = new ShortLinkNamespace(
            id, tenant, owner, domain, "HTTPS://GO.EXAMPLE.TEST:443/", "My_Campaign");

        entity.Id.ShouldBe(id);
        entity.TenantId.ShouldBe(tenant);
        entity.OwnerUserId.ShouldBe(owner);
        entity.DomainId.ShouldBe(domain);
        entity.Origin.ShouldBe("https://go.example.test");
        entity.Name.ShouldBe("My_Campaign");
        entity.ConcurrencyStamp.ShouldNotBeNullOrWhiteSpace();
        typeof(ISoftDelete).IsAssignableFrom(typeof(ShortLinkNamespace)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Aggregate_Should_Reject_Missing_Owner_Or_Domain(bool missingOwner)
    {
        Should.Throw<BusinessException>(() => new ShortLinkNamespace(
                Guid.NewGuid(), null, missingOwner ? Guid.Empty : Guid.NewGuid(),
                missingOwner ? Guid.NewGuid() : Guid.Empty, "https://go.example.test", "Valid"))
            .Code.ShouldBe(ShortLinkErrorCodes.NamespaceAccessDenied);
    }

    [Theory]
    [InlineData("https://go.example.test/path")]
    [InlineData("ftp://go.example.test")]
    [InlineData("not an origin")]
    public void Aggregate_Should_Reject_Invalid_Origins(string origin)
    {
        Should.Throw<BusinessException>(() => new ShortLinkNamespace(
                Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), origin, "Valid"))
            .Code.ShouldBe(ShortLinkErrorCodes.InvalidDomainOrigin);
    }

    [Theory]
    [InlineData(nameof(ShortLinkNamespace.TenantId))]
    [InlineData(nameof(ShortLinkNamespace.OwnerUserId))]
    [InlineData(nameof(ShortLinkNamespace.DomainId))]
    [InlineData(nameof(ShortLinkNamespace.Origin))]
    [InlineData(nameof(ShortLinkNamespace.Name))]
    public void Namespace_Identity_Should_Not_Have_Public_Setters(string property)
    {
        typeof(ShortLinkNamespace).GetProperty(property)!.GetSetMethod(nonPublic: true)!.IsPublic
            .ShouldBeFalse();
    }
}
