using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace SayHello.Subscription.Architecture;

public class ModuleBoundaryTests
{
    [Fact]
    public void Applications_never_compose_database_queries_or_reference_EF()
    {
        foreach (var directory in Directory.EnumerateDirectories(SourceRoot).Where(p => Path.GetFileName(p).Contains(".Application")))
        {
            var project = XDocument.Load(Path.Combine(directory, Path.GetFileName(directory) + ".csproj"));
            Assert.DoesNotContain(project.Descendants().Attributes("Include"), a => a.Value.Contains("EntityFrameworkCore", StringComparison.Ordinal));
            foreach (var path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).Where(NotBuildOutput))
            {
                var code = File.ReadAllText(path);
                Assert.DoesNotContain("IQueryable", code, StringComparison.Ordinal);
                Assert.DoesNotContain("GetQueryableAsync", code, StringComparison.Ordinal);
                Assert.DoesNotContain("IRepository", code, StringComparison.Ordinal);
                Assert.DoesNotContain("Microsoft.EntityFrameworkCore", code, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Source_projects_observe_shared_surface_and_business_module_boundaries()
    {
        var expectedCommonReferences = new Dictionary<string, string>
        {
            ["SayHello.Subscription.Admin.HttpApi"] = "SayHello.Subscription.Common.HttpApi",
            ["SayHello.Subscription.Public.HttpApi"] = "SayHello.Subscription.Common.HttpApi",
            ["SayHello.Subscription.Admin.HttpApi.Client"] = "SayHello.Subscription.Common.HttpApi.Client",
            ["SayHello.Subscription.Public.HttpApi.Client"] = "SayHello.Subscription.Common.HttpApi.Client",
            ["SayHello.Subscription.Admin.Web"] = "SayHello.Subscription.Common.Web",
            ["SayHello.Subscription.Public.Web"] = "SayHello.Subscription.Common.Web"
        };

        foreach (var path in Directory.EnumerateFiles(SourceRoot, "*.csproj", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var refs = XDocument.Load(path).Descendants("ProjectReference").Select(e => e.Attribute("Include")!.Value).ToArray();
            Assert.DoesNotContain(refs, r => r.Contains("SayHello.ShortLink", StringComparison.Ordinal));
            if (name.Contains(".Common.") || name.Contains(".Domain"))
                Assert.DoesNotContain(refs, r => r.Contains(".Public.") || r.Contains(".Admin."));
            if (name.Contains(".Public.")) Assert.DoesNotContain(refs, r => r.Contains(".Admin."));
            if (name.Contains(".Admin.")) Assert.DoesNotContain(refs, r => r.Contains(".Public."));
            if (name.EndsWith(".Web") || name.EndsWith(".HttpApi.Client"))
                Assert.DoesNotContain(refs, r => Path.GetFileNameWithoutExtension(r).EndsWith(".Application"));
            if (expectedCommonReferences.TryGetValue(name, out var commonReference))
            {
                Assert.Contains(refs, reference =>
                    Path.GetFileNameWithoutExtension(reference).Equals(commonReference, StringComparison.Ordinal));
                var moduleSource = File.ReadAllText(Path.Combine(
                    Path.GetDirectoryName(path)!,
                    GetModuleTypeName(name) + ".cs"));
                Assert.Contains($"typeof({GetModuleTypeName(commonReference)})", moduleSource, StringComparison.Ordinal);
            }
        }

        var expectedAggregateReferences = new Dictionary<string, string[]>
        {
            ["Application.Contracts"] =
            [
                "SayHello.Subscription.Admin.Application.Contracts",
                "SayHello.Subscription.Public.Application.Contracts"
            ],
            ["Application"] =
            [
                "SayHello.Subscription.Admin.Application",
                "SayHello.Subscription.Application.Contracts",
                "SayHello.Subscription.Public.Application"
            ],
            ["HttpApi"] =
            [
                "SayHello.Subscription.Admin.HttpApi",
                "SayHello.Subscription.Application.Contracts",
                "SayHello.Subscription.Public.HttpApi"
            ],
            ["HttpApi.Client"] =
            [
                "SayHello.Subscription.Admin.HttpApi.Client",
                "SayHello.Subscription.Application.Contracts",
                "SayHello.Subscription.Public.HttpApi.Client"
            ],
            ["Web"] =
            [
                "SayHello.Subscription.Admin.Web",
                "SayHello.Subscription.Application.Contracts",
                "SayHello.Subscription.Public.Web"
            ]
        };

        foreach (var (layer, expectedReferences) in expectedAggregateReferences)
        {
            var projectName = "SayHello.Subscription." + layer;
            var directory = Path.Combine(SourceRoot, projectName);
            var project = XDocument.Load(Path.Combine(directory, projectName + ".csproj"));
            var actualReferences = project.Descendants("ProjectReference")
                .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value))
                .OrderBy(reference => reference, StringComparer.Ordinal);
            Assert.Equal(expectedReferences.OrderBy(reference => reference, StringComparer.Ordinal), actualReferences);

            var sourceFile = Assert.Single(
                Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories),
                NotBuildOutput);
            Assert.EndsWith("Module.cs", sourceFile, StringComparison.Ordinal);
            var actualModuleDependencies = Regex.Matches(
                    File.ReadAllText(sourceFile),
                    @"typeof\((?<module>\w+Module)\)")
                .Select(match => match.Groups["module"].Value)
                .OrderBy(module => module, StringComparer.Ordinal);
            Assert.Equal(
                expectedReferences.Select(GetModuleTypeName).OrderBy(module => module, StringComparer.Ordinal),
                actualModuleDependencies);
        }

        foreach (var project in Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "modules", "SayHello.ShortLink", "src"),
                     "*.csproj", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain(XDocument.Load(project).Descendants("ProjectReference"),
                reference => reference.Attribute("Include")!.Value.Contains("SayHello.Subscription", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Scaffold_has_twenty_three_source_four_test_projects_and_matching_metadata()
    {
        var moduleRoot = Path.GetDirectoryName(SourceRoot)!;
        Assert.Equal(23, Directory.EnumerateFiles(SourceRoot, "*.csproj", SearchOption.AllDirectories).Count());
        Assert.Equal(4, Directory.EnumerateFiles(Path.Combine(moduleRoot, "test"), "*.csproj", SearchOption.AllDirectories).Count());
        using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(moduleRoot, "SayHello.Subscription.abpmdl")));
        Assert.Equal(27, metadata.RootElement.GetProperty("packages").EnumerateObject().Count());
        var solution = XDocument.Load(Path.Combine(RepositoryRoot, "SayHello.ShortLink.slnx"));
        Assert.Equal(27, solution.Descendants("Project").Count(p =>
            p.Attribute("Path")!.Value.Contains("modules/SayHello.Subscription/")));
        Assert.DoesNotContain("Blazor", metadata.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool NotBuildOutput(string path) =>
        !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p is "bin" or "obj");

    private static string GetModuleTypeName(string projectName)
    {
        const string prefix = "SayHello.Subscription.";
        return "Subscription" + projectName[prefix.Length..].Replace(".", string.Empty) + "Module";
    }

    private static string SourceRoot => Path.Combine(RepositoryRoot, "modules", "SayHello.Subscription", "src");

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "SayHello.ShortLink.slnx")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException();
        }
    }
}
