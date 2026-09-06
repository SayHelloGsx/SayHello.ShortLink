using System;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        }

        foreach (var layer in new[] { "Application", "Application.Contracts", "HttpApi", "HttpApi.Client", "Web" })
        {
            var files = Directory.EnumerateFiles(Path.Combine(SourceRoot, "SayHello.Subscription." + layer), "*.cs",
                SearchOption.AllDirectories).Where(NotBuildOutput);
            Assert.EndsWith("Module.cs", Assert.Single(files), StringComparison.Ordinal);
        }

        foreach (var project in Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "modules", "SayHello.ShortLink", "src"),
                     "*.csproj", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain(XDocument.Load(project).Descendants("ProjectReference"),
                reference => reference.Attribute("Include")!.Value.Contains("SayHello.Subscription", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Scaffold_has_twenty_source_four_test_projects_and_matching_metadata()
    {
        var moduleRoot = Path.GetDirectoryName(SourceRoot)!;
        Assert.Equal(20, Directory.EnumerateFiles(SourceRoot, "*.csproj", SearchOption.AllDirectories).Count());
        Assert.Equal(4, Directory.EnumerateFiles(Path.Combine(moduleRoot, "test"), "*.csproj", SearchOption.AllDirectories).Count());
        using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(moduleRoot, "SayHello.Subscription.abpmdl")));
        Assert.Equal(24, metadata.RootElement.GetProperty("packages").EnumerateObject().Count());
        var solution = XDocument.Load(Path.Combine(RepositoryRoot, "SayHello.ShortLink.slnx"));
        Assert.Equal(24, solution.Descendants("Project").Count(p =>
            p.Attribute("Path")!.Value.Contains("modules/SayHello.Subscription/")));
        Assert.DoesNotContain("Blazor", metadata.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool NotBuildOutput(string path) =>
        !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p is "bin" or "obj");

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
