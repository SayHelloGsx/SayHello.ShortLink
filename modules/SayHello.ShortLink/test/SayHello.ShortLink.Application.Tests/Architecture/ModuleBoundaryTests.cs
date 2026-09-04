using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace SayHello.ShortLink.Architecture;

public class ModuleBoundaryTests
{
    [Fact]
    public void Common_Public_And_Admin_Project_References_Should_Follow_Module_Boundaries()
    {
        var sourceRoot = Path.Combine(
            FindRepositoryRoot(),
            "modules",
            "SayHello.ShortLink",
            "src");

        foreach (var project in Directory.EnumerateFiles(
                     sourceRoot,
                     "*.csproj",
                     SearchOption.AllDirectories))
        {
            var projectName = Path.GetFileNameWithoutExtension(project);
            var references = GetProjectReferences(project);

            if (projectName.StartsWith("SayHello.ShortLink.Common.", StringComparison.Ordinal))
            {
                Assert.DoesNotContain(
                    references,
                    reference => reference.Contains(".Admin.", StringComparison.Ordinal) ||
                                 reference.Contains(".Public.", StringComparison.Ordinal));
            }

            if (projectName.StartsWith("SayHello.ShortLink.Public.", StringComparison.Ordinal))
            {
                Assert.DoesNotContain(
                    references,
                    reference => reference.Contains(".Admin.", StringComparison.Ordinal));
            }

            if (projectName.StartsWith("SayHello.ShortLink.Admin.", StringComparison.Ordinal))
            {
                Assert.DoesNotContain(
                    references,
                    reference => reference.Contains(".Public.", StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void Unsuffixed_Upper_Layer_Projects_Should_Be_Aggregators()
    {
        var sourceRoot = Path.Combine(
            FindRepositoryRoot(),
            "modules",
            "SayHello.ShortLink",
            "src");
        var expectedReferences = new Dictionary<string, string[]>
        {
            ["SayHello.ShortLink.Application.Contracts"] =
            [
                "SayHello.ShortLink.Admin.Application.Contracts",
                "SayHello.ShortLink.Public.Application.Contracts"
            ],
            ["SayHello.ShortLink.Application"] =
            [
                "SayHello.ShortLink.Admin.Application",
                "SayHello.ShortLink.Public.Application",
                "SayHello.ShortLink.Application.Contracts"
            ],
            ["SayHello.ShortLink.HttpApi"] =
            [
                "SayHello.ShortLink.Admin.HttpApi",
                "SayHello.ShortLink.Public.HttpApi",
                "SayHello.ShortLink.Application.Contracts"
            ],
            ["SayHello.ShortLink.HttpApi.Client"] =
            [
                "SayHello.ShortLink.Admin.HttpApi.Client",
                "SayHello.ShortLink.Public.HttpApi.Client",
                "SayHello.ShortLink.Application.Contracts"
            ],
            ["SayHello.ShortLink.Web"] =
            [
                "SayHello.ShortLink.Admin.Web",
                "SayHello.ShortLink.Public.Web",
                "SayHello.ShortLink.Application.Contracts"
            ]
        };

        foreach (var (projectName, expected) in expectedReferences)
        {
            var projectDirectory = Path.Combine(sourceRoot, projectName);
            var project = Path.Combine(projectDirectory, projectName + ".csproj");
            var references = GetProjectReferences(project);

            Assert.Equal(
                expected.OrderBy(value => value, StringComparer.Ordinal),
                references.OrderBy(value => value, StringComparer.Ordinal));

            var sourceFiles = Directory
                .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .ToList();
            Assert.Single(sourceFiles);
        }
    }

    [Fact]
    public void Solution_And_Module_Metadata_Should_Not_Contain_Blazor()
    {
        var repositoryRoot = FindRepositoryRoot();
        var moduleRoot = Path.Combine(repositoryRoot, "modules", "SayHello.ShortLink");
        var metadata = File.ReadAllText(Path.Combine(moduleRoot, "SayHello.ShortLink.abpmdl"));
        var solution = File.ReadAllText(Path.Combine(repositoryRoot, "SayHello.ShortLink.slnx"));

        Assert.DoesNotContain("Blazor", metadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Blazor", solution, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(moduleRoot, "*", SearchOption.AllDirectories),
            path => Path.GetFileName(path).Contains("Blazor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Application_Projects_Should_Not_Contain_Database_Queryables()
    {
        var sourceRoot = Path.Combine(
            FindRepositoryRoot(),
            "modules",
            "SayHello.ShortLink",
            "src");
        var applicationProjects = new[]
        {
            "SayHello.ShortLink.Common.Application",
            "SayHello.ShortLink.Public.Application",
            "SayHello.ShortLink.Admin.Application"
        };

        foreach (var project in applicationProjects)
        {
            var projectDirectory = Path.Combine(sourceRoot, project);
            foreach (var sourceFile in Directory.EnumerateFiles(
                         projectDirectory,
                         "*.cs",
                         SearchOption.AllDirectories).Where(path => !IsBuildOutput(path)))
            {
                var source = File.ReadAllText(sourceFile);
                Assert.DoesNotContain("GetQueryableAsync", source, StringComparison.Ordinal);
                Assert.DoesNotContain("IQueryable<", source, StringComparison.Ordinal);
                Assert.DoesNotContain("IRepository<", source, StringComparison.Ordinal);
            }
        }
    }

    private static IReadOnlyCollection<string> GetProjectReferences(string project)
    {
        return XDocument
            .Load(project)
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Replace('\\', '/'))
            .Select(value => value[(value.LastIndexOf('/') + 1)..])
            .Select(Path.GetFileNameWithoutExtension)
            .Where(value => value is not null)
            .Select(value => value!)
            .ToList();
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
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
