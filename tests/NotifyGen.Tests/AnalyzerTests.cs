using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NotifyGen.Generator;

namespace NotifyGen.Tests;

public class AnalyzerTests
{
    [Fact]
    public async Task Analyzer_NonPartialClass_ReportsError()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public class Person
                {
                    private string _name;
                }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Should().ContainSingle(d => d.Id == "NOTIFY001");
        diagnostics.First(d => d.Id == "NOTIFY001").Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Analyzer_PartialClassWithNoFields_ReportsWarning()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Empty
                {
                    public string publicField;
                }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Should().ContainSingle(d => d.Id == "NOTIFY002");
        diagnostics.First(d => d.Id == "NOTIFY002").Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task Analyzer_PartialClassWithFields_ReportsNoDiagnostics()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Person
                {
                    private string _name;
                }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Analyzer_NotifyAlsoWithUnknownProperty_ReportsWarning()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Person
                {
                    [NotifyAlso("NonExistentProperty")]
                    private string _name;
                }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Should().ContainSingle(d => d.Id == "NOTIFY003");
        var diagnostic = diagnostics.First(d => d.Id == "NOTIFY003");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Contain("_name");
        diagnostic.GetMessage().Should().Contain("NonExistentProperty");
    }

    [Fact]
    public async Task Analyzer_NotifyAlsoWithExistingProperty_ReportsNoDiagnostics()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Person
                {
                    [NotifyAlso("FullName")]
                    private string _firstName;

                    public string FullName => _firstName;
                }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Analyzer_NotifyAlsoWithGeneratedProperty_ReportsNoDiagnostics()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Person
                {
                    [NotifyAlso("LastName")]
                    private string _firstName;

                    private string _lastName;
                }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Analyzer_MultipleNotifyAlso_OnlyReportsUnknown()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Person
                {
                    [NotifyAlso("FullName")]
                    [NotifyAlso("UnknownProp")]
                    [NotifyAlso("LastName")]
                    private string _firstName;

                    private string _lastName;
                    public string FullName => _firstName;
                }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Should().ContainSingle(d => d.Id == "NOTIFY003");
        diagnostics.First(d => d.Id == "NOTIFY003").GetMessage().Should().Contain("UnknownProp");
    }

    private static async Task<IReadOnlyList<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(NotifyAttribute).Assembly.Location),
        };

        var runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (runtimeAssembly != null)
        {
            references = references.Append(MetadataReference.CreateFromFile(runtimeAssembly.Location)).ToArray();
        }

        var netstandardAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "netstandard");
        if (netstandardAssembly != null)
        {
            references = references.Append(MetadataReference.CreateFromFile(netstandardAssembly.Location)).ToArray();
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new NotifyAnalyzer());

        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        return diagnostics.ToList();
    }
}
