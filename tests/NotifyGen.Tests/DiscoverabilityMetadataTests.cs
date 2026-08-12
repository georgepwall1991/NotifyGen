using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;

namespace NotifyGen.Tests;

/// <summary>
/// Durable checks that NuGet metadata and the README conversion funnel stay honest.
/// Reads the real repo files, not copies.
/// </summary>
public class DiscoverabilityMetadataTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Package_Description_LeadsWithInpcAndZeroRuntimeWedge()
    {
        var description = GetCsprojProperty("Description");

        description.Should().Contain("INotifyPropertyChanged");
        description.Should().MatchRegex("(?i)source generator");
        description.Should().MatchRegex("(?i)no runtime");
        description
            .Should()
            .MatchRegex("(?i)(no required|without).{0,40}(ObservableObject|base class)");
        description.Should().MatchRegex("(?i)CommunityToolkit");
    }

    [Fact]
    public void Package_Tags_AreTruthfulAndNotStuffed()
    {
        var tags = GetCsprojProperty("PackageTags");

        tags.Should().Contain("INotifyPropertyChanged");
        tags.Should().Contain("INPC");
        tags.Should().Contain("SourceGenerator");
        tags.Should().Contain("Roslyn");
        tags.Should().Contain("MVVM");
        tags.Should().NotContain("ReactiveUI");
        tags.Should().NotContain("android");
        tags.Should().NotContain("linux");
    }

    [Fact]
    public void Package_Version_IsPatchAheadOfPublishedTwoOhOne()
    {
        var version = GetCsprojProperty("Version");
        Version.Parse(version).Should().BeGreaterThan(Version.Parse("2.0.1"));
    }

    [Fact]
    public void Readme_IsConversionFunnel_WithAbsoluteHttpsLinks()
    {
        var readme = File.ReadAllText(Path.Combine(RepoRoot, "README.md"));

        readme.Should().Contain("## Migrating from `[ObservableProperty]`?");
        readme.Should().Contain("## Quick start");
        readme.Should().Contain("PrivateAssets");
        readme.Should().Contain("2.0.2");
        readme.Should().Contain("NOTIFY001");
        readme.Should().NotContain("georgepwall1991.github.io/NotifyGen");

        foreach (Match match in Regex.Matches(readme, @"!\[[^\]]*\]\(([^)]+)\)"))
        {
            match.Groups[1].Value.Should().StartWith("https://");
        }

        foreach (Match match in Regex.Matches(readme, @"<img[^>]+src=""([^""]+)"""))
        {
            match.Groups[1].Value.Should().StartWith("https://");
        }

        foreach (Match match in Regex.Matches(readme, @"\[[^\]]+\]\(([^)]+)\)"))
        {
            var href = match.Groups[1].Value;
            if (href.StartsWith('#') || href.StartsWith("mailto:", StringComparison.Ordinal))
                continue;

            href.Should().StartWith("https://");
        }
    }

    [Fact]
    public void VisualAssets_ExistAndArePacked()
    {
        var icon = new FileInfo(Path.Combine(RepoRoot, "assets", "icon.png"));
        var header = new FileInfo(Path.Combine(RepoRoot, "assets", "header.png"));
        var demo = new FileInfo(Path.Combine(RepoRoot, "assets", "demo.gif"));

        icon.Exists.Should().BeTrue();
        icon.Length.Should().BeGreaterThan(0);
        header.Exists.Should().BeTrue();
        header.Length.Should().BeGreaterThan(0);
        demo.Exists.Should().BeTrue();
        demo.Length.Should().BeGreaterThan(0);

        var csproj = File.ReadAllText(
            Path.Combine(RepoRoot, "src", "NotifyGen.Generator", "NotifyGen.Generator.csproj")
        );
        csproj.Should().Contain(@"assets\**\*");
        csproj.Should().Contain(@"Pack=""true""");
        csproj.Should().Contain(@"PackagePath=""assets\""");
    }

    private static string GetCsprojProperty(string name)
    {
        var path = Path.Combine(
            RepoRoot,
            "src",
            "NotifyGen.Generator",
            "NotifyGen.Generator.csproj"
        );
        var document = XDocument.Load(path);
        var value = document
            .Descendants(name)
            .Select(element => element.Value.Trim())
            .FirstOrDefault();
        value.Should().NotBeNullOrWhiteSpace($"csproj should declare {name}");
        return value!;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NotifyGen.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find NotifyGen.sln from the test output path."
        );
    }
}
