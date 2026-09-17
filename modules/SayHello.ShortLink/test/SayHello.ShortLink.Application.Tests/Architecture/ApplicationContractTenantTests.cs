using System;
using System.Linq;
using SayHello.ShortLink.Admin.BlockedDomains;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.ShortLinks;
using Shouldly;
using Xunit;

namespace SayHello.ShortLink.Architecture;

public class ApplicationContractTenantTests
{
    [Fact]
    public void Application_contracts_never_expose_tenant_identifiers()
    {
        var contractAssemblies = new[]
        {
            typeof(ShortLinkDto).Assembly,
            typeof(IBlockedDomainAppService).Assembly,
            typeof(IShortLinkAppService).Assembly
        }.Distinct();

        var exposedMembers = contractAssemblies
            .SelectMany(assembly => assembly.ExportedTypes)
            .SelectMany(type =>
                type.GetProperties()
                    .Where(property => property.Name.Equals("TenantId", StringComparison.OrdinalIgnoreCase))
                    .Select(property => $"{type.FullName}.{property.Name}")
                    .Concat(type.GetMethods().SelectMany(method => method.GetParameters()
                        .Where(parameter => parameter.Name?.Equals(
                            "tenantId",
                            StringComparison.OrdinalIgnoreCase) == true)
                        .Select(parameter => $"{type.FullName}.{method.Name}({parameter.Name})"))))
            .ToArray();

        exposedMembers.ShouldBeEmpty();
    }
}
