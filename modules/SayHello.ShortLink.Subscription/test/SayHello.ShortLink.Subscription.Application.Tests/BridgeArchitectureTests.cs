using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using SayHello.Subscription.Public;
using SayHello.Subscription.Public.Entitlements;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace SayHello.ShortLink.Subscription;

public class BridgeArchitectureTests
{
    [Theory]
    [InlineData("SayHello.ShortLink", "SayHello.Subscription")]
    [InlineData("SayHello.Subscription", "SayHello.ShortLink")]
    public void Original_modules_do_not_reference_each_other_the_bridge_or_host(string module, string otherModule)
    {
        var root = Path.Combine(RepositoryRoot(), "modules", module, "src");
        foreach (var project in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            foreach (var reference in XDocument.Load(project).Descendants("ProjectReference"))
            {
                var name = ReferenceName(reference.Attribute("Include")!.Value);
                name.ShouldNotContain(otherModule);
                name.ShouldNotContain("SayHello.ShortLink.Subscription");
                name.ShouldNotContain("SayHello.ShortLink.WebHost");
            }
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(IsSource))
        {
            var source = File.ReadAllText(file);
            source.ShouldNotContain(otherModule);
            source.ShouldNotContain("SayHello.ShortLink.Subscription");
            source.ShouldNotContain("SayHello.ShortLink.WebHost");
        }
    }

    [Fact]
    public void Bridge_projects_follow_shared_definition_and_application_contract_boundaries()
    {
        ProjectReferences("SayHello.ShortLink.Subscription.Domain.Shared")
            .ShouldBe(new[] { "SayHello.Subscription.Domain.Shared" });
        ProjectReferences("SayHello.ShortLink.Subscription.Application").ShouldBe(new[]
        {
            "SayHello.ShortLink.Domain",
            "SayHello.ShortLink.Subscription.Domain.Shared",
            "SayHello.Subscription.Public.Application.Contracts"
        });

        ModuleDependencies(typeof(ShortLinkSubscriptionDomainSharedModule))
            .ShouldBe(new[] { typeof(global::SayHello.Subscription.SubscriptionDomainSharedModule) });
        ModuleDependencies(typeof(ShortLinkSubscriptionApplicationModule)).ShouldBe(new[]
        {
            typeof(ShortLinkDomainModule),
            typeof(ShortLinkSubscriptionDomainSharedModule),
            typeof(SubscriptionPublicApplicationContractsModule)
        }.OrderBy(type => type.FullName).ToArray());

        var forbiddenReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "SayHello.Subscription.Domain",
            "SayHello.Subscription.Public.Application",
            "SayHello.Subscription.Public.HttpApi.Client",
            "SayHello.Subscription.EntityFrameworkCore",
            "SayHello.ShortLink.WebHost.Domain"
        };
        typeof(ShortLinkSubscriptionApplicationModule).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ShouldNotContain(name => forbiddenReferences.Contains(name!));
    }

    [Fact]
    public void Host_owns_local_or_remote_implementation_selection()
    {
        var domainDirectory = Path.Combine(RepositoryRoot(), "host", "src", "SayHello.ShortLink.WebHost.Domain");
        var domainModule = File.ReadAllText(Path.Combine(domainDirectory, "WebHostDomainModule.cs"));
        domainModule.ShouldContain("typeof(ShortLinkSubscriptionDomainSharedModule)");
        domainModule.ShouldNotContain("ShortLinkSubscriptionDefinitionProvider");
        domainModule.ShouldNotContain("ShortLinkSubscriptionOptions");

        var applicationDirectory =
            Path.Combine(RepositoryRoot(), "host", "src", "SayHello.ShortLink.WebHost.Application");
        var applicationModule = File.ReadAllText(Path.Combine(applicationDirectory, "WebHostApplicationModule.cs"));
        applicationModule.ShouldContain("typeof(ShortLinkSubscriptionApplicationModule)");
        applicationModule.ShouldContain("typeof(global::SayHello.Subscription.SubscriptionApplicationModule)");

        var bridgeDependencies = ModuleDependencies(typeof(ShortLinkSubscriptionApplicationModule));
        bridgeDependencies.ShouldNotContain(typeof(SubscriptionPublicApplicationModule));
        bridgeDependencies.ShouldNotContain(typeof(SubscriptionPublicHttpApiClientModule));

        var clientModule = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "modules", "SayHello.Subscription", "src",
            "SayHello.Subscription.Public.HttpApi.Client", "SubscriptionPublicHttpApiClientModule.cs"));
        clientModule.ShouldContain("AddHttpClientProxies");
        clientModule.ShouldContain("SubscriptionPublicRemoteServiceConsts.RemoteServiceName");
    }

    [Fact]
    public void Public_contract_can_be_supplied_by_the_local_app_or_the_HTTP_proxy_module()
    {
        typeof(ICurrentUserEntitlementAppService)
            .IsAssignableFrom(typeof(CurrentUserEntitlementAppService))
            .ShouldBeTrue();

        var services = new ServiceCollection();
        new SubscriptionPublicHttpApiClientModule()
            .ConfigureServices(new ServiceConfigurationContext(services));

        services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(ICurrentUserEntitlementAppService));
    }

    [Fact]
    public void Bridge_definition_is_not_owned_by_web_host()
    {
        var hostRoot = Path.Combine(RepositoryRoot(), "host", "src");
        foreach (var file in Directory.EnumerateFiles(hostRoot, "*.cs", SearchOption.AllDirectories).Where(IsSource))
        {
            File.ReadAllText(file).ShouldNotContain("class ShortLinkSubscriptionDefinitionProvider");
        }

        File.Exists(Path.Combine(
            BridgeRoot(), "src", "SayHello.ShortLink.Subscription.Domain.Shared",
            "ShortLinkSubscriptionDefinitionProvider.cs")).ShouldBeTrue();
    }

    [Fact]
    public void Bridge_projects_are_registered_in_solution_and_ABP_metadata()
    {
        using var metadata = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(BridgeRoot(), "SayHello.ShortLink.Subscription.abpmdl")));
        var packages = metadata.RootElement.GetProperty("packages");
        packages.EnumerateObject().Count().ShouldBe(3);
        var expectedRoles = new Dictionary<string, string>
        {
            ["SayHello.ShortLink.Subscription.Domain.Shared"] = "lib.domain-shared",
            ["SayHello.ShortLink.Subscription.Application"] = "lib.application",
            ["SayHello.ShortLink.Subscription.Application.Tests"] = "lib.test"
        };
        foreach (var package in packages.EnumerateObject())
        {
            var path = package.Value.GetProperty("path").GetString()!;
            var packageFile = Path.Combine(BridgeRoot(), path.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(packageFile).ShouldBeTrue();
            using var packageMetadata = JsonDocument.Parse(File.ReadAllText(packageFile));
            packageMetadata.RootElement.GetProperty("role").GetString().ShouldBe(expectedRoles[package.Name]);
        }

        var solutionProjects = XDocument.Load(Path.Combine(RepositoryRoot(), "SayHello.ShortLink.slnx"))
            .Descendants("Project").Select(project => project.Attribute("Path")!.Value)
            .Where(path => path.Contains("modules/SayHello.ShortLink.Subscription/", StringComparison.Ordinal))
            .ToArray();
        solutionProjects.Length.ShouldBe(3);
        foreach (var project in solutionProjects)
        {
            File.Exists(Path.Combine(RepositoryRoot(), project.Replace('/', Path.DirectorySeparatorChar))).ShouldBeTrue();
        }
    }

    private static Type[] ModuleDependencies(Type module) =>
        module.GetCustomAttributes<DependsOnAttribute>()
            .SelectMany(attribute => attribute.GetDependedTypes())
            .OrderBy(type => type.FullName)
            .ToArray();

    private static string[] ProjectReferences(string projectName)
    {
        var project = Path.Combine(BridgeRoot(), "src", projectName, $"{projectName}.csproj");
        return XDocument.Load(project).Descendants("ProjectReference")
            .Select(reference => ReferenceName(reference.Attribute("Include")!.Value))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string BridgeRoot() =>
        Path.Combine(RepositoryRoot(), "modules", "SayHello.ShortLink.Subscription");

    private static string ReferenceName(string reference) =>
        Path.GetFileNameWithoutExtension(
            reference.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));

    private static bool IsSource(string path)
    {
        var normalized = path.Replace('\\', '/');
        return !normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) &&
               !normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SayHello.ShortLink.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the ShortLink repository root.");
    }
}
