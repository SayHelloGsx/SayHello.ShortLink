using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using SayHello.Subscription;
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
    public void Bridge_has_only_the_two_domain_project_and_module_dependencies()
    {
        var project = Path.Combine(BridgeRoot(), "src", "SayHello.ShortLink.Subscription.Domain",
            "SayHello.ShortLink.Subscription.Domain.csproj");
        var references = XDocument.Load(project).Descendants("ProjectReference")
            .Select(reference => ReferenceName(reference.Attribute("Include")!.Value))
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        references.ShouldBe(new[] { "SayHello.ShortLink.Domain", "SayHello.Subscription.Domain" });

        var moduleDependencies = typeof(ShortLinkSubscriptionDomainModule).GetCustomAttributes<DependsOnAttribute>()
            .SelectMany(attribute => attribute.GetDependedTypes()).ToArray();
        moduleDependencies.OrderBy(type => type.FullName).ShouldBe(new[]
        {
            typeof(ShortLinkDomainModule),
            typeof(SubscriptionDomainModule)
        }.OrderBy(type => type.FullName));

        var forbiddenLayers = new[] { "WebHost", "Identity", "EntityFrameworkCore", ".Public.", ".Admin.", "HttpApi" };
        foreach (var reference in typeof(ShortLinkSubscriptionDomainModule).Assembly.GetReferencedAssemblies())
        {
            foreach (var forbidden in forbiddenLayers)
            {
                reference.Name!.ShouldNotContain(forbidden);
            }
        }
    }

    [Fact]
    public void Bridge_does_not_define_products_or_hardcode_host_mapping_values()
    {
        var root = Path.Combine(BridgeRoot(), "src");
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(IsSource))
        {
            var source = File.ReadAllText(file);
            source.ShouldNotContain("ShortLinkSubscriptionDefinitions");
            source.ShouldNotContain("SubscriptionDefinitionProvider");
            source.ShouldNotContain("new ProductDefinition");
            source.ShouldNotContain("\"short-link\"");
            source.ShouldNotContain("\"max-links\"");
            source.ShouldNotContain("\"statistics\"");
        }
    }

    [Fact]
    public void Host_explicitly_loads_and_maps_the_optional_bridge_using_existing_definitions()
    {
        var directory = Path.Combine(RepositoryRoot(), "host", "src", "SayHello.ShortLink.WebHost.Domain");
        var module = File.ReadAllText(Path.Combine(directory, "WebHostDomainModule.cs"));
        module.ShouldContain("typeof(ShortLinkSubscriptionDomainModule)");
        module.ShouldContain("Configure<ShortLinkSubscriptionOptions>");
        module.ShouldContain("options.ProductCode = ShortLinkSubscriptionDefinitions.ProductCode");
        module.ShouldContain("options.QuotaFeatureKey = ShortLinkSubscriptionDefinitions.MaxLinks");
        module.ShouldContain("options.StatisticsFeatureKey = ShortLinkSubscriptionDefinitions.Statistics");
        XDocument.Load(Path.Combine(directory, "SayHello.ShortLink.WebHost.Domain.csproj"))
            .Descendants("ProjectReference")
            .Select(reference => ReferenceName(reference.Attribute("Include")!.Value))
            .ShouldContain("SayHello.ShortLink.Subscription.Domain");
    }

    [Fact]
    public void Minimal_bridge_projects_are_registered_in_solution_and_ABP_metadata()
    {
        using var metadata = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(BridgeRoot(), "SayHello.ShortLink.Subscription.abpmdl")));
        var packages = metadata.RootElement.GetProperty("packages");
        packages.EnumerateObject().Count().ShouldBe(2);
        foreach (var package in packages.EnumerateObject())
        {
            var path = package.Value.GetProperty("path").GetString()!;
            var packageFile = Path.Combine(BridgeRoot(), path.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(packageFile).ShouldBeTrue();
            using var packageMetadata = JsonDocument.Parse(File.ReadAllText(packageFile));
            packageMetadata.RootElement.GetProperty("role").GetString()
                .ShouldBe(package.Name.EndsWith(".Tests", StringComparison.Ordinal) ? "lib.test" : "lib.domain");
        }

        var solutionProjects = XDocument.Load(Path.Combine(RepositoryRoot(), "SayHello.ShortLink.slnx"))
            .Descendants("Project").Select(project => project.Attribute("Path")!.Value)
            .Where(path => path.Contains("modules/SayHello.ShortLink.Subscription/", StringComparison.Ordinal)).ToArray();
        solutionProjects.Length.ShouldBe(2);
        foreach (var project in solutionProjects)
        {
            File.Exists(Path.Combine(RepositoryRoot(), project.Replace('/', Path.DirectorySeparatorChar))).ShouldBeTrue();
        }
    }

    private static string BridgeRoot() => Path.Combine(RepositoryRoot(), "modules", "SayHello.ShortLink.Subscription");

    private static string ReferenceName(string reference) =>
        Path.GetFileNameWithoutExtension(reference.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));

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
