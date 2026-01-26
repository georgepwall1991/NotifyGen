using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NotifyGen.Generator;

namespace NotifyGen.Tests;

/// <summary>
/// Tests for the NotifyGen analyzer diagnostics and code fixes.
/// </summary>
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

    [Fact]
    public async Task CodeFix_NonPartialClass_AddsPartialModifier()
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

        var expectedFixed = """
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
        var fixedSource = await ApplyCodeFixAsync(source);

        // Assert
        fixedSource.Should().Be(expectedFixed);
    }

    [Fact]
    public async Task CodeFix_InternalClass_AddsPartialModifier()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                internal class Person
                {
                    private string _name;
                }
            }
            """;

        var expectedFixed = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                internal partial class Person
                {
                    private string _name;
                }
            }
            """;

        // Act
        var fixedSource = await ApplyCodeFixAsync(source);

        // Assert
        fixedSource.Should().Be(expectedFixed);
    }

    [Fact]
    public async Task CodeFix_ClassWithNoAccessModifier_AddsPartialModifier()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                class Person
                {
                    private string _name;
                }
            }
            """;

        var expectedFixed = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                partial class Person
                {
                    private string _name;
                }
            }
            """;

        // Act
        var fixedSource = await ApplyCodeFixAsync(source);

        // Assert
        fixedSource.Should().Be(expectedFixed);
    }

    [Fact]
    public async Task CodeFix_SealedClass_AddsPartialModifier()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public sealed class Person
                {
                    private string _name;
                }
            }
            """;

        var expectedFixed = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public sealed partial class Person
                {
                    private string _name;
                }
            }
            """;

        // Act
        var fixedSource = await ApplyCodeFixAsync(source);

        // Assert
        fixedSource.Should().Be(expectedFixed);
    }

    private static List<MetadataReference> GetRequiredReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(NotifyAttribute).Assembly.Location),
        };

        // Add runtime assemblies if available
        foreach (var name in new[] { "System.Runtime", "netstandard" })
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == name);
            if (asm != null)
                references.Add(MetadataReference.CreateFromFile(asm.Location));
        }

        return references;
    }

    private static CSharpCompilation CreateCompilation(string source, IEnumerable<MetadataReference> references)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));
    }

    private static async Task<IReadOnlyList<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var references = GetRequiredReferences();
        var compilation = CreateCompilation(source, references);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new NotifyAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        return diagnostics.ToList();
    }

    private static async Task<string> ApplyCodeFixAsync(string source)
    {
        var references = GetRequiredReferences();
        var compilation = CreateCompilation(source, references);

        // Get diagnostics from analyzer
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new NotifyAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        var notify001Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "NOTIFY001");
        if (notify001Diagnostic == null)
            return source; // No diagnostic, return original

        // Create a workspace and document
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable),
            metadataReferences: references);

        var project = workspace.AddProject(projectInfo);

        var document = workspace.AddDocument(
            DocumentInfo.Create(
                documentId,
                "Test.cs",
                loader: TextLoader.From(TextAndVersion.Create(
                    SourceText.From(source),
                    VersionStamp.Create()))));

        // Create and apply code fix
        var codeFixer = new NotifyCodeFixProvider();

        // Get fresh diagnostics from the document
        var freshCompilation = await document.Project.GetCompilationAsync();
        var freshCompilationWithAnalyzers = freshCompilation!.WithAnalyzers(analyzers);
        var freshDiagnostics = await freshCompilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        var freshNotify001 = freshDiagnostics.FirstOrDefault(d => d.Id == "NOTIFY001");

        if (freshNotify001 == null)
            return source;

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            freshNotify001,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await codeFixer.RegisterCodeFixesAsync(context);

        if (actions.Count == 0)
            return source;

        // Apply the first code action
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations.OfType<ApplyChangesOperation>().First().ChangedSolution;
        var changedDocument = changedSolution.GetDocument(document.Id);
        var changedText = await changedDocument!.GetTextAsync();

        return changedText.ToString();
    }
}
