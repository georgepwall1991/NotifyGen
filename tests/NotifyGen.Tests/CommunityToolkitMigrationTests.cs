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
/// CommunityToolkit takeover: opt-in generation and the migration code-fix.
/// </summary>
public class CommunityToolkitMigrationTests
{
    private const string CommunityToolkitStubs = """
        namespace CommunityToolkit.Mvvm.ComponentModel
        {
            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            public sealed class ObservablePropertyAttribute : Attribute
            {
            }

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
            public sealed class NotifyPropertyChangedForAttribute : Attribute
            {
                public NotifyPropertyChangedForAttribute(params string[] propertyNames) { }
            }

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
            public sealed class NotifyCanExecuteChangedForAttribute : Attribute
            {
                public NotifyCanExecuteChangedForAttribute(string commandName) { }
            }

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            public sealed class NotifyPropertyChangedRecipientsAttribute : Attribute
            {
            }

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            public sealed class NotifyDataErrorInfoAttribute : Attribute
            {
            }
        }
        """;

    private static string WithCommunityToolkit(string usingsAndTypes) =>
        "using System;"
        + Environment.NewLine
        + usingsAndTypes
        + Environment.NewLine
        + CommunityToolkitStubs;

    [Fact]
    public void Generator_NoOptInMarkers_StillGeneratesEveryEligibleField()
    {
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Person
                {
                    [NotifyAlso("FullName")]
                    private string _firstName;

                    private string _lastName;

                    public string FullName => FirstName;
                }
            }
            """;

        var (_, _, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(runResult, "Person.g.cs");

        generated.Should().Contain("public string FirstName");
        generated.Should().Contain("public string LastName");
    }

    [Fact]
    public void Generator_NotifyPlusNotifyProperty_DoesNotGenerateUnmarkedMembers()
    {
        var source = """
            using NotifyGen;

            public class Logger { }

            [Notify]
            public partial class Editor
            {
                [NotifyProperty]
                private string _title;

                private Logger _logger;
                private bool _disposed;
            }
            """;

        var (_, _, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(runResult, "Editor.g.cs");

        generated.Should().Contain("public string Title");
        generated.Should().NotContain("public Logger Logger");
        generated.Should().NotContain("public bool Disposed");
    }

    [Fact]
    public void Generator_NotifyPlusObservableProperty_DoesNotGenerateUnmarkedMembers()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;
            using NotifyGen;

            public class Logger { }

            [Notify]
            public partial class Editor
            {
                [ObservableProperty]
                private string _title;

                private Logger _logger;
                private bool _disposed;
            }
            """
        );

        var (_, _, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(runResult, "Editor.g.cs");

        generated.Should().Contain("public string Title");
        generated.Should().NotContain("public Logger Logger");
        generated.Should().NotContain("public bool Disposed");
    }

    [Fact]
    public async Task Analyzer_NotifyPlusObservableProperty_ReportsNotify022()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;
            using NotifyGen;

            [Notify]
            public partial class Editor
            {
                [ObservableProperty]
                private string _title;

                private bool _disposed;
            }
            """
        );

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(d => d.Id == "NOTIFY022");
        diagnostics.Should().NotContain(d => d.Id == "NOTIFY005");
    }

    [Fact]
    public async Task Analyzer_ObservablePropertyWithoutNotify_ReportsNotify023()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;

            public partial class Editor
            {
                [ObservableProperty]
                private string _title;

                private bool _disposed;
            }
            """
        );

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(d => d.Id == "NOTIFY023");
    }

    [Fact]
    public async Task Analyzer_ClassWithoutNotifyOrObservableProperty_ReportsNothing()
    {
        var source = """
            public class Person
            {
                private string _name;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Analyzer_OptInUnmarkedReadonly_DoesNotReportNotify005()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;
            using NotifyGen;

            public class Logger { }

            [Notify]
            public partial class Editor
            {
                [ObservableProperty]
                private string _title;

                private readonly Logger _logger;
            }
            """
        );

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().NotContain(d => d.Id == "NOTIFY005");
        diagnostics.Should().Contain(d => d.Id == "NOTIFY022");
    }

    [Fact]
    public async Task CodeFix_Notify023_ConvertsTypeToNotifyGenOptIn()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;

            public partial class Editor
            {
                [ObservableProperty]
                private string _title;

                private bool _disposed;
            }
            """
        );

        var fixedSource = await ApplyCodeFixAsync(source, "NOTIFY023");

        fixedSource.Should().Contain("[Notify]");
        fixedSource.Should().Contain("NotifyProperty");
        fixedSource.Should().NotContain("[ObservableProperty]");
        fixedSource.Should().Contain("private bool _disposed");
        fixedSource.Should().Contain("using NotifyGen");
    }

    [Fact]
    public async Task CodeFix_Notify022_RewritesNotifyPropertyChangedForToNotifyComputed()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                [ObservableProperty]
                [NotifyPropertyChangedFor(nameof(FullName))]
                private string _firstName;

                [ObservableProperty]
                [NotifyPropertyChangedFor(nameof(FullName))]
                private string _lastName;

                public string FullName => $"{FirstName} {LastName}";
            }
            """
        );

        var fixedSource = await ApplyCodeFixAsync(source, "NOTIFY022");

        fixedSource.Should().Contain("NotifyProperty");
        fixedSource.Should().NotContain("[ObservableProperty]");
        fixedSource.Should().NotContain("[NotifyPropertyChangedFor");
        fixedSource.Should().Contain("[NotifyComputed");
        fixedSource.Should().Contain("nameof(FirstName)");
        fixedSource.Should().Contain("nameof(LastName)");
        fixedSource.Should().Contain("public string FullName");
    }

    [Fact]
    public async Task CodeFix_Notify022_ConvertsEveryNotifyPropertyChangedForArgument()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                [ObservableProperty]
                [NotifyPropertyChangedFor(nameof(FullName), nameof(Display))]
                private string _firstName;

                public string FullName => FirstName;
                public string Display => FirstName;
            }
            """
        );

        var fixedSource = await ApplyCodeFixAsync(source, "NOTIFY022");

        fixedSource.Should().Contain("[NotifyComputed(nameof(FirstName))]");
        System
            .Text.RegularExpressions.Regex.Matches(
                fixedSource,
                @"\[NotifyComputed\(nameof\(FirstName\)\)\]"
            )
            .Count.Should()
            .Be(2);
        fixedSource.Should().NotContain("[NotifyPropertyChangedFor");
    }

    [Fact]
    public async Task CodeFix_Notify022_LeavesObservablePropertyOnRecipientsMembers()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                [ObservableProperty]
                [NotifyPropertyChangedRecipients]
                private string _title;

                [ObservableProperty]
                private string _body;
            }
            """
        );

        var fixedSource = await ApplyCodeFixAsync(source, "NOTIFY022");

        fixedSource.Should().Contain("[NotifyPropertyChangedRecipients]");
        fixedSource.Should().Contain("[ObservableProperty]");
        fixedSource.Should().Contain("NotifyGen.NotifyProperty");
        var (_, _, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(fixedSource);
        var generated = GeneratorTestHelper.GetGeneratedSource(runResult, "Person.g.cs");
        generated.Should().Contain("public string Body");
        generated.Should().NotContain("public string Title");
    }

    [Fact]
    public async Task CodeFix_Notify022_KeepsExplicitDependsOnWhenGetterIsNotWalkable()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                [ObservableProperty]
                [NotifyPropertyChangedFor(nameof(Initials))]
                private string _firstName;

                public string Initials => Format(FirstName);
                private static string Format(string value) => value;
            }
            """
        );

        var fixedSource = await ApplyCodeFixAsync(source, "NOTIFY022");

        fixedSource.Should().Contain("[NotifyComputed(nameof(FirstName))]");
        fixedSource.Should().NotContain("[NotifyPropertyChangedFor");
    }

    [Fact]
    public async Task CodeFix_Notify022_MergesSourcesIntoExistingNotifyComputed()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                [ObservableProperty]
                [NotifyPropertyChangedFor(nameof(FullName))]
                private string _lastName;

                [NotifyComputed(nameof(FirstName))]
                public string FullName => Format();
                private string Format() => LastName;
            }
            """
        );

        var fixedSource = await ApplyCodeFixAsync(source, "NOTIFY022");

        fixedSource.Should().Contain("NotifyComputed");
        fixedSource.Should().Contain("nameof(FirstName)");
        fixedSource.Should().Contain("nameof(LastName)");
    }

    [Fact]
    public void Generator_NotifyIgnoreWinsOverNotifyProperty()
    {
        var source = """
            using NotifyGen;

            [Notify]
            public partial class Editor
            {
                [NotifyProperty]
                [NotifyIgnore]
                private string _title;

                [NotifyProperty]
                private string _body;
            }
            """;

        var (_, _, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(runResult, "Editor.g.cs");

        generated.Should().Contain("public string Body");
        generated.Should().NotContain("public string Title");
    }

    [Fact]
    public void Generator_NotifyPropertyOnOrdinaryProperty_DoesNotSwitchToOptIn()
    {
        var source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _firstName;
                private string _lastName;

                [NotifyProperty]
                public string Display => FirstName;
            }
            """;

        var (_, _, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(runResult, "Person.g.cs");

        generated.Should().Contain("public string FirstName");
        generated.Should().Contain("public string LastName");
    }

    [Fact]
    public async Task CodeFix_LeavesCanExecuteAndDoesNotInventRelayCommand()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;

            public partial class Editor
            {
                [ObservableProperty]
                [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
                private string _title;

                public object SaveCommand { get; } = new object();

                private void Save() { }
            }
            """
        );

        var fixedSource = await ApplyCodeFixAsync(source, "NOTIFY023");

        fixedSource.Should().Contain("NotifyCanExecuteChangedFor");
        fixedSource.Should().Contain("SaveCommand");
        fixedSource.Should().Contain("private void Save()");
        fixedSource.Should().NotContain("[ObservableProperty]");
        fixedSource.Should().Contain("[Notify]");
    }

    [Fact]
    public async Task CodeFix_TwoTypes_ConvertsIndependentlyAndLeavesRelayCommand()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;

            public partial class First
            {
                [ObservableProperty]
                private string _title;
            }

            public partial class Second
            {
                [ObservableProperty]
                private string _body;

                public object SaveCommand { get; } = new object();
            }
            """
        );

        var afterFirst = await ApplyCodeFixAsync(source, "NOTIFY023");
        afterFirst.Should().Contain("class First");
        afterFirst.Should().Contain("class Second");

        var afterBoth = await ApplyCodeFixAsync(afterFirst, "NOTIFY023");
        afterBoth.Should().NotContain("[ObservableProperty]");
        afterBoth.Should().Contain("public object SaveCommand");
        afterBoth.Split("[Notify]").Length.Should().Be(3);
    }

    [Fact]
    public async Task Analyzer_PartialWithoutLocalCommunityToolkitAttrs_DoesNotReportNotify023()
    {
        var stubs = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;
            """
        );
        var commands = """
            public partial class Editor
            {
                public object SaveCommand { get; } = new object();
            }
            """;
        var properties = """
            using CommunityToolkit.Mvvm.ComponentModel;

            public partial class Editor
            {
                [ObservableProperty]
                private string _title;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(stubs, commands, properties);

        var notify023 = diagnostics.Where(d => d.Id == "NOTIFY023").ToList();
        notify023.Should().ContainSingle();
        notify023[0].Location.GetLineSpan().Path.Should().Be("File2.cs");
    }

    [Fact]
    public async Task CodeFix_Notify023_ConvertsEveryPartialDeclaration()
    {
        var stubs = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;
            """
        );
        var first = """
            using CommunityToolkit.Mvvm.ComponentModel;

            public partial class Editor
            {
                [ObservableProperty]
                private string _title;
            }
            """;
        var second = """
            using CommunityToolkit.Mvvm.ComponentModel;

            public partial class Editor
            {
                [ObservableProperty]
                private string _body;

                public object SaveCommand { get; } = new object();
            }
            """;

        var documents = await ApplyCodeFixAcrossDocumentsAsync(
            "NOTIFY023",
            ("Stubs.cs", stubs),
            ("First.cs", first),
            ("Second.cs", second)
        );

        documents["First.cs"].Should().Contain("NotifyProperty");
        documents["First.cs"].Should().NotContain("[ObservableProperty]");
        documents["Second.cs"].Should().Contain("NotifyProperty");
        documents["Second.cs"].Should().NotContain("[ObservableProperty]");
        documents["Second.cs"].Should().Contain("public object SaveCommand");
        (documents["First.cs"] + documents["Second.cs"]).Should().Contain("[Notify]");
    }

    [Fact]
    public async Task CodeFix_Notify023_ConvertsTwoPartialsInTheSameFile()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;

            public partial class Editor
            {
                [ObservableProperty]
                private string _title;
            }

            public partial class Editor
            {
                [ObservableProperty]
                private string _body;
            }
            """
        );

        var fixedSource = await ApplyCodeFixAsync(source, "NOTIFY023");

        fixedSource.Should().NotContain("[ObservableProperty]");
        System
            .Text.RegularExpressions.Regex.Matches(fixedSource, @"NotifyGen\.NotifyProperty")
            .Count.Should()
            .Be(2);
    }

    [Fact]
    public async Task CodeFix_Notify022_DoesNotDuplicateLongFormNotifyPropertyAttribute()
    {
        var source = WithCommunityToolkit(
            """
            using CommunityToolkit.Mvvm.ComponentModel;
            using NotifyGen;

            [Notify]
            public partial class Editor
            {
                [ObservableProperty]
                [NotifyPropertyAttribute]
                private string _title;
            }
            """
        );

        var fixedSource = await ApplyCodeFixAsync(source, "NOTIFY022");

        System
            .Text.RegularExpressions.Regex.Matches(fixedSource, @"\[NotifyProperty(Attribute)?\]")
            .Count.Should()
            .Be(1);
        fixedSource.Should().NotContain("[ObservableProperty]");
    }

    private static Task<IReadOnlyList<Diagnostic>> GetDiagnosticsAsync(string source) =>
        GetDiagnosticsAsync(new[] { source });

    private static async Task<IReadOnlyList<Diagnostic>> GetDiagnosticsAsync(
        params string[] sources
    )
    {
        var trees = sources
            .Select((text, index) => CSharpSyntaxTree.ParseText(text, path: $"File{index}.cs"))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            trees,
            GetRequiredReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary
            ).WithNullableContextOptions(NullableContextOptions.Enable)
        );
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new NotifyAnalyzer());
        var diagnostics = await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        return diagnostics.ToList();
    }

    private static async Task<IReadOnlyDictionary<string, string>> ApplyCodeFixAcrossDocumentsAsync(
        string diagnosticId,
        params (string Name, string Source)[] files
    )
    {
        var references = GetRequiredReferences();
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary
            ).WithNullableContextOptions(NullableContextOptions.Enable),
            metadataReferences: references
        );
        workspace.AddProject(projectInfo);

        foreach (var (name, source) in files)
        {
            workspace.AddDocument(
                DocumentInfo.Create(
                    DocumentId.CreateNewId(projectId),
                    name,
                    loader: TextLoader.From(
                        TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())
                    )
                )
            );
        }

        var project = workspace.CurrentSolution.GetProject(projectId)!;
        var compilation = await project.GetCompilationAsync();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new NotifyAnalyzer());
        var diagnostics = await compilation!.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        var diagnostic = diagnostics.FirstOrDefault(d => d.Id == diagnosticId);
        if (diagnostic is null)
        {
            var missing = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var document in project.Documents)
                missing[document.Name] = (await document.GetTextAsync()).ToString();
            return missing;
        }

        var hostDocument =
            project.GetDocument(diagnostic.Location.SourceTree) ?? project.Documents.First();
        var actions = new List<CodeAction>();
        await new NotifyCodeFixProvider().RegisterCodeFixesAsync(
            new CodeFixContext(
                hostDocument,
                diagnostic,
                (action, _) => actions.Add(action),
                CancellationToken.None
            )
        );
        if (actions.Count == 0)
        {
            var unchanged = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var document in project.Documents)
                unchanged[document.Name] = (await document.GetTextAsync()).ToString();
            return unchanged;
        }

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var solution = operations.OfType<ApplyChangesOperation>().First().ChangedSolution;
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var document in solution.GetProject(projectId)!.Documents)
            texts[document.Name] = (await document.GetTextAsync()).ToString();
        return texts;
    }

    private static async Task<string> ApplyCodeFixAsync(string source, string diagnosticId)
    {
        var references = GetRequiredReferences();
        var compilation = CreateCompilation(source, references);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new NotifyAnalyzer());
        var diagnostics = await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        if (diagnostics.All(d => d.Id != diagnosticId))
            return source;

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary
            ).WithNullableContextOptions(NullableContextOptions.Enable),
            metadataReferences: references
        );

        workspace.AddProject(projectInfo);
        var document = workspace.AddDocument(
            DocumentInfo.Create(
                documentId,
                "Test.cs",
                loader: TextLoader.From(
                    TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())
                )
            )
        );

        var freshCompilation = await document.Project.GetCompilationAsync();
        var freshDiagnostics = await freshCompilation!
            .WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync();
        var freshDiagnostic = freshDiagnostics.FirstOrDefault(d => d.Id == diagnosticId);
        if (freshDiagnostic is null)
            return source;

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            freshDiagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None
        );
        await new NotifyCodeFixProvider().RegisterCodeFixesAsync(context);
        if (actions.Count == 0)
            return source;

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changedDocument = operations
            .OfType<ApplyChangesOperation>()
            .First()
            .ChangedSolution.GetDocument(document.Id);
        return (await changedDocument!.GetTextAsync()).ToString();
    }

    private static CSharpCompilation CreateCompilation(
        string source,
        IEnumerable<MetadataReference>? references = null
    )
    {
        references ??= GetRequiredReferences();
        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary
            ).WithNullableContextOptions(NullableContextOptions.Enable)
        );
    }

    private static List<MetadataReference> GetRequiredReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location
            ),
            MetadataReference.CreateFromFile(typeof(NotifyAttribute).Assembly.Location),
        };

        foreach (var name in new[] { "System.Runtime", "netstandard" })
        {
            var asm = AppDomain
                .CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == name);
            if (asm != null)
                references.Add(MetadataReference.CreateFromFile(asm.Location));
        }

        return references;
    }
}
