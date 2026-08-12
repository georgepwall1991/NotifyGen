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
        description.Should().MatchRegex("(?i)computed");
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
    public void Package_Version_IsPatchAheadOfPublishedTwoTwoOh()
    {
        var version = GetCsprojProperty("Version");
        Version.Parse(version).Should().BeGreaterThan(Version.Parse("2.2.0"));
        GetCsprojProperty("PackageReleaseNotes").Should().Contain(version);
    }

    [Fact]
    public void Readme_IsConversionFunnel_WithAbsoluteHttpsLinks()
    {
        var readme = File.ReadAllText(Path.Combine(RepoRoot, "README.md"));
        var version = GetCsprojProperty("Version");

        readme.Should().Contain("## Migrating from `[ObservableProperty]`?");
        readme.Should().Contain("## Quick start");
        readme.Should().Contain("PrivateAssets");
        readme.Should().Contain(version);
        readme.Should().Contain("NotifyComputed");
        readme.Should().Contain("NotifyProperty");
        readme.Should().Contain("NOTIFY023");
        readme.Should().Contain("NOTIFY001");
        readme.Should().Contain("https://georgepwall1991.github.io/NotifyGen/");
        readme.Should().NotContain("until Pages is enabled");
        readme.Should().NotMatchRegex("(?i)pages.{0,40}404");

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

    [Fact]
    public void LaunchChannels_ContainsPasteReady1175AndRedditCopy()
    {
        var channels = File.ReadAllText(Path.Combine(RepoRoot, "docs", "launch-channels.md"));

        channels.Should().Contain("CommunityToolkit/dotnet#1175");
        channels.Should().Contain("NotifyComputed");
        channels.Should().Contain("NOTIFY023");
        channels.Should().Contain("r/csharp");
        channels.Should().Contain("https://georgepwall1991.github.io/NotifyGen/");
        channels.Should().MatchRegex("(?i)do not post");
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
