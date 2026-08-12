using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NotifyGen.Tests;

public class NotifyComputedTests
{
    [Fact]
    public void Interpolation_WiresGeneratedSources()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _firstName = string.Empty;
                private string _lastName = string.Empty;

                [NotifyComputed]
                public string FullName => $"{FirstName} {LastName}";
            }
            """;

        SetFirstName(source).Should().Equal("FirstName", "FullName");
    }

    [Fact]
    public void UnderscoreFieldInGetter_MapsToGeneratedProperty()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _firstName = string.Empty;
                private string _lastName = string.Empty;

                [NotifyComputed]
                public string FullName => $"{_firstName} {_lastName}";
            }
            """;

        SetFirstName(source).Should().Equal("FirstName", "FullName");
    }

    [Fact]
    public void BlockGetter_AndThisQualifier_AreAccepted()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed]
                public string DisplayName
                {
                    get { return this.Name; }
                }
            }
            """;

        SetProperty(source, "Name", "Ada").Should().Equal("Name", "DisplayName");
    }

    [Fact]
    public void NotifyAlsoTypoOnComputedSource_ReportsNotify003AndDoesNotEmit()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed]
                [NotifyAlso("Typo")]
                public string UpperName => Name;
            }
            """;

        RunAnalyzer(source).Should().Contain(diagnostic => diagnostic.Id == "NOTIFY003");
        GeneratedPersonSource(source).Should().NotContain("OnPropertyChanged(\"Typo\")");
    }

    [Fact]
    public void NotifyAlsoSubPropertyOnComputedSource_ReportsNotify012AndHasNoSubscription()
    {
        const string source = """
            using System.ComponentModel;
            using NotifyGen;

            public sealed class Child : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;
            }

            [Notify]
            public partial class Person
            {
                private Child _child = new();

                [NotifyComputed]
                [NotifyAlso(nameof(Label), NotifyOnSubPropertyChanged = true)]
                public Child Current => Child;

                public string Label => "";
            }
            """;

        RunAnalyzer(source).Should().Contain(diagnostic => diagnostic.Id == "NOTIFY012");
        GeneratedPersonSource(source).Should().NotContain("PropertyChanged +=");
    }

    [Fact]
    public void NotifyFromOnComputedSource_WiresTransitiveGreeting()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed]
                public string UpperName => Name;

                [NotifyAlso(nameof(UpperName), NotifyFrom = true)]
                public string Greeting => UpperName;
            }
            """;

        RunAnalyzer(source).Should().NotContain(diagnostic => diagnostic.Id == "NOTIFY011");
        SetProperty(source, "Name", "Ada").Should().Equal("Name", "UpperName", "Greeting");
    }

    [Fact]
    public void SplitPartialComputedProperty_UsesImplementationGetter()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed]
                public partial string Display { get; }
            }

            public partial class Person
            {
                public partial string Display => Name;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorWithLanguageVersionAndAssertCompiles(
            source,
            LanguageVersion.Preview
        );
        using var assemblyStream = new MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        var changed = new List<string>();
        ((INotifyPropertyChanged)person).PropertyChanged += (_, args) =>
            changed.Add(args.PropertyName!);

        personType.GetProperty("Name")!.SetValue(person, "Ada");
        changed.Should().Equal("Name", "Display");
    }

    [Fact]
    public void TransitiveComputed_FlattensOntoGeneratedSource()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _firstName = string.Empty;
                private string _lastName = string.Empty;

                [NotifyComputed]
                public string FullName => $"{FirstName} {LastName}";

                [NotifyComputed]
                public string Greeting => $"Hello, {FullName}";
            }
            """;

        SetFirstName(source).Should().Equal("FirstName", "FullName", "Greeting");
    }

    [Fact]
    public void ComputedCycle_ReportsNotify008()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed]
                public string Left => Right;

                [NotifyComputed]
                public string Right => Left;
            }
            """;

        var diagnostics = RunAnalyzer(source);

        diagnostics.Should().Contain(diagnostic => diagnostic.Id == "NOTIFY008");
    }

    [Fact]
    public void MethodCallGetter_WithoutDependsOn_ReportsNotify021AndWiresNothing()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _firstName = string.Empty;

                [NotifyComputed]
                public string FullName => Compute();

                private string Compute() => FirstName;
            }
            """;

        var diagnostics = RunAnalyzer(source);
        diagnostics.Should().Contain(diagnostic => diagnostic.Id == "NOTIFY021");

        var generated = GeneratedPersonSource(source);
        generated.Should().NotContain("OnPropertyChanged(\"FullName\")");
    }

    [Fact]
    public void ExplicitDependsOn_AllowsLinqGetter()
    {
        const string source = """
            using System.Linq;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _firstName = string.Empty;
                private string _lastName = string.Empty;

                [NotifyComputed(nameof(FirstName), nameof(LastName))]
                public string FullName
                {
                    get
                    {
                        var parts = new[] { FirstName, LastName }
                            .Where(s => !string.IsNullOrWhiteSpace(s));
                        return string.Join(" ", parts);
                    }
                }
            }
            """;

        SetFirstName(source).Should().Equal("FirstName", "FullName");
    }

    [Fact]
    public void HandwrittenSource_ReportsNotify011AndDoesNotWireGeneratedSetter()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                public string Manual => Name;

                [NotifyComputed(nameof(Manual))]
                public string Display => Manual;
            }
            """;

        RunAnalyzer(source).Should().Contain(diagnostic => diagnostic.Id == "NOTIFY011");
        GeneratedPersonSource(source).Should().NotContain("OnPropertyChanged(\"Display\")");
    }

    [Fact]
    public void InferredHandwrittenSource_ReportsNotify011AndDoesNotWire()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                public string Manual => Name;

                [NotifyComputed]
                public string Display => Manual;
            }
            """;

        RunAnalyzer(source).Should().Contain(diagnostic => diagnostic.Id == "NOTIFY011");
        GeneratedPersonSource(source).Should().NotContain("OnPropertyChanged(\"Display\")");
    }

    [Fact]
    public void LocalShadowingGeneratedName_DoesNotWireAndReportsNotify021()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _firstName = string.Empty;

                [NotifyComputed]
                public string FullName
                {
                    get
                    {
                        var FirstName = "x";
                        return FirstName;
                    }
                }
            }
            """;

        RunAnalyzer(source).Should().Contain(diagnostic => diagnostic.Id == "NOTIFY021");
        GeneratedPersonSource(source).Should().NotContain("OnPropertyChanged(\"FullName\")");
    }

    [Fact]
    public void EmptyExplicitDependsOn_ReportsNotify018AndDoesNotInfer()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed(new string[0])]
                public string Display => Name;
            }
            """;

        RunAnalyzer(source).Should().Contain(diagnostic => diagnostic.Id == "NOTIFY018");
        GeneratedPersonSource(source).Should().NotContain("OnPropertyChanged(\"Display\")");
    }

    [Fact]
    public void StaticNotifyComputedTarget_ReportsNotify020AndDoesNotWire()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed(nameof(Name))]
                public static string StaticLabel => "fixed";
            }
            """;

        RunAnalyzer(source).Should().Contain(diagnostic => diagnostic.Id == "NOTIFY020");
        GeneratedPersonSource(source).Should().NotContain("OnPropertyChanged(\"StaticLabel\")");
    }

    [Fact]
    public void IndexerNotifyComputedTarget_ReportsNotify020AndDoesNotWire()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed(nameof(Name))]
                public string this[int index] => Name;
            }
            """;

        RunAnalyzer(source).Should().Contain(diagnostic => diagnostic.Id == "NOTIFY020");
        GeneratedPersonSource(source).Should().NotContain("OnPropertyChanged(\"this[]\")");
    }

    [Fact]
    public void WritableNotifyComputedTarget_ReportsNotify020AndDoesNotWire()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed(nameof(Name))]
                public string Display { get; set; } = string.Empty;
            }
            """;

        RunAnalyzer(source).Should().Contain(diagnostic => diagnostic.Id == "NOTIFY020");
        GeneratedPersonSource(source).Should().NotContain("OnPropertyChanged(\"Display\")");
    }

    [Fact]
    public void NullExplicitDependsOn_DoesNotCrashGenerator()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed(null)]
                public string Display => Name;
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        result
            .Diagnostics.Should()
            .NotContain(diagnostic =>
                diagnostic.Id == "CS8785"
                || diagnostic.GetMessage().Contains("NullReferenceException")
            );
        result
            .OutputCompilation.GetDiagnostics()
            .Should()
            .NotContain(diagnostic => diagnostic.Id == "CS8785");

        var generated = GeneratorTestHelper.GetGeneratedSource(result.RunResult, "Person.g.cs");
        generated.Should().NotBeNull();
        generated.Should().Contain("public string Name");
    }

    [Fact]
    public void UnknownDependsOn_ReportsNotify003()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed(nameof(Missing))]
                public string DisplayName => Name;
            }
            """;

        RunAnalyzer(source).Should().Contain(diagnostic => diagnostic.Id == "NOTIFY003");
    }

    [Fact]
    public void EmptyComputedGetter_ReportsNotify018()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed]
                public string Constant => "fixed";
            }
            """;

        RunAnalyzer(source).Should().Contain(diagnostic => diagnostic.Id == "NOTIFY018");
    }

    [Fact]
    public void NotifyComputedOnGeneratedPartialProperty_ReportsNotify019()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                [NotifyComputed]
                public partial string Name { get; set; }
            }
            """;

        RunAnalyzer(source, LanguageVersion.Preview)
            .Should()
            .Contain(diagnostic => diagnostic.Id == "NOTIFY019");
    }

    [Fact]
    public void SameValue_DoesNotRaiseComputedTarget()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _firstName = "Ada";
                private string _lastName = "Lovelace";

                [NotifyComputed]
                public string FullName => $"{FirstName} {LastName}";
            }
            """;

        SetFirstName(source, "Ada").Should().BeEmpty();
    }

    [Fact]
    public void NotifyAlsoAndNotifyComputed_MergeAndDedupe()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                [NotifyAlso(nameof(FullName))]
                private string _firstName = string.Empty;
                private string _lastName = string.Empty;

                [NotifyComputed]
                public string FullName => $"{FirstName} {LastName}";
            }
            """;

        SetFirstName(source).Should().Equal("FirstName", "FullName");
    }

    [Fact]
    public void ComputedAndExplicitNotifyAlso_ProduceTheSameAlsoNotifyNames()
    {
        const string computed = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _firstName = string.Empty;
                private string _lastName = string.Empty;

                [NotifyComputed]
                public string FullName => $"{FirstName} {LastName}";
            }
            """;

        const string explicitAlso = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                [NotifyAlso(nameof(FullName))]
                private string _firstName = string.Empty;

                [NotifyAlso(nameof(FullName))]
                private string _lastName = string.Empty;

                public string FullName => $"{FirstName} {LastName}";
            }
            """;

        var computedSource = GeneratedPersonSource(computed);
        var explicitSource = GeneratedPersonSource(explicitAlso);

        CountNotifyCalls(computedSource, "FullName")
            .Should()
            .Be(CountNotifyCalls(explicitSource, "FullName"));
        CountNotifyCalls(computedSource, "FullName").Should().Be(2);
    }

    [Fact]
    public void Analyzer_Reports018019021AtAttributeLocation()
    {
        const string empty = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyComputed]
                public string Constant => "fixed";
            }
            """;

        var emptyDiagnostic = RunAnalyzer(empty)
            .Should()
            .ContainSingle(diagnostic => diagnostic.Id == "NOTIFY018")
            .Subject;
        emptyDiagnostic.Location.GetLineSpan().StartLinePosition.Line.Should().BeGreaterThan(0);

        const string generatedPartial = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                [NotifyComputed]
                public partial string Name { get; set; }
            }
            """;

        RunAnalyzer(generatedPartial, LanguageVersion.Preview)
            .Should()
            .Contain(diagnostic => diagnostic.Id == "NOTIFY019");

        const string methodCall = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _firstName = string.Empty;

                [NotifyComputed]
                public string FullName => Compute();

                private string Compute() => FirstName;
            }
            """;

        RunAnalyzer(methodCall).Should().Contain(diagnostic => diagnostic.Id == "NOTIFY021");
    }

    private static List<string> SetFirstName(string source, string value = "Ada") =>
        SetProperty(source, "FirstName", value);

    private static List<string> SetProperty(string source, string propertyName, string value)
    {
        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        using var assemblyStream = new MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        var changed = new List<string>();
        ((INotifyPropertyChanged)person).PropertyChanged += (_, args) =>
            changed.Add(args.PropertyName!);

        personType.GetProperty(propertyName)!.SetValue(person, value);
        return changed;
    }

    private static string GeneratedPersonSource(string source)
    {
        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result.RunResult, "Person.g.cs");
        generated.Should().NotBeNull();
        return generated!;
    }

    private static int CountNotifyCalls(string generated, string propertyName) =>
        Regex.Matches(generated, $@"OnPropertyChanged\(""{Regex.Escape(propertyName)}""\)").Count;

    private static IReadOnlyList<Diagnostic> RunAnalyzer(
        string source,
        LanguageVersion languageVersion = LanguageVersion.Default
    )
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(languageVersion)) },
            GeneratorTestHelperReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary
            ).WithNullableContextOptions(NullableContextOptions.Enable)
        );

        var analyzers =
            System.Collections.Immutable.ImmutableArray.Create<Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer>(
                new NotifyGen.Generator.NotifyAnalyzer()
            );
        return compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync().Result;
    }

    private static Microsoft.CodeAnalysis.MetadataReference[] GeneratorTestHelperReferences()
    {
        var platformAssemblies =
            AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException(
                "The .NET runtime did not provide trusted platform assemblies."
            );

        return platformAssemblies
            .Split(Path.PathSeparator)
            .Select(static path =>
                (Microsoft.CodeAnalysis.MetadataReference)MetadataReference.CreateFromFile(path)
            )
            .Append(MetadataReference.CreateFromFile(typeof(NotifyAttribute).Assembly.Location))
            .ToArray();
    }
}
